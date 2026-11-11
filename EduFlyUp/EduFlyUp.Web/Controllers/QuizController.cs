using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Quiz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin,Instructor")]
public class QuizController : ManagedCourseControllerBase
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

    // 1. DANH SÁCH ĐỀ THI CỦA KHÓA HỌC
    // GET: /Quiz?courseId=1
    [HttpGet]
    public async Task<IActionResult> Index(int courseId)
    {
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, courseId, "Bạn không có quyền quản lý đề thi của khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

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

    // 2. TẠO ĐỀ THI MỚI
    // GET: /Quiz/Create?courseId=1
    [HttpGet]
    public async Task<IActionResult> Create(int courseId)
    {
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, courseId, "Bạn không có quyền tạo đề thi cho khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

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
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, model.CourseId, "Bạn không có quyền tạo đề thi cho khóa học này.", notFoundMessage: "Không tìm thấy khóa học tương ứng.");
        if (errorResult != null || course == null) return errorResult!;

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

    // 3. CHỈNH SỬA CẤU HÌNH ĐỀ THI
    // GET: /Quiz/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (quiz, course, errorResult) = await AuthorizeManagedQuizAsync(id, "Không tìm thấy đề thi cần chỉnh sửa.", "Bạn không có quyền chỉnh sửa đề thi này.");
        if (errorResult != null || quiz == null || course == null) return errorResult!;

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
        var (existingQuiz, course, errorResult) = await AuthorizeManagedQuizAsync(model.Id, "Không tìm thấy đề thi cần cập nhật.", "Bạn không có quyền chỉnh sửa đề thi này.");
        if (errorResult != null || existingQuiz == null || course == null) return errorResult!;

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

    // 4. XÓA ĐỀ THI
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
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, targetCourseId, "Bạn không có quyền xóa đề thi của khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

        try
        {
            var success = await _quizService.DeleteQuizAsync(id);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? $"Đã xóa đề thi '{quiz.Title}' thành công."
                : "Không thể xóa đề thi này.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Không thể xóa đề thi này do đã có học viên làm bài hoặc có dữ liệu ràng buộc liên quan.";
        }

        return RedirectToAction(nameof(Index), new { courseId = targetCourseId });
    }

    // 5. GÁN CÂU HỎI VÀO ĐỀ THI (MANAGE QUESTIONS)
    // GET: /Quiz/ManageQuestions/5
    [HttpGet]
    public async Task<IActionResult> ManageQuestions(int id)
    {
        var (quiz, course, errorResult) = await AuthorizeManagedQuizAsync(id, "Không tìm thấy đề thi.", "Bạn không có quyền quản lý câu hỏi của đề thi này.", includeQuestions: true);
        if (errorResult != null || quiz == null || course == null) return errorResult!;

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
        var (quiz, course, errorResult) = await AuthorizeManagedQuizAsync(id, "Không tìm thấy đề thi.", "Bạn không có quyền quản lý câu hỏi của đề thi này.");
        if (errorResult != null || quiz == null || course == null) return errorResult!;

        var distinctQuestionIds = selectedQuestionIds?
            .Where(qId => qId > 0)
            .Distinct()
            .ToList() ?? new List<int>();

        var result = await _quizService.AssignQuestionsToQuizAsync(id, distinctQuestionIds);
        if (result.Success)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật câu hỏi cho đề thi '{quiz.Title}' thành công ({distinctQuestionIds.Count} câu hỏi).";
            return RedirectToAction(nameof(Index), new { courseId = quiz.CourseId });
        }

        TempData["ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(ManageQuestions), new { id });
    }

    private async Task<(Quiz? Quiz, Course? Course, IActionResult? ErrorResult)> AuthorizeManagedQuizAsync(
        int quizId,
        string notFoundQuizMessage,
        string unauthorizedMessage,
        bool includeQuestions = false)
    {
        if (quizId <= 0)
        {
            TempData["ErrorMessage"] = "Mã đề thi không hợp lệ.";
            return (null, null, RedirectToAction("Dashboard", "Instructor"));
        }

        var quiz = includeQuestions
            ? await _quizService.GetQuizWithQuestionsAsync(quizId)
            : await _quizService.GetQuizByIdAsync(quizId);

        if (quiz == null)
        {
            TempData["ErrorMessage"] = notFoundQuizMessage;
            return (null, null, RedirectToAction("Dashboard", "Instructor"));
        }

        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, quiz.CourseId, unauthorizedMessage);
        if (errorResult != null || course == null) return (null, null, errorResult);

        return (quiz, course, null);
    }
}
