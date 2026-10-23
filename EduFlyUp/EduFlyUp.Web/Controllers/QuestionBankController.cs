using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.QuestionBank;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin,Instructor")]
public class QuestionBankController : Controller
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

    // ==========================================
    // 1. DANH SÁCH CÂU HỎI THEO KHÓA HỌC (UC-31)
    // ==========================================
    // GET: /QuestionBank?courseId=1&difficulty=Easy&search=keyword
    [HttpGet]
    public async Task<IActionResult> Index(int courseId, string? difficulty, string? search)
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
            TempData["ErrorMessage"] = "Bạn không có quyền quản lý ngân hàng câu hỏi của khóa học này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

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

    // ==========================================
    // 2. TẠO CÂU HỎI MỚI (UC-31.1)
    // ==========================================
    // GET: /QuestionBank/Create?courseId=1
    [HttpGet]
    public async Task<IActionResult> Create(int courseId)
    {
        if (courseId <= 0)
        {
            TempData["ErrorMessage"] = "Mã khóa học không hợp lệ.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var course = await _courseService.GetCourseByIdAsync(courseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền thêm câu hỏi cho khóa học này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

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
        var course = await _courseService.GetCourseByIdAsync(model.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền thao tác trên khóa học này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        model.Course = course;

        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            TempData["ErrorMessage"] = string.IsNullOrEmpty(firstError) ? "Dữ liệu nhập không hợp lệ." : firstError;
            return View(model);
        }

        // Lọc các phương án hợp lệ và gán đúng IsCorrect theo CorrectOptionIndex
        var validOptions = model.Options
            .Where(o => !string.IsNullOrWhiteSpace(o.Text))
            .ToList();

        if (validOptions.Count < 2)
        {
            TempData["ErrorMessage"] = "Câu hỏi trắc nghiệm phải có ít nhất 2 phương án đáp án không được để trống.";
            return View(model);
        }

        if (model.CorrectOptionIndex < 0 || model.CorrectOptionIndex >= validOptions.Count)
        {
            model.CorrectOptionIndex = 0;
        }

        var entityOptions = validOptions.Select((o, idx) => new QuestionOption
        {
            Text = o.Text.Trim(),
            IsCorrect = (idx == model.CorrectOptionIndex)
        }).ToList();

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
            TempData["ErrorMessage"] = message;
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã thêm câu hỏi trắc nghiệm mới thành công.";
        return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
    }

    // ==========================================
    // 3. CHỈNH SỬA CÂU HỎI (UC-31.2)
    // ==========================================
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

        var course = await _courseService.GetCourseByIdAsync(question.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa câu hỏi này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

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

        var course = await _courseService.GetCourseByIdAsync(existing.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa câu hỏi của khóa học này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        model.Course = course;

        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            TempData["ErrorMessage"] = string.IsNullOrEmpty(firstError) ? "Dữ liệu nhập không hợp lệ." : firstError;
            return View(model);
        }

        var validOptions = model.Options
            .Where(o => !string.IsNullOrWhiteSpace(o.Text))
            .ToList();

        if (validOptions.Count < 2)
        {
            TempData["ErrorMessage"] = "Câu hỏi trắc nghiệm phải có ít nhất 2 phương án đáp án không được để trống.";
            return View(model);
        }

        if (model.CorrectOptionIndex < 0 || model.CorrectOptionIndex >= validOptions.Count)
        {
            model.CorrectOptionIndex = 0;
        }

        var entityOptions = validOptions.Select((o, idx) => new QuestionOption
        {
            Id = o.Id,
            QuestionId = model.Id,
            Text = o.Text.Trim(),
            IsCorrect = (idx == model.CorrectOptionIndex)
        }).ToList();

        existing.Content = model.Content.Trim();
        existing.Difficulty = model.Difficulty;
        existing.Points = model.Points > 0 ? model.Points : 1;
        existing.Explanation = model.Explanation?.Trim() ?? string.Empty;
        existing.UpdatedAt = DateTime.UtcNow;

        var (success, message) = await _questionService.UpdateQuestionAsync(existing, entityOptions);
        if (!success)
        {
            TempData["ErrorMessage"] = message;
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật câu hỏi trắc nghiệm thành công.";
        return RedirectToAction(nameof(Index), new { courseId = existing.CourseId });
    }

    // ==========================================
    // 4. XÓA CÂU HỎI (UC-31.3)
    // ==========================================
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

        var course = await _courseService.GetCourseByIdAsync(question.CourseId);
        if (course == null || !_courseAccess.CanManageCourse(course, User))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xóa câu hỏi này.";
            return RedirectToAction("Dashboard", "Instructor");
        }

        var success = await _questionService.DeleteQuestionAsync(id);
        if (success)
        {
            TempData["SuccessMessage"] = "Đã xóa câu hỏi khỏi ngân hàng câu hỏi thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = "Không thể xóa câu hỏi này.";
        }

        return RedirectToAction(nameof(Index), new { courseId = question.CourseId });
    }
}
