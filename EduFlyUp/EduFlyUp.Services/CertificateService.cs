using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using Microsoft.AspNetCore.Identity;

namespace EduFlyUp.Services
{
    public class CertificateService : ICertificateService
    {
        private readonly ICertificateRepository _certificateRepo;
        private readonly ICourseRepository _courseRepo;
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly UserManager<ApplicationUser> _userManager;

        public CertificateService(
            ICertificateRepository certificateRepo,
            ICourseRepository courseRepo,
            IEnrollmentRepository enrollmentRepo,
            UserManager<ApplicationUser> userManager)
        {
            _certificateRepo = certificateRepo;
            _courseRepo = courseRepo;
            _enrollmentRepo = enrollmentRepo;
            _userManager = userManager;
        }

        public async Task<IEnumerable<Certificate>> GetCertificatesByUserIdAsync(string userId)
            => await _certificateRepo.GetByUserIdAsync(userId);

        public async Task<Certificate?> GetCertificateByCodeAsync(string code)
            => await _certificateRepo.GetByCodeAsync(code);

        public async Task<Certificate?> GetCertificateByIdAsync(int id)
            => await _certificateRepo.GetByIdAsync(id);

        public async Task<Certificate?> GetCertificateByCourseAndUserAsync(int courseId, string userId)
            => await _certificateRepo.GetByCourseAndUserAsync(courseId, userId);

        public async Task<(bool Success, string Message, Certificate? Certificate)> CompleteFinalTestAndIssueAsync(
            string userId, int courseId, double score, string? requestBaseUrl = null)
        {
            if (score < 95.0)
            {
                return (false, $"Bạn đạt {score}% điểm. Cần đạt tối thiểu 95% để hoàn thành khóa học và nhận Chứng chỉ tốt nghiệp.", null);
            }

            var course = await _courseRepo.GetByIdAsync(courseId);
            if (course == null) return (false, "Không tìm thấy khóa học.", null);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return (false, "Không tìm thấy thông tin học viên.", null);

            var studentName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.UserName ?? user.Email ?? "Học viên EduFlyUp";

            // 1. Cập nhật trạng thái Enrollment thành Completed (đã hoàn thành khóa học)
            try
            {
                await _enrollmentRepo.CompleteEnrollmentAsync(userId, courseId);
            }
            catch
            {
                // Bỏ qua lỗi cập nhật enrollment nếu đã hoàn thành
            }

            // 2. Quy tắc: Mỗi học viên chỉ được cấp Certificate DUY NHẤT 1 LẦN ĐẦU TIÊN khi hoàn thành
            var existingCert = await _certificateRepo.GetByCourseAndUserAsync(courseId, userId);
            if (existingCert != null)
            {
                // Bảo lưu chứng chỉ lần đầu tiên, không tạo mới và không ghi đè ngày cấp ban đầu
                return (true, $"Bạn đã nhận Chứng chỉ tốt nghiệp cho khóa học này từ lần hoàn thành đầu tiên (Ngày cấp: {existingCert.IssuedAt:dd/MM/yyyy}). Khóa học đã được lưu vào mục 'Khóa học đã hoàn thành' và bạn có thể học lại bất cứ lúc nào!", existingCert);
            }

            // 3. Tạo chứng chỉ mới cho lần đầu hoàn thành
            var randomCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var certCode = $"CERT-{DateTime.UtcNow.Year}-{randomCode}";
            var verifyUrl = !string.IsNullOrWhiteSpace(requestBaseUrl)
                ? $"{requestBaseUrl.TrimEnd('/')}/Certificate/Verify/{certCode}"
                : $"/Certificate/Verify/{certCode}";

            var certificate = new Certificate
            {
                CertificateCode = certCode,
                UserId = userId,
                CourseId = courseId,
                StudentName = studentName,
                CourseTitle = course.Title,
                FinalScore = score,
                IssuedAt = DateTime.UtcNow,
                VerificationUrl = verifyUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var savedCert = await _certificateRepo.AddOrUpdateAsync(certificate);
            return (true, "Chúc mừng bạn đã hoàn thành xuất sắc khóa học và nhận Chứng chỉ tốt nghiệp EduFlyUp!", savedCert);
        }
    }
}
