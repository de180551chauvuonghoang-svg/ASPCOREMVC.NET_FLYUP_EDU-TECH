using EduFlyUp.Services;
using EduFlyUp.Web.Models.Lessons;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers
{
    public class LessonsController : Controller
    {
        private readonly ILessonService _lessonService;
        private readonly ICourseService _courseService;
        private readonly ICourseAccessService _courseAccessService;
        private readonly IQuizService _quizService;
        private readonly ILogger<LessonsController> _logger;

        public LessonsController(
            ILessonService lessonService,
            ICourseService courseService,
            ICourseAccessService courseAccessService,
            IQuizService quizService,
            ILogger<LessonsController> logger)
        {
            _lessonService = lessonService;
            _courseService = courseService;
            _courseAccessService = courseAccessService;
            _quizService = quizService;
            _logger = logger;
        }

        /// <summary>
        /// Mở bài học đầu tiên của khóa học (dành cho nút "Vào học ngay" ở cấp Khóa học)
        /// </summary>
        [HttpGet("/Lessons/Course/{id:int}")]
        public async Task<IActionResult> Course(int id)
        {
            var course = await _courseService.GetCourseByIdAsync(id);
            if (course == null) return NotFound();

            var lessons = (await _lessonService.GetLessonsByCourseIdAsync(id))
                .OrderBy(l => l.Order)
                .ToList();

            if (!lessons.Any())
            {
                TempData["ErrorMessage"] = $"Khóa học '{course.Title}' hiện đang được cập nhật nội dung bài giảng. Vui lòng quay lại sau!";
                return RedirectToAction("Details", "Courses", new { id });
            }

            return RedirectToAction(nameof(Learn), new { id = lessons[0].Id });
        }

        /// <summary>
        /// Trang học tập chuyên sâu kiểu Coursera / Udemy
        /// </summary>
        [HttpGet("/Lessons/Learn/{id:int}")]
        public async Task<IActionResult> Learn(int id)
        {
            var lesson = await _lessonService.GetLessonByIdAsync(id);
            if (lesson == null) return NotFound();

            var course = await _courseService.GetCourseByIdAsync(lesson.CourseId);
            if (course == null) return NotFound();

            var canLearn = await _courseAccessService.CanLearnCourseAsync(course, User);
            var canManage = _courseAccessService.CanManageCourse(course, User);

            // Kiểm tra phân quyền truy cập
            if (!canLearn && !canManage && !lesson.IsPreview)
            {
                if (User.Identity?.IsAuthenticated != true)
                {
                    var returnUrl = Url.Action(nameof(Learn), "Lessons", new { id });
                    return RedirectToAction("Login", "Account", new { returnUrl });
                }

                TempData["ErrorMessage"] = $"Bạn cần hoàn tất thanh toán khóa học '{course.Title}' để xem toàn bộ bài giảng.";
                return RedirectToAction("Checkout", "Payment", new { courseId = course.Id });
            }

            // Lấy danh sách toàn bộ bài học thuộc khóa học
            var allLessons = (await _lessonService.GetLessonsByCourseIdAsync(course.Id))
                .OrderBy(l => l.Order)
                .ToList();

            var currentIndex = allLessons.FindIndex(l => l.Id == lesson.Id);
            var prevLesson = currentIndex > 0 ? allLessons[currentIndex - 1] : null;
            var nextLesson = (currentIndex >= 0 && currentIndex < allLessons.Count - 1) ? allLessons[currentIndex + 1] : null;

            // Nhóm theo từng Chương (Section)
            var sectionGroups = allLessons
                .GroupBy(l => string.IsNullOrWhiteSpace(l.SectionName) ? "Chương 1: Tổng quan" : l.SectionName.Trim())
                .Select(g => new LessonSectionGroup
                {
                    SectionName = g.Key,
                    Lessons = g.OrderBy(l => l.Order).ToList()
                })
                .ToList();

            // Lấy đề thi trắc nghiệm (Quiz) của khóa học nếu có
            EduFlyUp.BusinessObjects.Quiz? courseQuiz = null;
            try
            {
                var quizzes = await _quizService.GetQuizzesByCourseIdAsync(course.Id);
                var firstQuiz = quizzes.FirstOrDefault();
                if (firstQuiz != null)
                {
                    courseQuiz = await _quizService.GetQuizWithQuestionsAsync(firstQuiz.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải đề thi trắc nghiệm cho khóa học {CourseId}", course.Id);
            }

            var viewModel = new LearnLessonViewModel
            {
                CurrentLesson = lesson,
                Course = course,
                AllLessons = allLessons,
                Sections = sectionGroups,
                PreviousLesson = prevLesson,
                NextLesson = nextLesson,
                IsEnrolled = canLearn,
                CanManageCourse = canManage,
                CourseQuiz = courseQuiz
            };

            return View("Learn", viewModel);
        }

        /// <summary>
        /// Alias tương thích cho liên kết /Lessons/Details/{id}
        /// </summary>
        [HttpGet("/Lessons/Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            return await Learn(id);
        }
    }
}
