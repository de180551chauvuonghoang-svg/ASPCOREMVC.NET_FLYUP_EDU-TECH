using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Services
{
    public interface ICertificateService
    {
        Task<IEnumerable<Certificate>> GetCertificatesByUserIdAsync(string userId);
        Task<Certificate?> GetCertificateByCodeAsync(string code);
        Task<Certificate?> GetCertificateByIdAsync(int id);
        Task<Certificate?> GetCertificateByCourseAndUserAsync(int courseId, string userId);
        Task<(bool Success, string Message, Certificate? Certificate)> CompleteFinalTestAndIssueAsync(string userId, int courseId, double score, string? requestBaseUrl = null);
    }
}
