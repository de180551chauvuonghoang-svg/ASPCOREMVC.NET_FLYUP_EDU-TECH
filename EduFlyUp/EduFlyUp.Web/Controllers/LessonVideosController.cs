using EduFlyUp.Services;
using EduFlyUp.Web.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize]
public sealed class LessonVideosController(ILessonService lessons, ICourseService courses,
    ICourseAccessService access, ILessonVideoStorage storage, ILogger<LessonVideosController> logger) : Controller
{
    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(LessonVideoStorage.MaxFileSize + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = LessonVideoStorage.MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> Save(int id, IFormFile? videoFile, string? expectedUrl, CancellationToken cancellationToken)
    {
        var lesson = await lessons.GetLessonByIdAsync(id);
        if (lesson is null) return NotFound();
        var course = await courses.GetCourseByIdAsync(lesson.CourseId);
        if (course is null || !access.CanManageCourse(course, User)) return NotFound();
        expectedUrl ??= "";
        if (!string.Equals(lesson.VideoUrl, expectedUrl, StringComparison.Ordinal)) return Changed(lesson.CourseId);
        if (!ModelState.IsValid || videoFile is null)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn video MP4 (tối đa 100 MB).";
            return Back(lesson.CourseId);
        }
        string fileName;
        try { fileName = await storage.SaveAsync(videoFile, cancellationToken); }
        catch (InvalidDataException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return Back(lesson.CourseId);
        }
        bool saved;
        try { saved = await lessons.TryUpdateVideoAsync(id, expectedUrl, $"/LessonVideos/Stream/{id}/{fileName}"); }
        catch { DeleteFile(fileName); throw; }
        if (!saved) { DeleteFile(fileName); return Changed(lesson.CourseId); }
        DeletePrevious(id, expectedUrl);
        TempData["SuccessMessage"] = "Đã lưu video bài giảng thành công.";
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
        if (!string.Equals(lesson.VideoUrl, expectedUrl, StringComparison.Ordinal) ||
            !await lessons.TryUpdateVideoAsync(id, expectedUrl, "")) return Changed(lesson.CourseId);
        DeletePrevious(id, expectedUrl);
        TempData["SuccessMessage"] = "Đã gỡ video khỏi bài giảng.";
        return Back(lesson.CourseId);
    }

    [HttpGet("/LessonVideos/Stream/{id:int}/{fileName}")]
    public async Task<IActionResult> StreamVideo(int id, string fileName)
    {
        var lesson = await lessons.GetLessonByIdAsync(id);
        if (lesson is null || lesson.VideoUrl != $"/LessonVideos/Stream/{id}/{fileName}") return NotFound();
        var course = await courses.GetCourseByIdAsync(lesson.CourseId);
        if (course is null || !await access.CanLearnCourseAsync(course, User)) return NotFound();
        var stream = storage.OpenRead(fileName);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, "video/mp4", enableRangeProcessing: true);
    }

    private IActionResult Changed(int courseId)
    {
        TempData["ErrorMessage"] = "Video đã thay đổi. Vui lòng tải lại trang trước khi thực hiện.";
        return Back(courseId);
    }

    private IActionResult Back(int courseId) => RedirectToAction("Curriculum", "Instructor", new { id = courseId });
    private void DeletePrevious(int id, string url)
    {
        var prefix = $"/LessonVideos/Stream/{id}/";
        if (url.StartsWith(prefix, StringComparison.Ordinal)) DeleteFile(url[prefix.Length..]);
    }
    private void DeleteFile(string fileName)
    {
        try { storage.Delete(fileName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "Không thể dọn video {FileName}; cần kiểm tra thư mục lưu trữ.", fileName);
        }
    }
}
