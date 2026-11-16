using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories
{
    public class CertificateRepository : ICertificateRepository
    {
        public async Task<IEnumerable<Certificate>> GetByUserIdAsync(string userId)
            => await CertificateDAO.Instance.GetByUserIdAsync(userId);

        public async Task<Certificate?> GetByCodeAsync(string certificateCode)
            => await CertificateDAO.Instance.GetByCodeAsync(certificateCode);

        public async Task<Certificate?> GetByIdAsync(int id)
            => await CertificateDAO.Instance.GetByIdAsync(id);

        public async Task<Certificate?> GetByCourseAndUserAsync(int courseId, string userId)
            => await CertificateDAO.Instance.GetByCourseAndUserAsync(courseId, userId);

        public async Task<Certificate> AddOrUpdateAsync(Certificate certificate)
            => await CertificateDAO.Instance.AddOrUpdateAsync(certificate);
    }
}
