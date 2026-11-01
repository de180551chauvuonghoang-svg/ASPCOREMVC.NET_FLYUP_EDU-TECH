using System.Net;
using System.Net.Http.Headers;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Web.Storage;
using Microsoft.AspNetCore.Http;

namespace EduFlyUp.Tests;

public sealed class LessonVideoTests
{
    internal static byte[] Mp4Header => new byte[] { 0, 0, 0, 24, 102, 116, 121, 112, 105, 115, 111, 109, 0, 0, 0, 0, 105, 115, 111, 109, 109, 112, 52, 50 };
    private static void Seed(TestApplication app)
    {
        app.Courses.Courses.Add(new Course { Id = 1, Title = "Video course", InstructorId = "owner@example.test", IsPublished = true });
        app.Lessons.Lessons.Add(new Lesson { Id = 1, CourseId = 1, Title = "Video lesson" });
        app.Courses.Courses.Single().Lessons.Add(app.Lessons.Lessons.Single());
    }
    private static MultipartFormDataContent Upload(string? token, string expectedUrl = "")
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent("1"), "id");
        form.Add(new StringContent(expectedUrl), "expectedUrl");
        if (token is not null) form.Add(new StringContent(token), "__RequestVerificationToken");
        var file = new ByteArrayContent(Mp4Header);
        file.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(file, "videoFile", "lesson.mp4");
        return form;
    }

    [Fact]
    public async Task Owner_uploads_streams_seeks_replaces_and_removes_video()
    {
        var videos = new TestVideoStorage();
        using var app = new TestApplication(videoStorage: videos);
        Seed(app);
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Instructor/Curriculum/1");
        using var form = Upload(token);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/LessonVideos/Save", form)).StatusCode);
        var lesson = app.Lessons.Lessons.Single();
        var oldUrl = lesson.VideoUrl;
        var stream = await browser.Client.GetAsync(oldUrl);
        Assert.Equal(HttpStatusCode.OK, stream.StatusCode);
        Assert.Equal("video/mp4", stream.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Mp4Header, await stream.Content.ReadAsByteArrayAsync());
        using var range = new HttpRequestMessage(HttpMethod.Get, oldUrl);
        range.Headers.Range = new RangeHeaderValue(4, 11);
        var partial = await browser.Client.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(Mp4Header[4..12], await partial.Content.ReadAsByteArrayAsync());
        using var replacement = Upload(token, oldUrl);
        await browser.Client.PostAsync("/LessonVideos/Save", replacement);
        Assert.Single(videos.Files);
        Assert.NotEqual(oldUrl, lesson.VideoUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync(oldUrl)).StatusCode);
        var currentUrl = lesson.VideoUrl;
        var remove = new Dictionary<string, string> { ["id"] = "1", ["expectedUrl"] = currentUrl, ["__RequestVerificationToken"] = token };
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/LessonVideos/Remove", new FormUrlEncodedContent(remove))).StatusCode);
        Assert.Empty(videos.Files);
        Assert.Empty(lesson.VideoUrl);
        Assert.Equal("Video lesson", lesson.Title);
    }

    [Theory]
    [InlineData("Instructor", true, HttpStatusCode.NotFound)]
    [InlineData("Student", true, HttpStatusCode.Redirect)]
    [InlineData("Instructor", false, HttpStatusCode.BadRequest)]
    public async Task Upload_enforces_owner_role_and_antiforgery(string role, bool tokenPresent, HttpStatusCode expected)
    {
        var videos = new TestVideoStorage();
        using var app = new TestApplication(videoStorage: videos);
        Seed(app);
        await app.CreateUserAsync("other@example.test", role);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "other@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Account/Login");
        using var form = Upload(tokenPresent ? token : null);
        Assert.Equal(expected, (await browser.Client.PostAsync("/LessonVideos/Save", form)).StatusCode);
        Assert.Empty(videos.Files);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Active, HttpStatusCode.OK)]
    [InlineData(EnrollmentStatus.Completed, HttpStatusCode.OK)]
    [InlineData(EnrollmentStatus.Cancelled, HttpStatusCode.NotFound)]
    public async Task Video_stream_checks_learning_access_on_published_course(EnrollmentStatus status, HttpStatusCode expected)
    {
        var videos = new TestVideoStorage();
        using var app = new TestApplication(videoStorage: videos);
        Seed(app);
        var name = new string('a', 32) + ".mp4";
        videos.Files[name] = Mp4Header;
        var url = app.Lessons.Lessons.Single().VideoUrl = "/LessonVideos/Stream/1/" + name;
        var userId = await app.CreateUserAsync("learner@example.test", "Student");
        app.Enrollments.Enrollments.Add(new Enrollment { UserId = userId, CourseId = 1, Status = status });
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "learner@example.test");
        Assert.Equal(expected, (await browser.Client.GetAsync(url)).StatusCode);
        using var guest = app.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_conflicting_save_keeps_old_video_and_removes_new_file(bool conflict)
    {
        var videos = new TestVideoStorage();
        using var app = new TestApplication(videoStorage: videos);
        Seed(app);
        var name = new string('a', 32) + ".mp4";
        videos.Files[name] = Mp4Header;
        var oldUrl = app.Lessons.Lessons.Single().VideoUrl = "/LessonVideos/Stream/1/" + name;
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Instructor/Curriculum/1");
        app.Lessons.FailUpdates = !conflict;
        app.Lessons.RejectNextMaterialUpdate = conflict;
        using var form = Upload(token, oldUrl);
        if (conflict) Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/LessonVideos/Save", form)).StatusCode);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => browser.Client.PostAsync("/LessonVideos/Save", form));
        Assert.Single(videos.Files);
        Assert.Equal(oldUrl, app.Lessons.Lessons.Single().VideoUrl);
    }

    [Fact]
    public async Task Deleting_course_also_cleans_its_uploaded_video()
    {
        var videos = new TestVideoStorage();
        using var app = new TestApplication(videoStorage: videos);
        Seed(app);
        var name = new string('a', 32) + ".mp4";
        videos.Files[name] = Mp4Header;
        app.Lessons.Lessons.Single().VideoUrl = "/LessonVideos/Stream/1/" + name;
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Instructor/Curriculum/1");
        await browser.Client.PostAsync("/Courses/Delete/1", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Empty(videos.Files);
        Assert.Empty(app.Courses.Courses);
    }

    [Fact]
    public async Task Real_storage_saves_private_mp4_and_rejects_invalid_signature_mime_and_size()
    {
        var root = Path.Combine(Path.GetTempPath(), "FlyUpVideoTest_" + Guid.NewGuid().ToString("N"));
        var storage = new LessonVideoStorage(new LessonMaterialStorageTests.TestEnvironment { ContentRootPath = root });
        static FormFile File(byte[] bytes, string name, string mime) => new(new MemoryStream(bytes), 0, bytes.Length, "videoFile", name) { Headers = new HeaderDictionary(), ContentType = mime };
        try
        {
            var name = await storage.SaveAsync(File(Mp4Header, "../../video.MP4", "video/mp4"), default);
            Assert.Matches(@"^[a-f0-9]{32}\.mp4$", name);
            Assert.Null(storage.OpenRead("../" + name));
            using (var stream = storage.OpenRead(name)) { Assert.NotNull(stream); Assert.Equal(Mp4Header.Length, stream.Length); }
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(File("fake video"u8.ToArray(), "fake.mp4", "video/mp4"), default));
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(File(Mp4Header, "fake.mp4", "text/html"), default));
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(File(Mp4Header, "fake.exe", "video/mp4"), default));
            var oversized = new FormFile(Stream.Null, 0, LessonVideoStorage.MaxFileSize + 1, "videoFile", "large.mp4");
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.SaveAsync(oversized, default));
            storage.Delete(name);
            Assert.Null(storage.OpenRead(name));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
