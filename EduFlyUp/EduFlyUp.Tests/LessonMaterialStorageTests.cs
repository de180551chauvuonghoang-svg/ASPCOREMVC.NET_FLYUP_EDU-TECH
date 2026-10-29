using System.IO.Compression;
using EduFlyUp.Web.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace EduFlyUp.Tests;

public sealed class LessonMaterialStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FlyUpMaterialTest_" + Guid.NewGuid().ToString("N"));
    private LessonMaterialStorage Storage => new(new TestEnvironment { ContentRootPath = _root });

    private static FormFile File(byte[] bytes, string name, string mime) => new(new MemoryStream(bytes), 0, bytes.Length, "materialFile", name)
    {
        Headers = new HeaderDictionary(), ContentType = mime
    };

    [Fact]
    public async Task Saves_private_file_with_generated_name_and_rejects_traversal()
    {
        var bytes = "%PDF-1.7\nmaterial"u8.ToArray();
        var storage = Storage;
        var name = await storage.SaveAsync(File(bytes, "../../lesson.PDF", "application/pdf"), default);
        Assert.Matches(@"^[a-f0-9]{32}\.pdf$", name);
        Assert.True(System.IO.File.Exists(Path.Combine(_root, "App_Data", "materials", name)));
        Assert.False(Directory.Exists(Path.Combine(_root, "wwwroot")));
        using var input = storage.OpenRead(name);
        Assert.NotNull(input);
        using var copy = new MemoryStream();
        await input.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
        input.Dispose();
        Assert.Null(storage.OpenRead("../" + name));
        storage.Delete(name);
        Assert.Null(storage.OpenRead(name));
    }

    [Theory]
    [InlineData("script.html", "text/html", "<script>alert(1)</script>")]
    [InlineData("fake.pdf", "application/pdf", "executable")]
    [InlineData("fake.pdf", "text/html", "%PDF-1.7")]
    [InlineData("fake.docx", "application/octet-stream", "not a zip")]
    [InlineData("empty.pdf", "application/pdf", "")]
    public async Task Rejects_disallowed_extension_mime_signature_and_empty_file(string name, string mime, string text)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Storage.SaveAsync(File(System.Text.Encoding.UTF8.GetBytes(text), name, mime), default));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task Rejects_oversized_file_before_writing()
    {
        var file = new FormFile(Stream.Null, 0, LessonMaterialStorage.MaxFileSize + 1, "materialFile", "large.pdf");
        await Assert.ThrowsAsync<InvalidDataException>(() => Storage.SaveAsync(file, default));
        Assert.False(Directory.Exists(_root));
    }

    [Theory]
    [InlineData(".docx", "word/document.xml")]
    [InlineData(".pptx", "ppt/presentation.xml")]
    public async Task Office_packages_need_the_correct_document_entry_and_reject_macros(string extension, string entry)
    {
        static byte[] Package(string entryName, bool macro = false)
        {
            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                zip.CreateEntry("[Content_Types].xml");
                zip.CreateEntry(entryName);
                if (macro) zip.CreateEntry("word/vbaProject.bin");
            }
            return output.ToArray();
        }
        var storage = Storage;
        var name = await storage.SaveAsync(File(Package(entry), "lesson" + extension, "application/octet-stream"), default);
        Assert.EndsWith(extension, name);
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(File(Package("other.xml"), "fake" + extension, "application/octet-stream"), default));
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(File(Package(entry, true), "macro" + extension, "application/octet-stream"), default));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
