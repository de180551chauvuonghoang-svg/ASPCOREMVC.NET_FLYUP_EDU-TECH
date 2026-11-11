using System.Net;
using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class CourseGovernanceTests
{
    private static Course MakeCourse(
        int id = 1,
        string title = "Governance Course",
        string owner = "owner@example.test",
        CourseStatus status = CourseStatus.Draft,
        bool published = false,
        int lessonCount = 0,
        string? rejectionReason = null)
    {
        var course = new Course
        {
            Id = id,
            Title = title,
            Description = "Course description",
            InstructorId = owner,
            Status = status,
            IsPublished = published,
            RejectionReason = rejectionReason,
            Level = CourseLevel.Beginner,
            Price = 100000
        };
        for (var i = 1; i <= lessonCount; i++)
        {
            course.Lessons.Add(new Lesson { Id = (id * 100) + i, CourseId = id, Title = $"Lesson {i}", Order = i });
        }
        return course;
    }

    private static void SeedLessonsIntoRepo(TestApplication app, Course course)
    {
        foreach (var lesson in course.Lessons)
        {
            app.Lessons.Lessons.Add(lesson);
        }
    }

    private static async Task<string> TokenAsync(TestBrowser browser, string path)
        => await TestApplication.AntiForgeryTokenAsync(browser, path);

    [Fact]
    public async Task Unarchive_requires_at_least_one_lesson_and_transitions_to_pending_approval()
    {
        using var app = new TestApplication();
        var emptyArchived = MakeCourse(1, "Empty Archived", status: CourseStatus.Archived, lessonCount: 0, rejectionReason: "Old error");
        var validArchived = MakeCourse(2, "Valid Archived", status: CourseStatus.Archived, lessonCount: 1, rejectionReason: "Old error");
        app.Courses.Courses.AddRange([emptyArchived, validArchived]);
        SeedLessonsIntoRepo(app, validArchived);
        await app.CreateUserAsync("owner@example.test", "Instructor");

        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TokenAsync(browser, "/Instructor/Dashboard");

        var failResponse = await browser.Client.PostAsync("/Instructor/Unarchive/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, failResponse.StatusCode);
        Assert.Equal(CourseStatus.Archived, app.Courses.Courses[0].Status);
        Assert.False(app.Courses.Courses[0].IsPublished);

        var okResponse = await browser.Client.PostAsync("/Instructor/Unarchive/2", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, okResponse.StatusCode);
        Assert.Equal(CourseStatus.PendingApproval, app.Courses.Courses[1].Status);
        Assert.False(app.Courses.Courses[1].IsPublished);
        Assert.Equal(string.Empty, app.Courses.Courses[1].RejectionReason);
    }

    [Theory]
    [InlineData(CourseStatus.Approved, true, false)]
    [InlineData(CourseStatus.PendingApproval, false, false)]
    [InlineData(CourseStatus.Archived, false, false)]
    [InlineData(CourseStatus.Draft, false, true)]
    public async Task DeleteLesson_blocks_last_lesson_for_published_pending_archived_or_enrolled_courses(
        CourseStatus status, bool published, bool withActiveEnrollment)
    {
        using var app = new TestApplication();
        var course = MakeCourse(1, status: status, published: published, lessonCount: 1);
        app.Courses.Courses.Add(course);
        SeedLessonsIntoRepo(app, course);
        await app.CreateUserAsync("owner@example.test", "Instructor");
        if (withActiveEnrollment)
        {
            var studentId = await app.CreateUserAsync("student@example.test", "Student");
            app.Enrollments.Enrollments.Add(new Enrollment { CourseId = 1, UserId = studentId, Status = EnrollmentStatus.Active });
        }

        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TokenAsync(browser, "/Instructor/Curriculum/1");

        var response = await browser.Client.PostAsync("/Instructor/DeleteLesson", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = "101",
            ["courseId"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(app.Lessons.Lessons);

        var curriculumHtml = await browser.Client.GetStringAsync("/Instructor/Curriculum/1");
        Assert.DoesNotContain("asp-action=\"DeleteLesson\"", curriculumHtml);
        Assert.DoesNotContain("/Instructor/DeleteLesson", curriculumHtml);
    }

    [Fact]
    public async Task DeleteLesson_uses_actual_lesson_course_even_if_courseId_form_field_is_forged()
    {
        using var app = new TestApplication();
        var draftCourse = MakeCourse(1, "Own Draft", status: CourseStatus.Draft, lessonCount: 1);
        var publishedCourse = MakeCourse(2, "Own Live", status: CourseStatus.Approved, published: true, lessonCount: 1);
        app.Courses.Courses.AddRange([draftCourse, publishedCourse]);
        SeedLessonsIntoRepo(app, draftCourse);
        SeedLessonsIntoRepo(app, publishedCourse);
        await app.CreateUserAsync("owner@example.test", "Instructor");

        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TokenAsync(browser, "/Instructor/Curriculum/1");

        var forgedResponse = await browser.Client.PostAsync("/Instructor/DeleteLesson", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = "201",
            ["courseId"] = "1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, forgedResponse.StatusCode);
        Assert.Contains("/Instructor/Curriculum/2", forgedResponse.Headers.Location?.ToString());
        Assert.Contains(app.Lessons.Lessons, l => l.Id == 201);

        // Deleting last lesson on un-enrolled Draft course IS allowed.
        var draftDelete = await browser.Client.PostAsync("/Instructor/DeleteLesson", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["id"] = "101",
            ["courseId"] = "1"
        }));
        Assert.Equal(HttpStatusCode.Redirect, draftDelete.StatusCode);
        Assert.DoesNotContain(app.Lessons.Lessons, l => l.Id == 101);
    }

    [Fact]
    public async Task Admin_reject_course_requires_reason_sets_rejected_and_refuses_draft_or_archived()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        var ownerId = await app.CreateUserAsync("owner@example.test", "Instructor");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Courses.AddRange(
                MakeCourse(1, "Pending Course", ownerId, CourseStatus.PendingApproval, lessonCount: 1),
                MakeCourse(2, "Draft Course", ownerId, CourseStatus.Draft, lessonCount: 1),
                MakeCourse(3, "Archived Course", ownerId, CourseStatus.Archived, lessonCount: 1));
            await db.SaveChangesAsync();
        }

        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");
        var token = await TokenAsync(browser, "/Admin/Courses");

        // Missing reason keeps PendingApproval intact.
        var blankReject = await browser.Client.PostAsync("/Admin/RejectCourse/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["rejectionReason"] = "   "
        }));
        Assert.Equal(HttpStatusCode.Redirect, blankReject.StatusCode);

        // Draft and Archived cannot be rejected.
        await browser.Client.PostAsync("/Admin/RejectCourse/2", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["rejectionReason"] = "Should fail on draft"
        }));
        await browser.Client.PostAsync("/Admin/RejectCourse/3", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["rejectionReason"] = "Should fail on archived"
        }));

        // Valid reject updates PendingApproval -> Rejected with trimmed reason.
        var okReject = await browser.Client.PostAsync("/Admin/RejectCourse/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["rejectionReason"] = "  Video bị mất tiếng ở bài 1  ",
            ["returnUrl"] = "/Admin/CourseDetails/1"
        }));
        Assert.Equal(HttpStatusCode.Redirect, okReject.StatusCode);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = await db.Courses.AsNoTracking().SingleAsync(c => c.Id == 1);
            var draft = await db.Courses.AsNoTracking().SingleAsync(c => c.Id == 2);
            var archived = await db.Courses.AsNoTracking().SingleAsync(c => c.Id == 3);

            Assert.Equal(CourseStatus.Rejected, pending.Status);
            Assert.False(pending.IsPublished);
            Assert.Equal("Video bị mất tiếng ở bài 1", pending.RejectionReason);
            Assert.Equal(CourseStatus.Draft, draft.Status);
            Assert.Null(draft.RejectionReason);
            Assert.Equal(CourseStatus.Archived, archived.Status);
            Assert.Null(archived.RejectionReason);
        }

        var detailHtml = WebUtility.HtmlDecode(await browser.Client.GetStringAsync("/Admin/CourseDetails/1"));
        Assert.Contains("Video bị mất tiếng ở bài 1", detailHtml);
        var listHtml = WebUtility.HtmlDecode(await browser.Client.GetStringAsync("/Admin/Courses"));
        Assert.Contains("Video bị mất tiếng ở bài 1", listHtml);
    }

    [Fact]
    public async Task Admin_disable_supports_optional_reason_and_status_filter_matches_expected_courses()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("admin@example.test", "Admin");
        var ownerId = await app.CreateUserAsync("owner@example.test", "Instructor");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Courses.AddRange(
                MakeCourse(1, "Course Live", ownerId, CourseStatus.Approved, published: true, lessonCount: 1),
                MakeCourse(2, "Course Pending", ownerId, CourseStatus.PendingApproval, lessonCount: 1),
                MakeCourse(3, "Course Draft", ownerId, CourseStatus.Draft, lessonCount: 0),
                MakeCourse(4, "Course Rejected", ownerId, CourseStatus.Rejected, rejectionReason: "Thiếu mô tả"),
                MakeCourse(5, "Course Archived", ownerId, CourseStatus.Archived, lessonCount: 1));
            await db.SaveChangesAsync();
        }

        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");

        var pendingList = await browser.Client.GetStringAsync($"/Admin/Courses?Status={(int)CourseStatus.PendingApproval}");
        Assert.Contains("Course Pending", pendingList);
        Assert.Contains("Từ chối", pendingList);
        Assert.DoesNotContain("Course Live", pendingList);
        Assert.DoesNotContain("Course Draft", pendingList);
        Assert.DoesNotContain("Course Archived", pendingList);

        var draftList = await browser.Client.GetStringAsync($"/Admin/Courses?Status={(int)CourseStatus.Draft}");
        Assert.Contains("Course Draft", draftList);
        Assert.DoesNotContain("Course Pending", draftList);

        var archivedList = await browser.Client.GetStringAsync($"/Admin/Courses?Status={(int)CourseStatus.Archived}");
        Assert.Contains("Course Archived", archivedList);
        Assert.DoesNotContain("Course Rejected", archivedList);

        var token = await TokenAsync(browser, "/Admin/CourseDetails/1");
        await browser.Client.PostAsync("/Admin/DisableCourse/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["rejectionReason"] = "  Nội dung chưa cập nhật  "
        }));

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var disabled = await db.Courses.AsNoTracking().SingleAsync(c => c.Id == 1);
            Assert.False(disabled.IsPublished);
            Assert.Equal(CourseStatus.Rejected, disabled.Status);
            Assert.Equal("Nội dung chưa cập nhật", disabled.RejectionReason);
        }
    }

    [Theory]
    [InlineData(CourseStatus.Draft)]
    [InlineData(CourseStatus.PendingApproval)]
    [InlineData(CourseStatus.Rejected)]
    [InlineData(CourseStatus.Archived)]
    public async Task Unpublished_unenrolled_courses_can_be_deleted(CourseStatus status)
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(MakeCourse(1, status: status, published: false, lessonCount: 0));
        await app.CreateUserAsync("owner@example.test", "Instructor");

        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");
        var token = await TokenAsync(browser, "/Instructor/Dashboard");

        var response = await browser.Client.PostAsync("/Courses/Delete/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(app.Courses.Courses);
    }

    [Theory]
    [InlineData(CourseStatus.Draft, false)]
    [InlineData(CourseStatus.PendingApproval, false)]
    [InlineData(CourseStatus.Rejected, false)]
    [InlineData(CourseStatus.Archived, false)]
    [InlineData(CourseStatus.Approved, true)]
    public async Task Courses_with_enrollments_or_live_status_cannot_be_deleted_even_by_admin(CourseStatus status, bool published)
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(MakeCourse(1, status: status, published: published, lessonCount: 1));
        var studentId = await app.CreateUserAsync("student@example.test", "Student");
        if (!published)
        {
            app.Enrollments.Enrollments.Add(new Enrollment { CourseId = 1, UserId = studentId, Status = EnrollmentStatus.Active });
        }

        await app.CreateUserAsync("owner@example.test", "Instructor");
        await app.CreateUserAsync("admin@example.test", "Admin");

        foreach (var account in new[] { "owner@example.test", "admin@example.test" })
        {
            using var browser = app.CreateBrowser();
            await TestApplication.LoginAsync(browser, account);
            var token = await TokenAsync(browser, "/Courses/Edit/1");

            var editHtml = await browser.Client.GetStringAsync("/Courses/Edit/1");
            Assert.Contains("Không thể xóa", editHtml);
            Assert.DoesNotContain("id=\"delete-course-form\"", editHtml);

            var response = await browser.Client.PostAsync("/Courses/Delete/1", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Single(app.Courses.Courses);
        }
    }

    [Fact]
    public async Task Admin_cannot_publish_zero_lesson_or_draft_course_via_course_create_or_edit()
    {
        using var app = new TestApplication();
        app.Courses.Courses.Add(MakeCourse(1, status: CourseStatus.Draft, published: false, lessonCount: 0));
        await app.CreateUserAsync("admin@example.test", "Admin");

        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "admin@example.test");

        var createHtml = await browser.Client.GetStringAsync("/Courses/Create");
        Assert.DoesNotContain("name=\"IsPublished\"", createHtml, StringComparison.Ordinal);

        var createToken = await TokenAsync(browser, "/Courses/Create");
        var createPost = await browser.Client.PostAsync("/Courses/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = createToken,
            ["Title"] = "New Admin Course",
            ["Description"] = "Desc",
            ["Price"] = "100000",
            ["Level"] = "0",
            ["IsPublished"] = "true"
        }));
        Assert.Equal(HttpStatusCode.OK, createPost.StatusCode);
        Assert.Single(app.Courses.Courses);

        var editToken = await TokenAsync(browser, "/Courses/Edit/1");
        var editPost = await browser.Client.PostAsync("/Courses/Edit/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Id"] = "1",
            ["Title"] = "Updated Admin Draft",
            ["Description"] = "Desc",
            ["Price"] = "100000",
            ["Level"] = "0",
            ["IsPublished"] = "true"
        }));
        Assert.Equal(HttpStatusCode.OK, editPost.StatusCode);
        Assert.False(app.Courses.Courses[0].IsPublished);
        Assert.Equal(0, app.Courses.PublicationUpdates);
    }

    [Fact]
    public async Task Instructor_can_set_and_update_course_category_via_create_and_edit()
    {
        using var app = new TestApplication();
        await app.CreateUserAsync("owner@example.test", "Instructor");

        using var browser = app.CreateBrowser();
        await TestApplication.LoginAsync(browser, "owner@example.test");

        var createToken = await TokenAsync(browser, "/Courses/Create");
        var createResponse = await browser.Client.PostAsync("/Courses/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = createToken,
            ["Title"] = "Course With Category",
            ["Description"] = "Category test description",
            ["Price"] = "150000",
            ["Level"] = "1",
            ["Category"] = "Cơ sở dữ liệu"
        }));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Single(app.Courses.Courses);
        Assert.Equal("Cơ sở dữ liệu", app.Courses.Courses[0].Category);

        var editToken = await TokenAsync(browser, "/Courses/Edit/1");
        var editResponse = await browser.Client.PostAsync("/Courses/Edit/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Id"] = "1",
            ["Title"] = "Course Updated Category",
            ["Description"] = "Category test description",
            ["Price"] = "200000",
            ["Level"] = "2",
            ["Category"] = "ASP.NET Core"
        }));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        Assert.Equal("ASP.NET Core", app.Courses.Courses[0].Category);
    }
}
