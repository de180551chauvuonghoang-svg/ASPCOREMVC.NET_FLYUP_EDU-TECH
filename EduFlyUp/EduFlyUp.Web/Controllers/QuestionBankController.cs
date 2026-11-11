using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.QuestionBank;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin,Instructor")]
public class QuestionBankController : ManagedCourseControllerBase
{
    private readonly ICourseService _courseService;
    private readonly ICourseAccessService _courseAccess;
    private readonly IQuestionService _questionService;

    public QuestionBankController(
        ICourseService courseService,
        ICourseAccessService courseAccess,
        IQuestionService questionService)
    {
        _courseService = courseService;
        _courseAccess = courseAccess;
        _questionService = questionService;
    }

    // 1. DANH SÁCH CÂU HỎI THEO KHÓA HỌC
    // GET: /QuestionBank?courseId=1&difficulty=Easy&search=keyword
    [HttpGet]
    public async Task<IActionResult> Index(int courseId, string? difficulty, string? search)
    {
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, courseId, "Bạn không có quyền quản lý ngân hàng câu hỏi của khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

        QuestionDifficulty? parsedDifficulty = null;
        if (!string.IsNullOrWhiteSpace(difficulty) && Enum.TryParse<QuestionDifficulty>(difficulty, true, out var d))
        {
            parsedDifficulty = d;
        }

        var questions = (await _questionService.GetQuestionsByCourseIdAsync(courseId, parsedDifficulty, search))
            .OrderBy(q => q.CreatedAt)
            .ThenBy(q => q.Id)
            .ToList();

        var viewModel = new QuestionBankIndexViewModel
        {
            Course = course,
            Questions = questions,
            CurrentSearch = search,
            CurrentDifficulty = difficulty
        };

        return View(viewModel);
    }

    // 2. TẠO CÂU HỎI MỚI
    // GET: /QuestionBank/Create?courseId=1
    [HttpGet]
    public async Task<IActionResult> Create(int courseId)
    {
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, courseId, "Bạn không có quyền thêm câu hỏi cho khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

        // Tạo sẵn 4 phương án mẫu A, B, C, D
        var model = new QuestionCreateEditViewModel
        {
            CourseId = courseId,
            Course = course,
            Difficulty = QuestionDifficulty.Medium,
            Points = 1,
            CorrectOptionIndex = 0,
            Options = new List<QuestionOptionInputModel>
            {
                new() { Text = string.Empty, IsCorrect = true },
                new() { Text = string.Empty, IsCorrect = false },
                new() { Text = string.Empty, IsCorrect = false },
                new() { Text = string.Empty, IsCorrect = false }
            }
        };

        return View(model);
    }

    // POST: /QuestionBank/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuestionCreateEditViewModel model)
    {
        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, model.CourseId, "Bạn không có quyền thao tác trên khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

        model.Course = course;
        var entityOptions = ExtractAndValidateOptions(model, questionId: null);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var question = new Question
        {
            CourseId = model.CourseId,
            Content = model.Content.Trim(),
            Difficulty = model.Difficulty,
            Explanation = model.Explanation?.Trim() ?? string.Empty,
            Points = model.Points > 0 ? model.Points : 1,
            CreatedAt = DateTime.UtcNow
        };

        var (success, message, _) = await _questionService.CreateQuestionAsync(question, entityOptions);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã thêm câu hỏi trắc nghiệm mới thành công.";
        return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
    }

    // 3. CHỈNH SỬA CÂU HỎI
    // GET: /QuestionBank/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var question = await _questionService.GetQuestionByIdAsync(id);
        if (question == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy câu hỏi cần chỉnh sửa.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, question.CourseId, "Bạn không có quyền chỉnh sửa câu hỏi này.");
        if (errorResult != null || course == null) return errorResult!;

        var optionsList = question.Options.OrderBy(o => o.Id).ToList();
        var correctIdx = optionsList.FindIndex(o => o.IsCorrect);
        if (correctIdx < 0) correctIdx = 0;

