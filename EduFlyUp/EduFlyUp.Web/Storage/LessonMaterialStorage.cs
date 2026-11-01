using System.IO.Compression;
using System.Text.RegularExpressions;

namespace EduFlyUp.Web.Storage;

public interface ILessonMaterialStorage
{
    Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken);
    Stream? OpenRead(string fileName);
    void Delete(string fileName);
}

public sealed class LessonMaterialStorage(IWebHostEnvironment environment) : ILessonMaterialStorage
{
    public const long MaxFileSize = 10 * 1024 * 1024;
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "materials");
    private static bool IsStoredName(string name) => Regex.IsMatch(name, @"\A[a-f0-9]{32}\.(pdf|doc|docx|ppt|pptx)\z");

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxFileSize)
            throw new InvalidDataException("Tài liệu phải có nội dung và không vượt quá 10 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var expectedMime = extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => throw new InvalidDataException("Chỉ hỗ trợ PDF, DOC, DOCX, PPT và PPTX.")
        };
        if (!string.Equals(file.ContentType, expectedMime, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Định dạng tài liệu không khớp với phần mở rộng.");

        using var content = new MemoryStream();
        await using (var input = file.OpenReadStream())
        {
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (content.Length + count > MaxFileSize)
                    throw new InvalidDataException("Tài liệu không được vượt quá 10 MB.");
                await content.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
        }
        var bytes = content.ToArray();
        var valid = extension switch
        {
            ".pdf" => bytes.AsSpan().StartsWith("%PDF-"u8),
            ".doc" or ".ppt" => bytes.AsSpan().StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            _ => IsOfficePackage(content, extension)
        };
        if (!valid) throw new InvalidDataException("Nội dung file không đúng định dạng tài liệu đã chọn.");
        Directory.CreateDirectory(Root);
        var name = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(Root, name);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await output.WriteAsync(bytes, cancellationToken);
        }
        catch { File.Delete(path); throw; }
        return name;
    }

    private static bool IsOfficePackage(MemoryStream content, string extension)
    {
        try
        {
            content.Position = 0;
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            return archive.GetEntry("[Content_Types].xml") is not null &&
                archive.GetEntry(extension == ".docx" ? "word/document.xml" : "ppt/presentation.xml") is not null &&
                !archive.Entries.Any(e => e.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException) { return false; }
    }

    public Stream? OpenRead(string fileName)
    {
        if (!IsStoredName(fileName)) return null;
        try { return File.OpenRead(Path.Combine(Root, fileName)); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    public void Delete(string fileName)
    {
        if (!IsStoredName(fileName)) throw new ArgumentException("Invalid stored file name.", nameof(fileName));
        File.Delete(Path.Combine(Root, fileName));
    }
}
