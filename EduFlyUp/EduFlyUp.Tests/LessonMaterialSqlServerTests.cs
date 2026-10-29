using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace EduFlyUp.Tests;

public sealed class LessonMaterialSqlServerTests
{
    [Fact]
    public async Task Production_material_and_content_updates_translate_to_sql_server()
    {
        var recorder = new CommandRecorder();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=TranslationOnly;Integrated Security=True")
            .AddInterceptors(new NoConnection(), recorder).Options);
        Assert.True(await LessonDAO.TryUpdateMaterialAsync(db, 1, "old", "new", "new.pdf"));
        Assert.Contains("WHERE", recorder.Sql);
        Assert.Contains("[MaterialUrl] =", recorder.Sql);
        Assert.Contains("[AttachmentName] =", recorder.Sql);
        Assert.Contains("AND", recorder.Sql);
        await LessonDAO.UpdateContentAsync(db, new Lesson { Id = 1, Title = "Updated" });
        Assert.Contains("CASE", recorder.Sql);
        Assert.Contains("[Title] =", recorder.Sql);
        Assert.DoesNotContain("[CourseId] =", recorder.Sql);
    }

    private sealed class NoConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public string Sql { get; private set; } = "";
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Sql = command.CommandText;
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }
    }

    [SqlServerFact]
    public async Task Delete_returns_committed_material_snapshots_and_keeps_other_courses()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = new Course { Title = "Delete", InstructorId = "owner" };
        var lessons = new[] { new Lesson { Title = "First", Course = course, MaterialUrl = "first" }, new Lesson { Title = "Second", Course = course, MaterialUrl = "second" } };
        db.Lessons.AddRange(lessons);
        db.Courses.Add(new Course { Title = "Keep", InstructorId = "other" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var deleted = await LessonDAO.DeleteAsync(db, lessons[0].Id);
        Assert.Equal("first", deleted?.MaterialUrl);
        db.ChangeTracker.Clear();
        var deletedWithCourse = await CourseDAO.DeleteAsync(db, course.Id);
        Assert.Equal("second", Assert.Single(deletedWithCourse).MaterialUrl);
        Assert.Empty(await db.Lessons.ToListAsync());
        Assert.Equal("Keep", (await db.Courses.SingleAsync()).Title);
    }

    [SqlServerFact]
    public async Task Conditional_material_updates_preserve_content_and_reject_stale_requests()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        int id;
        using (var scope = database.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var course = new Course { Title = "Materials", InstructorId = "owner@example.test" };
            var lesson = new Lesson { Course = course, Title = "Original", Content = "Keep content" };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();
            id = lesson.Id;
        }
        using var first = database.Services.CreateScope();
        using var second = database.Services.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<AppDbContext>();
        var b = second.ServiceProvider.GetRequiredService<AppDbContext>();
        var oldUrl = $"/LessonMaterials/Download/{id}/{new string('a', 32)}.pdf";
        Assert.True(await LessonDAO.TryUpdateMaterialAsync(a, id, "", oldUrl, "old.pdf"));
        var stale = await a.Lessons.AsNoTracking().SingleAsync(l => l.Id == id);
        var newUrl = $"/LessonMaterials/Download/{id}/{new string('b', 32)}.pdf";
        var results = await Task.WhenAll(
            LessonDAO.TryUpdateMaterialAsync(a, id, oldUrl, newUrl, "new.pdf"),
            LessonDAO.TryUpdateMaterialAsync(b, id, oldUrl, "", ""));
        Assert.Single(results.Where(result => result));
        var current = await b.Lessons.AsNoTracking().SingleAsync(l => l.Id == id);
        var expectedUrl = current.MaterialUrl;
        var expectedName = current.AttachmentName;
        stale.Title = "Updated content";
        await LessonDAO.UpdateContentAsync(a, stale);
        current = await b.Lessons.AsNoTracking().SingleAsync(l => l.Id == id);
        Assert.Equal(expectedUrl, current.MaterialUrl);
        Assert.Equal(expectedName, current.AttachmentName);
        Assert.Equal("Updated content", current.Title);
        Assert.Equal("Keep content", current.Content);
        Assert.False(await LessonDAO.TryUpdateMaterialAsync(a, id, oldUrl, "", ""));
    }
}