        var model = new QuestionCreateEditViewModel
        {
            Id = question.Id,
            CourseId = question.CourseId,
            Course = course,
            Content = question.Content,
            Difficulty = question.Difficulty,
            Points = question.Points,
            Explanation = question.Explanation,
            CorrectOptionIndex = correctIdx,
            Options = optionsList.Select(o => new QuestionOptionInputModel
            {
                Id = o.Id,
                Text = o.Text,
                IsCorrect = o.IsCorrect
            }).ToList()
        };

        // Nếu câu hỏi có ít hơn 2 phương án, tự động bù thêm để hiển thị form chuẩn
        while (model.Options.Count < 2)
        {
            model.Options.Add(new QuestionOptionInputModel { Text = string.Empty, IsCorrect = false });
        }

        return View(model);
    }

    // POST: /QuestionBank/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(QuestionCreateEditViewModel model)
    {
        var existing = await _questionService.GetQuestionByIdAsync(model.Id);
        if (existing == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy câu hỏi.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, existing.CourseId, "Bạn không có quyền chỉnh sửa câu hỏi của khóa học này.");
        if (errorResult != null || course == null) return errorResult!;

        model.Course = course;
        model.CourseId = existing.CourseId;
        var entityOptions = ExtractAndValidateOptions(model, questionId: model.Id);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        existing.Content = model.Content.Trim();
        existing.Difficulty = model.Difficulty;
        existing.Points = model.Points > 0 ? model.Points : 1;
        existing.Explanation = model.Explanation?.Trim() ?? string.Empty;
        existing.UpdatedAt = DateTime.UtcNow;

        var (success, message) = await _questionService.UpdateQuestionAsync(existing, entityOptions);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật câu hỏi trắc nghiệm thành công.";
        return RedirectToAction(nameof(Index), new { courseId = existing.CourseId });
    }

    // 4. XÓA CÂU HỎI
    // POST: /QuestionBank/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int courseId)
    {
        var question = await _questionService.GetQuestionByIdAsync(id);
        if (question == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy câu hỏi cần xóa.";
            return RedirectToAction(nameof(Index), new { courseId });
        }

        var (course, errorResult) = await AuthorizeManagedCourseAsync(_courseService, _courseAccess, question.CourseId, "Bạn không có quyền xóa câu hỏi này.");
        if (errorResult != null || course == null) return errorResult!;

        try
        {
            var success = await _questionService.DeleteQuestionAsync(id);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? "Đã xóa câu hỏi khỏi ngân hàng câu hỏi thành công."
                : "Không thể xóa câu hỏi này.";
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Không thể xóa câu hỏi do có dữ liệu bài làm thi liên quan.";
        }

        return RedirectToAction(nameof(Index), new { courseId = question.CourseId });
    }

    private List<QuestionOption> ExtractAndValidateOptions(QuestionCreateEditViewModel model, int? questionId)
    {
        var validOptions = model.Options
            .Where(o => !string.IsNullOrWhiteSpace(o.Text))
            .ToList();

        if (validOptions.Count < 2)
        {
            ModelState.AddModelError(string.Empty, "Câu hỏi trắc nghiệm phải có ít nhất 2 phương án đáp án không được để trống.");
            return new List<QuestionOption>();
        }

        var chosenIndex = model.CorrectOptionIndex;
        var selectedOpt = chosenIndex >= 0 && chosenIndex < model.Options.Count
            ? model.Options[chosenIndex]
            : null;

        var entityOptions = validOptions.Select((o, idx) => new QuestionOption
        {
            Id = questionId.HasValue ? o.Id : 0,
            QuestionId = questionId ?? 0,
            Text = o.Text.Trim(),
            IsCorrect = selectedOpt != null ? o == selectedOpt : idx == 0
        }).ToList();

        if (!entityOptions.Any(o => o.IsCorrect))
        {
            entityOptions[0].IsCorrect = true;
        }

        return entityOptions;
    }
}
