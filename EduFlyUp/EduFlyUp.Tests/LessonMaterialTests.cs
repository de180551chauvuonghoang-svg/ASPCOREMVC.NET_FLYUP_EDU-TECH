using System.Net;
using System.Net.Http.Headers;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Tests;

public sealed class LessonMaterialTests
{
    private static void Seed(TestApplication app)
    {
        app.Courses.Courses.Add(new Course { Id = 1, Title = "Khóa học", InstructorId = "owner@example.test", IsPublished = true });
        app.Lessons.Lessons.Add(new Lesson { Id = 1, CourseId = 1, Title = "Bài 1" });
    }

    private static MultipartFormDataContent Upload(string? token)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent("1"), "id");
        if (token is not null) form.Add(new StringContent(token), "__RequestVerificationToken");
        var file = new ByteArrayContent("%PDF-1.7\nTest material"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "materialFile", "bai-giang.pdf");
        return form;
    }

    [Fact]
    public async Task Owner_uploads_downloads_and_edits_lesson_without_losing_material()
    {
        var storage = new TestMaterialStorage();
        using var app = new TestApplication(storage);
        Seed(app);
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Instructor/Curriculum/1");
        using var form = Upload(token);
        var response = await browser.Client.PostAsync("/LessonMaterials/Upload", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Instructor/Curriculum/1", response.Headers.Location?.ToString());
        var lesson = app.Lessons.Lessons.Single();
        Assert.Equal("bai-giang.pdf", lesson.AttachmentName);
        var url = lesson.MaterialUrl;
        var download = await browser.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("%PDF-1.7\nTest material"u8.ToArray(), await download.Content.ReadAsByteArrayAsync());
        Assert.True(download.Headers.CacheControl?.NoStore);

        var edit = await browser.Client.PostAsync("/Instructor/EditLesson", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Id"] = "1", ["CourseId"] = "1",
            ["Title"] = "Bài đã sửa", ["SectionName"] = "Chương 1", ["Order"] = "1"
        }));
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.Equal("Bài đã sửa", lesson.Title);
        Assert.Equal(url, lesson.MaterialUrl);
        Assert.Equal("bai-giang.pdf", lesson.AttachmentName);

        using var duplicate = Upload(token);
        await browser.Client.PostAsync("/LessonMaterials/Upload", duplicate);
        Assert.Single(storage.Files);
        Assert.Equal(url, lesson.MaterialUrl);
    }

    [Theory]
    [InlineData("Instructor", true, HttpStatusCode.NotFound)]
    [InlineData("Student", true, HttpStatusCode.Redirect)]
    [InlineData("Instructor", false, HttpStatusCode.BadRequest)]
    public async Task Upload_rejects_other_owner_student_and_missing_antiforgery(string role, bool includeToken, HttpStatusCode expected)
    {
        var storage = new TestMaterialStorage();
        using var app = new TestApplication(storage);
        Seed(app);
        await app.CreateUserAsync("other@example.test", role);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "other@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Account/Login");
        using var form = Upload(includeToken ? token : null);
        var response = await browser.Client.PostAsync("/LessonMaterials/Upload", form);
        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(storage.Files);
        Assert.Empty(app.Lessons.Lessons.Single().MaterialUrl);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Active, HttpStatusCode.OK)]
    [InlineData(EnrollmentStatus.Completed, HttpStatusCode.OK)]
    [InlineData(EnrollmentStatus.Cancelled, HttpStatusCode.NotFound)]
    public async Task Download_requires_learning_access_even_for_published_courses(EnrollmentStatus status, HttpStatusCode expected)
    {
        var storage = new TestMaterialStorage();
        using var app = new TestApplication(storage);
        Seed(app);
        var name = new string('a', 32) + ".pdf";
        storage.Files[name] = "%PDF-1.7"u8.ToArray();
        var lesson = app.Lessons.Lessons.Single();
        lesson.MaterialUrl = $"/LessonMaterials/Download/1/{name}";
        lesson.AttachmentName = "lesson.pdf";
        var userId = await app.CreateUserAsync("learner@example.test", "Student");
        app.Enrollments.Enrollments.Add(new Enrollment { CourseId = 1, UserId = userId, Status = status });
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "learner@example.test");
        Assert.Equal(expected, (await browser.Client.GetAsync(lesson.MaterialUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync($"/LessonMaterials/Download/2/{name}")).StatusCode);
        using var guest = app.CreateBrowser();
        Assert.Equal(HttpStatusCode.Redirect, (await guest.Client.GetAsync(lesson.MaterialUrl)).StatusCode);
    }

    [Fact]
    public async Task Failed_database_save_removes_new_file()
    {
        var storage = new TestMaterialStorage();
        using var app = new TestApplication(storage);
        Seed(app);
        app.Lessons.FailUpdates = true;
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Instructor/Curriculum/1");
        using var form = Upload(token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => browser.Client.PostAsync("/LessonMaterials/Upload", form));
        Assert.Empty(storage.Files);
    }
}
