using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;

namespace SkillSwap_Platform.Services.AdminControls.Message.SensitiveWord
{
    public class SensitiveWordService
     : ISensitiveWordService
    {
        private readonly SkillSwapDbContext _ctx;
        private readonly ILogger<SensitiveWordService> _log;

        public SensitiveWordService(
            SkillSwapDbContext ctx,
            ILogger<SensitiveWordService> log)
        {
            _ctx = ctx;
            _log = log;
        }

        public async Task<PagedResult<SensitiveWordVm>> GetPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;

            var query = _ctx.SensitiveWords
                            .AsNoTracking()
                            .OrderBy(sw => sw.Word);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(sw => new SensitiveWordVm
                {
                    Id = sw.Id,
                    Word = sw.Word,
                    WarningMessage = sw.WarningMessage
                })
                .ToListAsync();

            return new PagedResult<SensitiveWordVm>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<IReadOnlyList<SensitiveWordVm>> GetAllAsync()
        {
            try
            {
                return await _ctx.SensitiveWords
                    .AsNoTracking()
                    .OrderBy(sw => sw.Word)
                    .Select(sw => new SensitiveWordVm
                    {
                        Id = sw.Id,
                        Word = sw.Word,
                        WarningMessage = sw.WarningMessage
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error loading all sensitive words");
                throw;
            }
        }

        public async Task<SensitiveWordVm> GetByIdAsync(int id)
        {
            try
            {
                var sw = await _ctx.SensitiveWords.FindAsync(id)
                          ?? throw new KeyNotFoundException($"SensitiveWord {id} not found");
                return new SensitiveWordVm
                {
                    Id = sw.Id,
                    Word = sw.Word,
                    WarningMessage = sw.WarningMessage
                };
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error loading sensitive word {Id}", id);
                throw;
            }
        }

        public async Task CreateAsync(SensitiveWordVm vm)
        {
            try
            {
                var entity = new Models.SensitiveWord
                {
                    Word = vm.Word.Trim(),
                    WarningMessage = vm.WarningMessage.Trim()
                };
                _ctx.SensitiveWords.Add(entity);
                await _ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error creating sensitive word {@Vm}", vm);
                throw;
            }
        }

        public async Task UpdateAsync(SensitiveWordVm vm)
        {
            try
            {
                var entity = await _ctx.SensitiveWords.FindAsync(vm.Id)
                             ?? throw new KeyNotFoundException($"SensitiveWord {vm.Id} not found");
                entity.Word = vm.Word.Trim();
                entity.WarningMessage = vm.WarningMessage.Trim();
                _ctx.SensitiveWords.Update(entity);
                await _ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error updating sensitive word {@Vm}", vm);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                var entity = await _ctx.SensitiveWords.FindAsync(id)
                             ?? throw new KeyNotFoundException($"SensitiveWord {id} not found");
                _ctx.SensitiveWords.Remove(entity);
                await _ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error deleting sensitive word {Id}", id);
                throw;
            }
        }
    }
}
