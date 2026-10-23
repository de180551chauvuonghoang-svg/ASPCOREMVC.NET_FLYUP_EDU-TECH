using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Quiz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin,Instructor")]
public class QuizController : Controller
{
    private readonly IQuizService _quizService;
    private readonly ICourseService _courseService;
    private readonly ICourseAccessService _courseAccess;
    private readonly IQuestionService _questionService;

    public QuizController(
        IQuizService quizService,
        ICourseService courseService,
        ICourseAccessService courseAccess,
        IQuestionService questionService)
    {
        _quizService = quizService;
        _courseService = courseService;
        _courseAccess = courseAccess;
        _questionService = questionService;
    }

    // ==========================================
    // 1. DANH SÁCH ĐỀ THI CỦA KHÓA HỌC (UC-32)
    // ==========================================
    // GET: /Quiz?courseId=1
    [HttpGet]
    public async Task<IActionResult> Index(int courseId)
    {
        if (courseId <= 0)
        {
            TempData["ErrorMessage"] = "Mã khóa học không hợp lệ.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var course = await _courseService.GetCourseByIdAsync(courseId);
        if (course == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy khóa học.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        if (!_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền quản lý đề thi của khóa học này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var quizzes = (await _quizService.GetQuizzesByCourseIdAsync(courseId)).ToList();

        var totalQuizzes = quizzes.Count;
        var adaptiveQuizzes = quizzes.Count(q => q.IsCAT);
        var totalQuestions = quizzes.Sum(q => q.QuizQuestions?.Count ?? 0);
        var avgPassing = totalQuizzes > 0 ? quizzes.Average(q => q.PassingScorePercentage) : 0;

        var viewModel = new QuizIndexViewModel
        {
            Course = course,
            Quizzes = quizzes,
            TotalQuizzes = totalQuizzes,
            AdaptiveQuizzes = adaptiveQuizzes,
            TotalQuestionsAssigned = totalQuestions,
            AveragePassingScore = Math.Round(avgPassing, 1)
        };

        return View(viewModel);
    }

    // ==========================================
    // 2. TẠO ĐỀ THI MỚI (UC-32.1)
    // ==========================================
    // GET: /Quiz/Create?courseId=1
    [HttpGet]
    public async Task<IActionResult> Create(int courseId)
    {
        if (courseId <= 0)
        {
            TempData["ErrorMessage"] = "Mã khóa học không hợp lệ.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var course = await _courseService.GetCourseByIdAsync(courseId);
        if (course == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy khóa học.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        if (!_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền tạo đề thi cho khóa học này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var model = new QuizCreateEditViewModel
        {
            CourseId = courseId,
            CourseTitle = course.Title,
            DurationMinutes = 30,
            PassingScorePercentage = 70,
            IsCAT = false
        };

        return View(model);
    }

    // POST: /Quiz/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuizCreateEditViewModel model)
    {
        var course = await _courseService.GetCourseByIdAsync(model.CourseId);
        if (course == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy khóa học tương ứng.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        if (!_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền tạo đề thi cho khóa học này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        if (!ModelState.IsValid)
        {
            model.CourseTitle = course.Title;
            return View(model);
        }

        var quiz = new Quiz
        {
            CourseId = model.CourseId,
            Title = model.Title.Trim(),
            Description = model.Description?.Trim() ?? string.Empty,
            DurationMinutes = model.DurationMinutes,
            PassingScorePercentage = model.PassingScorePercentage,
            IsCAT = model.IsCAT
        };

        var result = await _quizService.CreateQuizAsync(quiz);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            model.CourseTitle = course.Title;
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
    }

    // ==========================================
    // 3. CHỈNH SỬA CẤU HÌNH ĐỀ THI (UC-32.2)
    // ==========================================
    // GET: /Quiz/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (id <= 0)
        {
            TempData["ErrorMessage"] = "Mã đề thi không hợp lệ.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var quiz = await _quizService.GetQuizByIdAsync(id);
        if (quiz == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đề thi cần chỉnh sửa.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var course = await _courseService.GetCourseByIdAsync(quiz.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa đề thi này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var model = new QuizCreateEditViewModel
        {
            Id = quiz.Id,
            CourseId = quiz.CourseId,
            CourseTitle = course.Title,
            Title = quiz.Title,
            Description = quiz.Description,
            DurationMinutes = quiz.DurationMinutes,
            PassingScorePercentage = quiz.PassingScorePercentage,
            IsCAT = quiz.IsCAT
        };

        return View(model);
    }

    // POST: /Quiz/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(QuizCreateEditViewModel model)
    {
        if (model.Id <= 0)
        {
            TempData["ErrorMessage"] = "Mã đề thi không hợp lệ.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var existingQuiz = await _quizService.GetQuizByIdAsync(model.Id);
        if (existingQuiz == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đề thi cần cập nhật.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var course = await _courseService.GetCourseByIdAsync(existingQuiz.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa đề thi này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        if (!ModelState.IsValid)
        {
            model.CourseTitle = course.Title;
            return View(model);
        }

        existingQuiz.Title = model.Title.Trim();
        existingQuiz.Description = model.Description?.Trim() ?? string.Empty;
        existingQuiz.DurationMinutes = model.DurationMinutes;
        existingQuiz.PassingScorePercentage = model.PassingScorePercentage;
        existingQuiz.IsCAT = model.IsCAT;

        var result = await _quizService.UpdateQuizAsync(existingQuiz);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            model.CourseTitle = course.Title;
            return View(model);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index), new { courseId = existingQuiz.CourseId });
    }

    // ==========================================
    // 4. XÓA ĐỀ THI (UC-32.3)
    // ==========================================
    // POST: /Quiz/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int? courseId)
    {
        if (id <= 0)
        {
            TempData["ErrorMessage"] = "Mã đề thi không hợp lệ.";
            return RedirectToAction(nameof(Index), new { courseId = courseId ?? 0 });
        }

        var quiz = await _quizService.GetQuizByIdAsync(id);
        if (quiz == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đề thi cần xóa.";
            return RedirectToAction(nameof(Index), new { courseId = courseId ?? 0 });
        }

        var targetCourseId = quiz.CourseId;
        var course = await _courseService.GetCourseByIdAsync(targetCourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xóa đề thi của khóa học này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        try
        {
            var success = await _quizService.DeleteQuizAsync(id);
            if (success)
            {
                TempData["SuccessMessage"] = $"Đã xóa đề thi '{quiz.Title}' thành công.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể xóa đề thi này.";
            }
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Không thể xóa đề thi này do đã có học viên làm bài hoặc có dữ liệu ràng buộc liên quan.";
        }

        return RedirectToAction(nameof(Index), new { courseId = targetCourseId });
    }

    // ==========================================
    // 5. GÁN CÂU HỎI VÀO ĐỀ THI (MANAGE QUESTIONS)
    // ==========================================
    // GET: /Quiz/ManageQuestions/5
    [HttpGet]
    public async Task<IActionResult> ManageQuestions(int id)
    {
        if (id <= 0)
        {
            TempData["ErrorMessage"] = "Mã đề thi không hợp lệ.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var quiz = await _quizService.GetQuizWithQuestionsAsync(id);
        if (quiz == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đề thi.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var course = await _courseService.GetCourseByIdAsync(quiz.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền quản lý câu hỏi của đề thi này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var allCourseQuestions = (await _questionService.GetQuestionsByCourseIdAsync(quiz.CourseId))
            .OrderBy(q => q.CreatedAt)
            .ThenBy(q => q.Id)
            .ToList();

        var assignedQuestions = quiz.QuizQuestions
            .OrderBy(qq => qq.Order)
            .Select(qq => qq.Question)
            .Where(q => q != null)
            .ToList();

        var selectedIds = quiz.QuizQuestions.Select(qq => qq.QuestionId).ToList();

        var viewModel = new QuizManageQuestionsViewModel
        {
            Quiz = quiz,
            Course = course,
            AssignedQuestions = assignedQuestions,
            AvailableQuestions = allCourseQuestions,
            SelectedQuestionIds = selectedIds
        };

        return View(viewModel);
    }

    // POST: /Quiz/ManageQuestions/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageQuestions(int id, List<int>? selectedQuestionIds)
    {
        if (id <= 0)
        {
            TempData["ErrorMessage"] = "Mã đề thi không hợp lệ.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var quiz = await _quizService.GetQuizByIdAsync(id);
        if (quiz == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đề thi.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var course = await _courseService.GetCourseByIdAsync(quiz.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền quản lý câu hỏi của đề thi này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        selectedQuestionIds ??= new List<int>();

        var result = await _quizService.AssignQuestionsToQuizAsync(id, selectedQuestionIds);
        if (result.Success)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật câu hỏi cho đề thi '{quiz.Title}' thành công ({selectedQuestionIds.Count} câu hỏi).";
            return RedirectToAction(nameof(Index), new { courseId = quiz.CourseId });
        }

        TempData["ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(ManageQuestions), new { id });
    }
}
