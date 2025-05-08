using SkillSwap_Platform.Models.ViewModels.ReviewReplyVm;
using SkillSwap_Platform.Models;
using Microsoft.EntityFrameworkCore;

namespace SkillSwap_Platform.Services.ReviewReply
{
    public class ReviewService : IReviewService
    {
        private readonly SkillSwapDbContext _db;
        private readonly ILogger<ReviewService> _log;

        public ReviewService(SkillSwapDbContext db, ILogger<ReviewService> log)
        {
            _db = db;
            _log = log;
        }

        public async Task<ReviewListVm> GetReviewsAsync(int offerId)
        {
            var vm = new ReviewListVm { OfferId = offerId };

            var reviews = await _db.TblReviews
                .Where(r => r.OfferId == offerId)
                .OrderByDescending(r => r.CreatedDate)
                .Include(r => r.Reviewer)
                .Include(r => r.TblReviewReplies)
                    .ThenInclude(rep => rep.ReplierUser)
                .ToListAsync();

            vm.Reviews = reviews;

            return vm;
        }

        public async Task<ReviewReplyVm> AddReplyAsync(int reviewId, string text, int userId)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var reply = new TblReviewReply
                {
                    ReviewId = reviewId,
                    ReplierUserId = userId,
                    Comments = text,
                    CreatedDate = DateTime.UtcNow
                };

                _db.TblReviewReplies.Add(reply);
                await _db.SaveChangesAsync();

                // load back for VM
                var reloaded = await _db.TblReviewReplies
                    .Include(r => r.ReplierUser)
                    .FirstAsync(r => r.ReplyId == reply.ReplyId);

                await tx.CommitAsync();

                return new ReviewReplyVm
                {
                    ReplyId = reloaded.ReplyId,
                    RepliedBy = reloaded.ReplierUser.UserName,
                    CreatedAt = reloaded.CreatedDate,
                    Text = reloaded.Comments
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _log.LogError(ex, "Error adding reply to review {ReviewId} by user {UserId}", reviewId, userId);
                throw;
            }
        }
    }
}