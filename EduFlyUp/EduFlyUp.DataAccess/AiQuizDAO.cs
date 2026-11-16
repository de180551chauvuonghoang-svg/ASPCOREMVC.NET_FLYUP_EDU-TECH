using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

/// <summary>
/// Data Access Object chịu trách nhiệm truy vấn thông tin học liệu và giải mã file bài giảng
/// </summary>
public sealed class AiQuizDAO : SingletonBase<AiQuizDAO>
{
    public async Task<LessonMaterialContext?> GetLessonMaterialContextAsync(int lessonId, CancellationToken cancellationToken = default)
    {
        using var context = new AppDbContext();
        var lesson = await context.Lessons
            .Include(l => l.Course)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken);

        if (lesson is null) return null;

        var extractedText = string.Empty;

        // Nếu có file tài liệu lưu trữ cục bộ trong App_Data/materials/
        if (!string.IsNullOrWhiteSpace(lesson.MaterialUrl))
        {
            var prefix = $"/LessonMaterials/Download/{lessonId}/";
            if (lesson.MaterialUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var storedFileName = lesson.MaterialUrl[prefix.Length..];
                var root = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "materials");
                var filePath = Path.Combine(root, storedFileName);

                if (File.Exists(filePath))
                {
                    try
                    {
                        var ext = Path.GetExtension(storedFileName).ToLowerInvariant();
                        if (ext == ".docx")
                        {
                            extractedText = ExtractTextFromDocx(filePath);
                        }
                        else if (ext is ".txt" or ".md" or ".json" or ".csv")
                        {
                            extractedText = await File.ReadAllTextAsync(filePath, cancellationToken);
                        }
                    }
                    catch
                    {
                        // Bỏ qua lỗi đọc file để không làm crash luồng ứng dụng
                    }
                }
            }
        }

        return new LessonMaterialContext
        {
            LessonId = lesson.Id,
            LessonTitle = lesson.Title,
            SectionName = lesson.SectionName,
            DurationMinutes = lesson.DurationMinutes,
            CourseTitle = lesson.Course?.Title ?? string.Empty,
            Content = lesson.Content ?? string.Empty,
            MaterialUrl = lesson.MaterialUrl ?? string.Empty,
            AttachmentName = lesson.AttachmentName ?? string.Empty,
            ExtractedDocumentText = extractedText
        };
    }

    private static string ExtractTextFromDocx(string filePath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            var entry = zip.GetEntry("word/document.xml");
            if (entry is null) return string.Empty;

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);
            var textElements = doc.Descendants().Where(d => d.Name.LocalName == "t").Select(d => d.Value);
            return string.Join(" ", textElements);
        }
        catch
        {
            return string.Empty;
        }
    }
}
