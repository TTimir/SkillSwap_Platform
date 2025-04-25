using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.Newsletter
{
    public class NewsletterService : INewsletterService
    {
        private readonly SkillSwapDbContext _db;

        public NewsletterService(SkillSwapDbContext db)
        {
            _db = db;
        }

        public async Task SubscribeAsync(string email)
        {
            // prevent duplicates
            if (!await _db.TblNewsletterSubscribers.AnyAsync(n => n.Email == email))
            {
                _db.TblNewsletterSubscribers.Add(new TblNewsletterSubscriber
                {
                    Email = email,
                    SubscribedAtUtc = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
        }
    }
}