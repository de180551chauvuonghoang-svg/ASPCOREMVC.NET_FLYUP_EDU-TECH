using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Repositories
{
    public interface ICertificateRepository
    {
        Task<IEnumerable<Certificate>> GetByUserIdAsync(string userId);
        Task<Certificate?> GetByCodeAsync(string certificateCode);
        Task<Certificate?> GetByIdAsync(int id);
        Task<Certificate?> GetByCourseAndUserAsync(int courseId, string userId);
        Task<Certificate> AddOrUpdateAsync(Certificate certificate);
    }
}
