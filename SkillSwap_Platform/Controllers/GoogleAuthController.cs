using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.TextTemplating;
using NuGet.Common;
using SkillSwap_Platform.Models;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
    [Authorize]
    public class GoogleAuthController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleAuthController> _logger;
        private readonly SkillSwapDbContext _dbContext;
        private static readonly string[] Scopes = { "https://www.googleapis.com/auth/calendar" };

        public GoogleAuthController(IConfiguration configuration, ILogger<GoogleAuthController> logger, SkillSwapDbContext dbContext)
        {
            _configuration = configuration;
            _logger = logger;
            _dbContext = dbContext;
        }
        public IActionResult Index()
        {
            return View();
        }

        // Initiates the OAuth process by redirecting to Google's consent screen.
        [HttpGet]
        public IActionResult Authorize(int? exchangeId = null, int? otherUserId = null)
        {
            try
            {

                string scope = exchangeId.HasValue && otherUserId.HasValue
        ? "https://www.googleapis.com/auth/calendar.events"
        : "https://www.googleapis.com/auth/calendar";


                string state = exchangeId.HasValue && otherUserId.HasValue
       ? $"{exchangeId}|{otherUserId}"
       : "connect";


                var clientId = _configuration["Authentication:Google:ClientId"];
                var redirectUri = Url.Action("OAuth2Callback", "GoogleAuth", null, Request.Scheme);
               
                //var authorizationUrl =
                //    "https://accounts.google.com/o/oauth2/v2/auth" +
                //    $"?response_type=code" +
                //    $"&client_id={Uri.EscapeDataString(clientId)}" +
                //    $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                //    $"&scope={Uri.EscapeDataString(string.Join(" ", Scopes))}" +
                //    $"&access_type=offline" +
                //    $"&prompt=consent" +
                //    $"&state={Uri.EscapeDataString(state)}";

                var url = "https://accounts.google.com/o/oauth2/v2/auth"
                    + "?response_type=code"
                    + $"&client_id={Uri.EscapeDataString(clientId)}"
                    + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                    + $"&scope={Uri.EscapeDataString(scope)}"
                    + "&access_type=offline"
                    + "&prompt=consent"
                    + $"&state={Uri.EscapeDataString(state)}";
                return Redirect(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google OAuth authorization initiation.");
                return StatusCode(500, "Internal Server Error");
            }
        }

        // Callback endpoint for handling the OAuth response.
        public async Task<IActionResult> OAuth2Callback(string code, string error, string state, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("OAuth error: {Error}", error);
                return BadRequest("OAuth error: " + error);
            }
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                _logger.LogError("Missing code or state in OAuth callback");
                return BadRequest("Missing code or state.");
            }

            var token = await ExchangeCodeForTokenAsync(code, cancellationToken);
            if (token == null)
            {
                _logger.LogError("Token exchange failed");
                return StatusCode(500, "Token exchange failed");
            }

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await SaveTokensAsync(userId, token);

            if (state == "connect")
            {
                // Pro users return to calendar listing
                return RedirectToAction("Index", "GoogleCalendar");
            }
            else
            {
                // Scheduling flow → parse exchangeId|otherUserId
                var parts = state.Split('|');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out int exchId)
                    && int.TryParse(parts[1], out int otherId))
                {
                    return RedirectToAction(
                        "CreateEvent",
                        "GoogleCalendar",
                        new { exchangeId = exchId, otherUserId = otherId }
                    );
                }

                _logger.LogError("Invalid state in OAuth callback: {State}", state);
                return BadRequest("Invalid state value.");
            }
        }
        private async Task<TokenResponse> ExchangeCodeForTokenAsync(
            string code,
            CancellationToken ct)
        {
            var clientId = _configuration["Authentication:Google:ClientId"];
            var clientSecret = _configuration["Authentication:Google:ClientSecret"];
            var redirectUri = Url.Action(nameof(OAuth2Callback), "GoogleAuth", null, Request.Scheme);

            var tokenRequestUrl = "https://oauth2.googleapis.com/token";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("code",         code),
                new KeyValuePair<string,string>("client_id",    clientId),
                new KeyValuePair<string,string>("client_secret",clientSecret),
                new KeyValuePair<string,string>("redirect_uri", redirectUri),
                new KeyValuePair<string,string>("grant_type",   "authorization_code"),
            });

            using var http = new HttpClient();
            var res = await http.PostAsync(tokenRequestUrl, content, ct);
            if (!res.IsSuccessStatusCode) return null;
            var json = await res.Content.ReadAsStringAsync(ct);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<TokenResponse>(json);
        }

        // ─── Helper: Persist tokens ───
        private async Task SaveTokensAsync(int userId, TokenResponse token)
        {
            var expiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresInSeconds ?? 3600);

            var existing = await _dbContext.UserGoogleTokens
                                     .FirstOrDefaultAsync(t => t.UserId == userId);
            if (existing != null)
            {
                existing.AccessToken = token.AccessToken;
                existing.RefreshToken = token.RefreshToken;
                existing.ExpiresAt = expiresAt;
                _dbContext.UserGoogleTokens.Update(existing);
            }
            else
            {
                _dbContext.UserGoogleTokens.Add(new UserGoogleToken
                {
                    UserId = userId,
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    ExpiresAt = expiresAt
                });
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}