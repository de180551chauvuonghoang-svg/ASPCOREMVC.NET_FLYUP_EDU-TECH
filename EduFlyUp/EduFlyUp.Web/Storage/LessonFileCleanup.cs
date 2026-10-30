using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Storage;

public sealed class LessonFileCleanup(ILessonMaterialStorage materials, ILessonVideoStorage videos, ILogger<LessonFileCleanup> logger)
{
    public void DeleteFiles(Lesson lesson)
    {
        var prefix = $"/LessonMaterials/Download/{lesson.Id}/";
        if (lesson.MaterialUrl.StartsWith(prefix, StringComparison.Ordinal))
            TryDelete(() => materials.Delete(lesson.MaterialUrl[prefix.Length..]), lesson.Id);
        var videoPrefix = $"/LessonVideos/Stream/{lesson.Id}/";
        if (lesson.VideoUrl.StartsWith(videoPrefix, StringComparison.Ordinal))
            TryDelete(() => videos.Delete(lesson.VideoUrl[videoPrefix.Length..]), lesson.Id);
    }

    private void TryDelete(Action delete, int lessonId)
    {
        try { delete(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "Không thể dọn học liệu của bài giảng đã xóa {LessonId}.", lessonId);
        }
    }
}
