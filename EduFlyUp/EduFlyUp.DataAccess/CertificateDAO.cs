using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess
{
    public class CertificateDAO : SingletonBase<CertificateDAO>
    {
        public async Task<IEnumerable<Certificate>> GetByUserIdAsync(string userId)
        {
            using var context = new AppDbContext();
            return await context.Certificates
                .Include(c => c.Course)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.IssuedAt)
                .ToListAsync();
        }

        public async Task<Certificate?> GetByCodeAsync(string certificateCode)
        {
            using var context = new AppDbContext();
            return await context.Certificates
                .Include(c => c.Course)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CertificateCode == certificateCode);
        }

        public async Task<Certificate?> GetByIdAsync(int id)
        {
            using var context = new AppDbContext();
            return await context.Certificates
                .Include(c => c.Course)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Certificate?> GetByCourseAndUserAsync(int courseId, string userId)
        {
            using var context = new AppDbContext();
            return await context.Certificates
                .FirstOrDefaultAsync(c => c.CourseId == courseId && c.UserId == userId);
        }

        public async Task<Certificate> AddOrUpdateAsync(Certificate certificate)
        {
            using var context = new AppDbContext();
            var existing = await context.Certificates
                .FirstOrDefaultAsync(c => c.CourseId == certificate.CourseId && c.UserId == certificate.UserId);

            if (existing != null)
            {
                if (certificate.FinalScore > existing.FinalScore)
                {
                    existing.FinalScore = certificate.FinalScore;
                    existing.IssuedAt = DateTime.UtcNow;
                }
                await context.SaveChangesAsync();
                return existing;
            }

            await context.Certificates.AddAsync(certificate);
            await context.SaveChangesAsync();
            return certificate;
        }
    }
}
