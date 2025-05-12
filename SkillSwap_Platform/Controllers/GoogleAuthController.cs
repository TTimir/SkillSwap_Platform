using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.TextTemplating;
using NuGet.Common;
using SkillSwap_Platform.Models;
using System.Security.Claims;

namespace SkillSwap_Platform.Controllers
{
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
        public IActionResult Authorize(int exchangeId, int otherUserId)
        {
            try
            {
                var clientId = _configuration["Authentication:Google:ClientId"];
                var redirectUri = Url.Action("OAuth2Callback", "GoogleAuth", null, Request.Scheme);
                var state = $"{exchangeId}|{otherUserId}";
                var scope = "https://www.googleapis.com/auth/calendar.events.readonly";

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
                _logger.LogError("Error returned from Google OAuth: {Error}", error);
                return BadRequest("Error during Google OAuth process.");
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogError("Authorization code is missing in the callback.");
                return BadRequest("Authorization code missing.");
            }

            // Parse the exchangeId and otherUserId from the state parameter.
            if (string.IsNullOrEmpty(state))
            {
                _logger.LogError("State parameter is missing.");
                return BadRequest("State parameter is missing.");
            }

            var stateParts = state.Split('|');
            if (stateParts.Length < 2
                || !int.TryParse(stateParts[0], out int exchangeId) || exchangeId == 0
                || !int.TryParse(stateParts[1], out int otherUserId) || otherUserId == 0)
            {
                _logger.LogError("Invalid exchangeId or otherUserId in state.");
                return BadRequest("A valid exchange ID and a valid OtherUserId must be provided.");
            }

            try
            {
                var clientId = _configuration["Authentication:Google:ClientId"];
                var clientSecret = _configuration["Authentication:Google:ClientSecret"];
                var redirectUri = Url.Action("OAuth2Callback", "GoogleAuth", null, Request.Scheme);

                var tokenRequestUrl = "https://oauth2.googleapis.com/token";
                var requestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                });

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.PostAsync(tokenRequestUrl, requestBody, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<TokenResponse>(responseContent);

                    // Retrieve the current user's id from claims.
                    int userId = 0;
                    var userClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (userClaim != null && int.TryParse(userClaim.Value, out int parsedUserId))
                    {
                        userId = parsedUserId;
                    }
                    else
                    {
                        _logger.LogError("Unable to retrieve user id from claims.");
                        return Unauthorized();
                    }

                    // Save or update token in the database.
                    var existingToken = await _dbContext.UserGoogleTokens.FirstOrDefaultAsync(t => t.UserId == userId);
                    // Calculate expiration time. tokenResponse.ExpiresIn is typically in seconds.
                    //var expiresAt = DateTime.UtcNow.AddSeconds((double)tokenResponse.ExpiresInSeconds);
                    var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds.HasValue ? tokenResponse.ExpiresInSeconds.Value : 3600);

                    if (existingToken != null)
                    {
                        existingToken.AccessToken = tokenResponse.AccessToken;
                        existingToken.RefreshToken = tokenResponse.RefreshToken; // Update only if provided.
                        existingToken.ExpiresAt = expiresAt;
                        _dbContext.UserGoogleTokens.Update(existingToken);
                    }
                    else
                    {
                        var newToken = new UserGoogleToken
                        {
                            UserId = userId,
                            AccessToken = tokenResponse.AccessToken,
                            RefreshToken = tokenResponse.RefreshToken,
                            ExpiresAt = expiresAt
                        };
                        await _dbContext.UserGoogleTokens.AddAsync(newToken);
                    }

                    await _dbContext.SaveChangesAsync();
                }

                // Redirect to the action that creates the calendar event.
                return RedirectToAction("CreateEvent", "GoogleCalendar", new { exchangeId, otherUserId });
                //return RedirectToAction("Index", "GoogleCalendar");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google OAuth callback processing.");
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}