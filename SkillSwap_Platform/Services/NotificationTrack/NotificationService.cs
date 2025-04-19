using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.NotificationTrack
{
    public class NotificationService : INotificationService
    {
        private readonly SkillSwapDbContext _db;
        public NotificationService(SkillSwapDbContext db) => _db = db;

        public async Task AddAsync(TblNotification notif)
        {
            try
            {
                notif.CreatedAt = DateTime.UtcNow;
                _db.TblNotifications.Add(notif);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // TODO: inject ILogger<NotificationService> and log
                throw;
            }
        }

        public async Task<IEnumerable<TblNotification>> GetRecentAsync(int userId, int take = 5)
        {
            return await _db.TblNotifications
                            .Where(n => n.UserId == userId)
                            .OrderByDescending(n => n.CreatedAt)
                            .Take(take)
                            .ToListAsync();
        }

        public async Task MarkAllReadAsync(int userId)
        {
            var unread = await _db.TblNotifications
                                  .Where(n => n.UserId == userId && !n.IsRead)
                                  .ToListAsync();
            unread.ForEach(n => n.IsRead = true);
            await _db.SaveChangesAsync();
        }
    }
}