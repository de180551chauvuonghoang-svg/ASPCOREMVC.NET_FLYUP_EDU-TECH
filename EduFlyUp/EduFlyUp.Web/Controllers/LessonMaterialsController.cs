using EduFlyUp.Services;
using EduFlyUp.Web.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize]
public sealed class LessonMaterialsController(
    ILessonService lessons, ICourseService courses, ICourseAccessService access,
    ILessonMaterialStorage storage, ILogger<LessonMaterialsController> logger) : Controller
{
    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(LessonMaterialStorage.MaxFileSize + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = LessonMaterialStorage.MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> Upload(int id, IFormFile? materialFile, CancellationToken cancellationToken)
        => await SaveMaterial(id, materialFile, "", cancellationToken);

    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(LessonMaterialStorage.MaxFileSize + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = LessonMaterialStorage.MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> Replace(int id, IFormFile? materialFile, string? expectedUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedUrl)) return BadRequest();
        return await SaveMaterial(id, materialFile, expectedUrl, cancellationToken);
    }

    private async Task<IActionResult> SaveMaterial(int id, IFormFile? materialFile, string expectedUrl, CancellationToken cancellationToken)
    {
        var lesson = await lessons.GetLessonByIdAsync(id);
        if (lesson is null) return NotFound();
        var course = await courses.GetCourseByIdAsync(lesson.CourseId);
        if (course is null || !access.CanManageCourse(course, User)) return NotFound();
        if (!string.Equals(lesson.MaterialUrl, expectedUrl, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "Tài liệu đã thay đổi. Vui lòng tải lại trang trước khi thực hiện.";
            return Back(lesson.CourseId);
        }
        if (!ModelState.IsValid || materialFile is null)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn tài liệu PDF, Word hoặc PowerPoint (tối đa 10 MB).";
            return Back(lesson.CourseId);
        }
        string fileName;
        try { fileName = await storage.SaveAsync(materialFile, cancellationToken); }
        catch (InvalidDataException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return Back(lesson.CourseId);
        }
        var originalName = Path.GetFileName(materialFile.FileName.Replace('\\', '/'));
        bool saved;
        try
        {
            saved = await lessons.TryUpdateMaterialAsync(lesson.Id, expectedUrl,
                $"/LessonMaterials/Download/{lesson.Id}/{fileName}",
                originalName.Length <= 150 ? originalName : originalName[..150]);
        }
        catch
        {
            DeleteFile(fileName);
            throw;
        }
        if (!saved)
        {
            DeleteFile(fileName);
            TempData["ErrorMessage"] = "Tài liệu đã thay đổi. Vui lòng tải lại trang trước khi thực hiện.";
            return Back(lesson.CourseId);
        }
        DeletePreviousFile(id, expectedUrl);
        TempData["SuccessMessage"] = expectedUrl.Length == 0
            ? "Đã tải lên tài liệu bài giảng thành công."
            : "Đã thay thế tài liệu bài giảng thành công.";
        return Back(lesson.CourseId);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, string? expectedUrl)
    {
        var lesson = await lessons.GetLessonByIdAsync(id);
        if (lesson is null) return NotFound();
        var course = await courses.GetCourseByIdAsync(lesson.CourseId);
        if (course is null || !access.CanManageCourse(course, User)) return NotFound();
        if (string.IsNullOrWhiteSpace(expectedUrl)) return BadRequest();
        if (!string.Equals(lesson.MaterialUrl, expectedUrl, StringComparison.Ordinal) ||
            !await lessons.TryUpdateMaterialAsync(id, expectedUrl, "", ""))
        {
            TempData["ErrorMessage"] = "Tài liệu đã thay đổi. Vui lòng tải lại trang trước khi thực hiện.";
            return Back(lesson.CourseId);
        }
        // Revoke the download URL before deleting the physical file.
        DeletePreviousFile(id, expectedUrl);
        TempData["SuccessMessage"] = "Đã xóa tài liệu đính kèm. Nội dung bài giảng được giữ nguyên.";
        return Back(lesson.CourseId);
    }

    private void DeletePreviousFile(int id, string url)
    {
        var prefix = $"/LessonMaterials/Download/{id}/";
        if (url.StartsWith(prefix, StringComparison.Ordinal)) DeleteFile(url[prefix.Length..]);
    }

    private void DeleteFile(string fileName)
    {
        try { storage.Delete(fileName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "Không thể dọn file học liệu {FileName}; cần kiểm tra thư mục lưu trữ.", fileName);
        }
    }

    [HttpGet("/LessonMaterials/Download/{id:int}/{fileName}")]
    public async Task<IActionResult> Download(int id, string fileName)
    {
        var lesson = await lessons.GetLessonByIdAsync(id);
        if (lesson is null || lesson.MaterialUrl != $"/LessonMaterials/Download/{id}/{fileName}") return NotFound();
        var course = await courses.GetCourseByIdAsync(lesson.CourseId);
        if (course is null || !await access.CanLearnCourseAsync(course, User)) return NotFound();
        var stream = storage.OpenRead(fileName);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, "application/octet-stream", lesson.AttachmentName);
    }

    private IActionResult Back(int courseId) => RedirectToAction("Curriculum", "Instructor", new { id = courseId });
}
