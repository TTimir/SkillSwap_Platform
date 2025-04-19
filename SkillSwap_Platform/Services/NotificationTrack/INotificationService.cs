using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.NotificationTrack
{
    public interface INotificationService
    {
        Task AddAsync(TblNotification notif);
        Task<IEnumerable<TblNotification>> GetRecentAsync(int userId, int take = 5);
        Task MarkAllReadAsync(int userId);
    }
}
