namespace SkillSwap_Platform.Services.AdminControls.PlatformMetrics
{
    public interface IPerformanceService
    {
        Task<PlatformMetricsDto> GetCurrentMetricsAsync();
    }
}
