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
    {
        var lesson = await lessons.GetLessonByIdAsync(id);
        if (lesson is null) return NotFound();
        var course = await courses.GetCourseByIdAsync(lesson.CourseId);
        if (course is null || !access.CanManageCourse(course, User)) return NotFound();
        if (!string.IsNullOrWhiteSpace(lesson.MaterialUrl))
        {
            TempData["ErrorMessage"] = "Bài giảng đã có tài liệu. Chỉ tải lên khi chưa có tài liệu đính kèm.";
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
        lesson.MaterialUrl = $"/LessonMaterials/Download/{lesson.Id}/{fileName}";
        lesson.AttachmentName = originalName.Length <= 150 ? originalName : originalName[..150];
        try { await lessons.UpdateLessonAsync(lesson); }
        catch
        {
            try { storage.Delete(fileName); }
            catch (IOException ex) { logger.LogWarning(ex, "Không thể dọn file học liệu {FileName} sau lỗi lưu dữ liệu.", fileName); }
            throw;
        }
        TempData["SuccessMessage"] = "Đã tải lên tài liệu bài giảng thành công.";
        return Back(lesson.CourseId);
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
