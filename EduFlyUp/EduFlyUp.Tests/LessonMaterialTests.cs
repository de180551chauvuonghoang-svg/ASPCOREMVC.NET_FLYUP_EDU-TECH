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

    private static string SeedMaterial(TestApplication app, TestMaterialStorage storage)
    {
        var name = new string('b', 32) + ".pdf";
        storage.Files[name] = "%PDF-1.7 old"u8.ToArray();
        var lesson = app.Lessons.Lessons.Single();
        lesson.MaterialUrl = $"/LessonMaterials/Download/1/{name}";
        lesson.AttachmentName = "old.pdf";
        return lesson.MaterialUrl;
    }

    [Theory]
    [InlineData("Instructor")]
    [InlineData("Admin")]
    public async Task Owner_or_admin_replaces_then_removes_material_without_deleting_lesson(string role)
    {
        var storage = new TestMaterialStorage();
        using var app = new TestApplication(storage);
        Seed(app);
        var oldUrl = SeedMaterial(app, storage);
        var email = role == "Admin" ? "admin@example.test" : "owner@example.test";
        await app.CreateUserAsync(email, role);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, email);
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Instructor/Curriculum/1");
        using var form = Upload(token);
        form.Add(new StringContent(oldUrl), "expectedUrl");
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/LessonMaterials/Replace", form)).StatusCode);
        var lesson = app.Lessons.Lessons.Single();
        Assert.NotEqual(oldUrl, lesson.MaterialUrl);
        Assert.Equal("bai-giang.pdf", lesson.AttachmentName);
        Assert.Single(storage.Files);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync(oldUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync(lesson.MaterialUrl)).StatusCode);
        var html = await browser.Client.GetStringAsync("/Instructor/Curriculum/1");
        Assert.Contains("/LessonMaterials/Replace", html);
        Assert.Contains("/LessonMaterials/Remove", html);
        var newUrl = lesson.MaterialUrl;
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/LessonMaterials/Remove", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "1", ["expectedUrl"] = newUrl, ["__RequestVerificationToken"] = token
        }))).StatusCode);
        Assert.Empty(lesson.MaterialUrl);
        Assert.Empty(lesson.AttachmentName);
        Assert.Equal("Bài 1", lesson.Title);
        Assert.Empty(storage.Files);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync(newUrl)).StatusCode);
    }

    [Theory]
    [InlineData("Replace", "Instructor", true, HttpStatusCode.NotFound)]
    [InlineData("Remove", "Instructor", true, HttpStatusCode.NotFound)]
    [InlineData("Replace", "Student", true, HttpStatusCode.Redirect)]
    [InlineData("Remove", "Student", true, HttpStatusCode.Redirect)]
    [InlineData("Replace", "Instructor", false, HttpStatusCode.BadRequest)]
    [InlineData("Remove", "Instructor", false, HttpStatusCode.BadRequest)]
    public async Task Mutations_enforce_owner_role_and_antiforgery(string action, string role, bool withToken, HttpStatusCode expected)
    {
        var storage = new TestMaterialStorage();
        using var app = new TestApplication(storage);
        Seed(app);
        var oldUrl = SeedMaterial(app, storage);
        await app.CreateUserAsync("other@example.test", role);
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "other@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Account/Login");
        using var form = Upload(withToken ? token : null);
        form.Add(new StringContent(oldUrl), "expectedUrl");
        Assert.Equal(expected, (await browser.Client.PostAsync("/LessonMaterials/" + action, form)).StatusCode);
        Assert.Equal(oldUrl, app.Lessons.Lessons.Single().MaterialUrl);
        Assert.Single(storage.Files);
    }

    [Theory]
    [InlineData("Replace", "stale")]
    [InlineData("Replace", "conflict")]
    [InlineData("Replace", "database")]
    [InlineData("Replace", "invalid")]
    [InlineData("Remove", "stale")]
    [InlineData("Remove", "conflict")]
    [InlineData("Remove", "database")]
    public async Task Failed_or_stale_mutation_keeps_old_material(string action, string failure)
    {
        var storage = new TestMaterialStorage();
        using var app = new TestApplication(storage);
        Seed(app);
        var oldUrl = SeedMaterial(app, storage);
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Instructor/Curriculum/1");
        app.Lessons.FailUpdates = failure == "database";
        app.Lessons.RejectNextMaterialUpdate = failure == "conflict";
        storage.RejectFile = failure == "invalid";
        using var form = Upload(token);
        form.Add(new StringContent(failure == "stale" ? "https://example.test/old.pdf" : oldUrl), "expectedUrl");
        if (failure == "database")
            await Assert.ThrowsAsync<InvalidOperationException>(() => browser.Client.PostAsync("/LessonMaterials/" + action, form));
        else
            Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/LessonMaterials/" + action, form)).StatusCode);
        Assert.Equal(oldUrl, app.Lessons.Lessons.Single().MaterialUrl);
        Assert.Single(storage.Files);
        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync(oldUrl)).StatusCode);
    }

    [Fact]
    public async Task Cleanup_failure_revokes_old_download_and_external_link_removal_never_deletes_storage()
    {
        var storage = new TestMaterialStorage { FailDelete = true };
        using var app = new TestApplication(storage);
        Seed(app);
        var oldUrl = SeedMaterial(app, storage);
        await app.CreateUserAsync("owner@example.test", "Instructor");
        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TestApplication.AntiForgeryTokenAsync(browser, "/Instructor/Curriculum/1");
        var form = new Dictionary<string, string> { ["id"] = "1", ["expectedUrl"] = oldUrl, ["__RequestVerificationToken"] = token };
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/LessonMaterials/Remove", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Client.GetAsync(oldUrl)).StatusCode);
        Assert.Single(storage.Files); // Logged orphan; the revoked URL cannot be used to retrieve it.
        var lesson = app.Lessons.Lessons.Single();
        lesson.MaterialUrl = "https://example.test/slide.pdf";
        lesson.AttachmentName = "External";
        form["expectedUrl"] = lesson.MaterialUrl;
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.PostAsync("/LessonMaterials/Remove", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Empty(lesson.MaterialUrl);
        Assert.Single(storage.Files);
    }
}
