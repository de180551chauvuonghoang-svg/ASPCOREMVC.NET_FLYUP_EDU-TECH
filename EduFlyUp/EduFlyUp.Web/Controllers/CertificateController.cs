using System.Security.Claims;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers
{
    public class CertificateController : Controller
    {
        private readonly ICertificateService _certificateService;
        private readonly ICourseService _courseService;
        private readonly ILogger<CertificateController> _logger;

        public CertificateController(
            ICertificateService certificateService,
            ICourseService courseService,
            ILogger<CertificateController> logger)
        {
            _certificateService = certificateService;
            _courseService = courseService;
            _logger = logger;
        }

        public class FinalTestSubmissionRequest
        {
            public int CourseId { get; set; }
            public double Score { get; set; }
        }

        /// <summary>
        /// API ghi nhận hoàn thành Final Test và tự động cấp Certificate khi điểm >= 95%
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("/Certificate/CompleteFinalTest")]
        public async Task<IActionResult> CompleteFinalTest([FromBody] FinalTestSubmissionRequest request)
        {
            if (request == null || request.CourseId <= 0)
            {
                return BadRequest(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để nộp bài và nhận chứng chỉ." });
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _certificateService.CompleteFinalTestAndIssueAsync(userId, request.CourseId, request.Score, baseUrl);

            if (!result.Success || result.Certificate == null)
            {
                return Ok(new
                {
                    success = false,
                    isPassed = false,
                    score = request.Score,
                    message = result.Message
                });
            }

            return Ok(new
            {
                success = true,
                isPassed = true,
                score = result.Certificate.FinalScore,
                certificateId = result.Certificate.Id,
                certificateCode = result.Certificate.CertificateCode,
                viewUrl = Url.Action("ViewCertificate", "Certificate", new { id = result.Certificate.Id }),
                message = result.Message
            });
        }

        /// <summary>
        /// Xem chứng chỉ toàn màn hình (Chuẩn chứng chỉ quốc tế cao cấp, có nút in / tải PDF)
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("/Certificate/View/{id:int}")]
        public async Task<IActionResult> ViewCertificate(int id)
        {
            var certificate = await _certificateService.GetCertificateByIdAsync(id);
            if (certificate == null) return NotFound();

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdminOrInstructor = User.IsInRole("Admin") || User.IsInRole("Instructor");

            // Chỉ người sở hữu chứng chỉ hoặc Admin/Instructor mới được xem
            if (certificate.UserId != currentUserId && !isAdminOrInstructor)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập chứng chỉ này.";
                return RedirectToAction("MyCourses", "Courses");
            }

            return View("View", certificate);
        }

        /// <summary>
        /// Trang công khai xác minh tính hợp lệ của chứng chỉ cho nhà tuyển dụng
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [Route("/Certificate/Verify/{code}")]
        public async Task<IActionResult> Verify(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return NotFound();

            var certificate = await _certificateService.GetCertificateByCodeAsync(code);
            return View("Verify", certificate);
        }
    }
}
