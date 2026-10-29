using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Storage;

public sealed class LessonFileCleanup(ILessonMaterialStorage materials, ILogger<LessonFileCleanup> logger)
{
    public void DeleteFiles(Lesson lesson)
    {
        var prefix = $"/LessonMaterials/Download/{lesson.Id}/";
        if (!lesson.MaterialUrl.StartsWith(prefix, StringComparison.Ordinal)) return;
        try { materials.Delete(lesson.MaterialUrl[prefix.Length..]); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "Không thể dọn học liệu của bài giảng đã xóa {LessonId}.", lesson.Id);
        }
    }
}
