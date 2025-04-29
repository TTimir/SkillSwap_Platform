namespace SkillSwap_Platform.Services.AdminControls.Certificate
{
    public interface ICertificateReviewService
    {
        Task<PagedResult<CertificateReviewDto>> GetPendingCertificatesAsync(int page, int pageSize);
        Task<PagedResult<CertificateReviewDto>> GetApprovedCertificatesAsync(int page, int pageSize);
        Task<PagedResult<CertificateReviewDto>> GetRejectedCertificatesAsync(int page, int pageSize);

        Task<CertificateDetailDto> GetCertificateDetailAsync(int certificateId);
        Task<bool> ApproveCertificateAsync(int certificateId, int adminUserId);
        Task<bool> RejectCertificateAsync(int certificateId, int adminUserId, string reason);
    }
}
