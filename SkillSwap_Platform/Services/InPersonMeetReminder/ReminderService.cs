using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Services.Email;

namespace SkillSwap_Platform.Services.InPersonMeetReminder
{
    public class ReminderService : IReminderService
    {
        private readonly SkillSwapDbContext _db;
        private readonly IEmailService _emails;
        public ReminderService(SkillSwapDbContext db, IEmailService emails)
        {
            _db = db;
            _emails = emails;
        }

        public async Task SendEndMeetingReminder(int exchangeId)
        {
            var meeting = await _db.TblInPersonMeetings
              .Include(m => m.Exchange)
              .FirstOrDefaultAsync(m => m.ExchangeId == exchangeId);
            if (meeting == null) return;

            var exchange = meeting.Exchange;
            var ownerUserId = exchange.OfferOwnerId;
            var otherUserId = exchange.OtherUserId;

            var ownerUser = ownerUserId == null
                ? null
                : await _db.TblUsers.FindAsync(ownerUserId.Value);

            var otherUser = otherUserId == null
                ? null
                : await _db.TblUsers.FindAsync(otherUserId.Value);

            if (ownerUser == null || otherUser == null)
                return;

            var when = meeting.MeetingScheduledDateTime?.ToLocalTime().ToString("f");
            var location = meeting.MeetingLocation;
            var offer = exchange.Offer?.Title ?? "your exchange";

            var subject = "🌟 Friendly Reminder: Your in-person meet is coming up!";
            var body = $@"
                <p>Hi there!</p>
                <p>Just a quick reminder that your in-person meeting “<strong>{exchange.Offer?.Title}</strong>” is scheduled for <strong>{when}</strong> at <strong>{location}</strong>.</p>
                <p>We know schedules get busy—please don’t forget to open the website at the time of your meet to capture both your start and end proofs.</p>
                <p>Thanks for making Swapo great, and enjoy your session!</p>
                <p>— The Swapo Team</p>
            ";

            // send to both parties
            await _emails.SendEmailAsync(ownerUser.Email, subject, body, isBodyHtml: true);
            await _emails.SendEmailAsync(otherUser.Email, subject, body, isBodyHtml: true);
        }

        public async Task SendMissingEndProofReminder(int exchangeId, bool isFinal = false)
        {
            // load the in‐person meeting + exchange + users
            var meeting = await _db.TblInPersonMeetings
                .Include(m => m.Exchange)
                .FirstOrDefaultAsync(m => m.ExchangeId == exchangeId);
            if (meeting == null) return;

            // If they already submitted end proof, bail
            if (meeting.IsMeetingEnded) return;

            var exchange = meeting.Exchange;
            var ownerUser = await _db.TblUsers.FindAsync(exchange.OfferOwnerId);
            var otherUser = await _db.TblUsers.FindAsync(exchange.OtherUserId);
            if (ownerUser == null || otherUser == null) return;

            var when = meeting.MeetingScheduledDateTime?.ToLocalTime().ToString("f");
            var subject = isFinal
                ? "⚠️ Final Reminder: Please submit your end-meeting proof"
                : "🤗 Gentle Nudge: Don’t forget your end-meeting proof";

            var body = isFinal
              ? $@"<p>Hi there,</p>
           <p>This is our <strong>final</strong> reminder to submit your end-meeting proof for your session on <strong>{when}</strong>. 
           After this, your exchange will be marked incomplete.</p>
           <p>— The Swapo Team</p>"
              : $@"<p>Hello!</p>
           <p>We hope your in-person meeting on <strong>{when}</strong> went well. When you have a moment, 
           please submit</a> your end-meeting proof.</p>
           <p>Thanks!</p>
           <p>— The Swapo Team</p>";

            await _emails.SendEmailAsync(ownerUser.Email, subject, body, isBodyHtml: true);
            await _emails.SendEmailAsync(otherUser.Email, subject, body, isBodyHtml: true);
        }

    }
}
