using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace SkillSwap_Platform.Services
{
    public class SensitiveWordService : ISensitiveWordService
    {
        private readonly SkillSwapDbContext _context;
        // Using Lazy<T> for thread-safe, lazy initialization of the automaton.
        private Lazy<Task<AhoCorasickAutomaton>> _automatonCache;
        public SensitiveWordService(SkillSwapDbContext context)
        {
            _context = context;
            _automatonCache = new Lazy<Task<AhoCorasickAutomaton>>(BuildAutomatonAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task<Dictionary<string, string>> CheckSensitiveWordsAsync(string input)
        {
            var foundWarnings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(input))
            {
                return foundWarnings;
            }

            // Normalize the input once.
            string normalizedInput = Normalize(input);
            try
            {
                // Get the automaton from cache (build if necessary).
                var automaton = await _automatonCache.Value;

                // Search the normalized input for any matches.
                var matches = automaton.Search(normalizedInput);

                // Add each found match to foundWarnings (using original sensitive word).
                foreach (var match in matches)
                {
                    if (!foundWarnings.ContainsKey(match.Original))
                    {
                        Debug.WriteLine($"SensitiveWordService: Detected sensitive word '{match.Original}'.");
                        foundWarnings.Add(match.Original, match.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                // Optionally log the exception.
                throw new Exception("Error checking for sensitive words.", ex);
            }
            return foundWarnings;
        }

        /// <summary>
        /// Asynchronously builds the Aho–Corasick automaton from the database.
        /// </summary>
        private async Task<AhoCorasickAutomaton> BuildAutomatonAsync()
        {
            // Load sensitive words from the database.
            var sensitiveWords = await _context.SensitiveWords.ToListAsync();

            // Build a dictionary: normalized word -> (original, warning)
            var wordMapping = new Dictionary<string, (string Original, string Warning)>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in sensitiveWords)
            {
                if (!string.IsNullOrWhiteSpace(item.Word))
                {
                    string normalizedWord = Normalize(item.Word);
                    if (!wordMapping.ContainsKey(normalizedWord))
                    {
                        wordMapping[normalizedWord] = (item.Word, item.WarningMessage);
                    }
                }
            }

            var automaton = new AhoCorasickAutomaton();
            automaton.Build(wordMapping);
            return automaton;
        }

        /// <summary>
        /// Normalizes a string by converting it to lowercase and removing whitespace and punctuation.
        /// </summary>
        private string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            input = input.ToLowerInvariant();
            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        #region Aho–Corasick Implementation

        /// <summary>
        /// Represents a node in the Aho–Corasick trie.
        /// </summary>
        public class AhoCorasickNode
        {
            public Dictionary<char, AhoCorasickNode> Children { get; } = new Dictionary<char, AhoCorasickNode>();
            public AhoCorasickNode Failure { get; set; }
            public List<(string Original, string Warning)> Outputs { get; } = new List<(string, string)>();
        }

        /// <summary>
        /// Implements a basic Aho–Corasick automaton.
        /// </summary>
        public class AhoCorasickAutomaton
        {
            private AhoCorasickNode root;

            public AhoCorasickAutomaton()
            {
                root = new AhoCorasickNode();
            }

            /// <summary>
            /// Builds the automaton from a mapping of normalized keywords to (original, warning) tuples.
            /// </summary>
            public void Build(Dictionary<string, (string Original, string Warning)> wordMapping)
            {
                // Insert all keywords into the trie.
                foreach (var kvp in wordMapping)
                {
                    Insert(kvp.Key, kvp.Value);
                }
                // Build failure links.
                BuildFailureLinks();
            }

            /// <summary>
            /// Inserts a keyword with its associated data into the trie.
            /// </summary>
            private void Insert(string word, (string Original, string Warning) data)
            {
                var node = root;
                foreach (char c in word)
                {
                    if (!node.Children.ContainsKey(c))
                    {
                        node.Children[c] = new AhoCorasickNode();
                    }
                    node = node.Children[c];
                }
                node.Outputs.Add(data);
            }

            /// <summary>
            /// Builds the failure links for the automaton.
            /// </summary>
            private void BuildFailureLinks()
            {
                var queue = new Queue<AhoCorasickNode>();
                root.Failure = root;
                foreach (var child in root.Children.Values)
                {
                    child.Failure = root;
                    queue.Enqueue(child);
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var kvp in current.Children)
                    {
                        char c = kvp.Key;
                        var child = kvp.Value;
                        var failureNode = current.Failure;
                        while (failureNode != root && !failureNode.Children.ContainsKey(c))
                        {
                            failureNode = failureNode.Failure;
                        }
                        if (failureNode.Children.ContainsKey(c) && failureNode.Children[c] != child)
                        {
                            child.Failure = failureNode.Children[c];
                        }
                        else
                        {
                            child.Failure = root;
                        }
                        child.Outputs.AddRange(child.Failure.Outputs);
                        queue.Enqueue(child);
                    }
                }
            }

            /// <summary>
            /// Searches the given text using the automaton.
            /// Returns a list of matches (original word and warning).
            /// </summary>
            public List<(string Original, string Warning)> Search(string text)
            {
                var results = new List<(string Original, string Warning)>();
                var node = root;
                foreach (char c in text)
                {
                    while (node != root && !node.Children.ContainsKey(c))
                    {
                        node = node.Failure;
                    }
                    if (node.Children.ContainsKey(c))
                    {
                        node = node.Children[c];
                    }
                    if (node.Outputs.Count > 0)
                    {
                        results.AddRange(node.Outputs);
                    }
                }
                return results;
            }
        }

        #endregion
    }
}