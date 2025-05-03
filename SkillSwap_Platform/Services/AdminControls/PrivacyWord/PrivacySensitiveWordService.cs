using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.PrivacyWord
{
    public class PrivacySensitiveWordService : IPrivacySensitiveWordService
    {
        private readonly SkillSwapDbContext _ctx;
        private readonly ILogger<PrivacySensitiveWordService> _log;

        public PrivacySensitiveWordService(
            SkillSwapDbContext ctx,
            ILogger<PrivacySensitiveWordService> log)
        {
            _ctx = ctx;
            _log = log;
        }

        public async Task<PagedResult<PrivacySensitiveWordVm>> GetPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;

            var query = _ctx.PrivacySensitiveWords
                            .AsNoTracking()
                            .OrderBy(x => x.Word);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(sw => new PrivacySensitiveWordVm
                {
                    Id = sw.Id,
                    Word = sw.Word
                })
                .ToListAsync();

            return new PagedResult<PrivacySensitiveWordVm>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PrivacySensitiveWordVm> GetByIdAsync(int id)
        {
            try
            {
                var sw = await _ctx.PrivacySensitiveWords.FindAsync(id)
                         ?? throw new KeyNotFoundException($"PrivacySensitiveWord {id} not found");

                return new PrivacySensitiveWordVm
                {
                    Id = sw.Id,
                    Word = sw.Word
                };
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error loading privacy‐sensitive word {Id}", id);
                throw;
            }
        }

        public async Task CreateAsync(PrivacySensitiveWordVm vm)
        {
            try
            {
                var entity = new PrivacySensitiveWord
                {
                    Word = vm.Word.Trim()
                };
                _ctx.PrivacySensitiveWords.Add(entity);
                await _ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error creating privacy‐sensitive word {@Vm}", vm);
                throw;
            }
        }

        public async Task UpdateAsync(PrivacySensitiveWordVm vm)
        {
            try
            {
                var entity = await _ctx.PrivacySensitiveWords.FindAsync(vm.Id)
                             ?? throw new KeyNotFoundException($"PrivacySensitiveWord {vm.Id} not found");

                entity.Word = vm.Word.Trim();
                _ctx.PrivacySensitiveWords.Update(entity);
                await _ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error updating privacy‐sensitive word {@Vm}", vm);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                var entity = await _ctx.PrivacySensitiveWords.FindAsync(id)
                             ?? throw new KeyNotFoundException($"PrivacySensitiveWord {id} not found");

                _ctx.PrivacySensitiveWords.Remove(entity);
                await _ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error deleting privacy‐sensitive word {Id}", id);
                throw;
            }
        }
    }
}