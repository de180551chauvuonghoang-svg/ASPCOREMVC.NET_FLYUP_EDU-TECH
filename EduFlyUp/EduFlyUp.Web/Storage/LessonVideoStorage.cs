using System.Text.RegularExpressions;

namespace EduFlyUp.Web.Storage;

public interface ILessonVideoStorage
{
    Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken);
    Stream? OpenRead(string fileName);
    void Delete(string fileName);
}

public sealed class LessonVideoStorage(IWebHostEnvironment environment) : ILessonVideoStorage
{
    public const long MaxFileSize = 100 * 1024 * 1024;
    private string Root => Path.Combine(environment.ContentRootPath, "App_Data", "videos");
    private static bool ValidName(string name) => Regex.IsMatch(name, @"\A[a-f0-9]{32}\.mp4\z");

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxFileSize) throw new InvalidDataException("Video phải có nội dung và không vượt quá 100 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".mp4", StringComparison.OrdinalIgnoreCase) ||
            !(string.Equals(file.ContentType, "video/mp4", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(file.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Chỉ hỗ trợ video MP4.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        try { await input.ReadExactlyAsync(header, cancellationToken); }
        catch (EndOfStreamException) { throw new InvalidDataException("Video không đúng định dạng MP4."); }
        var boxSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header);
        var brand = System.Text.Encoding.ASCII.GetString(header, 8, 4);
        if (boxSize < 16 || boxSize > file.Length || !header.AsSpan(4, 4).SequenceEqual("ftyp"u8) ||
            !new[] { "isom", "iso2", "iso4", "iso5", "iso6", "mp41", "mp42", "avc1", "M4V " }.Contains(brand))
            throw new InvalidDataException("Video không đúng định dạng MP4.");

        Directory.CreateDirectory(Root);
        var name = Guid.NewGuid().ToString("N") + ".mp4";
        var path = Path.Combine(Root, name);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await output.WriteAsync(header, cancellationToken);
            var buffer = new byte[81920];
            long written = header.Length;
            int count;
            while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                written += count;
                if (written > MaxFileSize) throw new InvalidDataException("Video không được vượt quá 100 MB.");
                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            if (written < boxSize) throw new InvalidDataException("Video MP4 bị thiếu dữ liệu.");
        }
        catch { File.Delete(path); throw; }
        return name;
    }

    public Stream? OpenRead(string fileName)
    {
        if (!ValidName(fileName)) return null;
        try { return new FileStream(Path.Combine(Root, fileName), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public void Delete(string fileName)
    {
        if (!ValidName(fileName)) throw new ArgumentException("Invalid stored video name.", nameof(fileName));
        File.Delete(Path.Combine(Root, fileName));
    }
}
