using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Options;
using SkillSwap_Platform.Models;
using Google.Apis.Util;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Google.Apis.Util.Store;
using System.Net;
using Newtonsoft.Json;
using System.Text.Json.Nodes;

namespace SkillSwap_Platform.Services
{
    public class GoogleCalendarService
    {
        private static readonly string[] Scopes = { CalendarService.Scope.Calendar };
        private readonly SkillSwapDbContext _db;
        private readonly IConfiguration _cfg;
        private readonly ILogger<GoogleCalendarService> _log;

        public GoogleCalendarService(
            SkillSwapDbContext db,
            IConfiguration cfg,
            ILogger<GoogleCalendarService> log)
        {
            _db = db; _cfg = cfg; _log = log;
        }

        public async Task<string> CreateSwapEventAsync(int userId, int exchangeId, DateTime startUtc, DateTime endUtc)
        {
            // 1) Load token
            var token = await _db.UserGoogleTokens
                                 .SingleOrDefaultAsync(t => t.UserId == userId);
            if (token == null)
                throw new InvalidOperationException("No Google token");

            // 2) Build the JSON payload
            var evt = new
            {
                summary = $"Swapo Exchange #{exchangeId}",
                description = "Your upcoming Swapo session",
                start = new { dateTime = startUtc.ToString("o"), timeZone = "UTC" },
                end = new { dateTime = endUtc.ToString("o"), timeZone = "UTC" }
            };
            string payloadJson = System.Text.Json.JsonSerializer.Serialize(evt);

            // 3) Call Google Calendar REST API
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.AccessToken);

            using var content = new StringContent(payloadJson, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var resp = await http.PostAsync(
                "https://www.googleapis.com/calendar/v3/calendars/primary/events",
                content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _log.LogError("Calendar API error: {Status} {Body}", resp.StatusCode, body);
                throw new InvalidOperationException("Calendar API error");
            }

            // 4) Parse the response
            var responseBody = await resp.Content.ReadAsStringAsync();
            var doc = JsonNode.Parse(responseBody)?.AsObject();
            if (doc != null && doc.TryGetPropertyValue("htmlLink", out var linkNode))
                return linkNode?.GetValue<string>();

            throw new InvalidOperationException("Missing htmlLink in calendar response");
        }

        public async Task<IList<Event>> GetUpcomingEventsAsync(int userId, CancellationToken ct = default)
        {
            var tokenEntity = await _db.UserGoogleTokens.SingleOrDefaultAsync(t => t.UserId == userId, ct);
            if (tokenEntity == null)
                throw new InvalidOperationException("Google calendar not connected.");

            if (tokenEntity.ExpiresAt <= DateTime.UtcNow)
            {
                tokenEntity = await RefreshAccessTokenAsync(tokenEntity, ct);
                if (tokenEntity == null)
                    throw new InvalidOperationException("Google token expired; re-authorization required.");
            }

            var svc = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = GoogleCredential.FromAccessToken(tokenEntity.AccessToken),
                ApplicationName = "Swapo",
            });

            var calList = await svc.CalendarList.List().ExecuteAsync();
            var allEvents = new List<Event>();
            foreach (var c in calList.Items)
            {
                var req = svc.Events.List(c.Id);
                req.TimeMin = DateTime.UtcNow.AddDays(-2);
                req.SingleEvents = true;
                req.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
                var resp = await req.ExecuteAsync();
                allEvents.AddRange(resp.Items);
            }

            return allEvents;
        }

        private async Task<UserGoogleToken> RefreshAccessTokenAsync(UserGoogleToken token, CancellationToken cancellationToken)
        {
            var clientId = _cfg["Authentication:Google:ClientId"];
            var clientSecret = _cfg["Authentication:Google:ClientSecret"];
            var url = "https://oauth2.googleapis.com/token";

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = token.RefreshToken,
                ["grant_type"] = "refresh_token"
            });

            using var http = new HttpClient();
            var res = await http.PostAsync(url, body, cancellationToken);

            // if Google says “401 Unauthorized” => refresh token revoked
            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                _db.UserGoogleTokens.Remove(token);
                await _db.SaveChangesAsync(cancellationToken);
                return null;
            }

            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            var tr = JsonConvert.DeserializeObject<TokenResponse>(json);

            token.AccessToken = tr.AccessToken;
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(tr.ExpiresInSeconds ?? 3600);
            _db.UserGoogleTokens.Update(token);
            await _db.SaveChangesAsync(cancellationToken);

            return token;
        }
    }
}