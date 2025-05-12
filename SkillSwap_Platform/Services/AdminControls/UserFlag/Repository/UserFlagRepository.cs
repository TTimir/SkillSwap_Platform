using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.UserFlag.Repository
{
    public class UserFlagRepository : IUserFlagRepository
    {
        private readonly SkillSwapDbContext _ctx;
        public UserFlagRepository(SkillSwapDbContext ctx) => _ctx = ctx;

        public Task AddAsync(TblUserFlag flag) =>
            _ctx.TblUserFlags.AddAsync(flag).AsTask();

        public Task<TblUserFlag?> GetByIdAsync(int flagId) =>
            _ctx.TblUserFlags.FindAsync(flagId).AsTask();

        public Task<IEnumerable<TblUserFlag>> GetPendingAsync() =>
            _ctx.TblUserFlags
                .Include(f => f.FlaggedUser)
                .Include(f => f.FlaggedByUser)
                .Where(f => f.AdminAction == null)
                .OrderByDescending(f => f.FlaggedDate)
                .ToListAsync()
                .ContinueWith(t => (IEnumerable<TblUserFlag>)t.Result);

        public Task<IEnumerable<TblUserFlag>> GetByUserAsync(int flaggedUserId) =>
            _ctx.TblUserFlags
                .Where(f => f.FlaggedUserId == flaggedUserId)
                .ToListAsync()
                .ContinueWith(t => (IEnumerable<TblUserFlag>)t.Result);

        public Task UpdateAsync(TblUserFlag flag)
        {
            _ctx.TblUserFlags.Update(flag);
            return _ctx.SaveChangesAsync();
        }
    }
}