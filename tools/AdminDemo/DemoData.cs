using System.Text.Json.Serialization;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdminDemo;

internal sealed record DemoAccount(string Id, string Email, string Role);
internal sealed class DemoSession(string server, string database, string password)
{
    public string Server { get; } = server;
    public string Database { get; } = database;
    public string Password { get; } = password;
    public string BaseUrl { get; set; } = "";
    public int ProcessId { get; set; }
    public string StopFile { get; set; } = "";
    public Dictionary<string, DemoAccount> Accounts { get; } = [];
    public Dictionary<string, int> Courses { get; } = [];
    [JsonIgnore] public string Connection => new SqlConnectionStringBuilder
    {
        DataSource = Server, InitialCatalog = Database, IntegratedSecurity = true,
        TrustServerCertificate = true, ConnectTimeout = 10, MultipleActiveResultSets = true
    }.ConnectionString;
    public AppDbContext OpenDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Connection).Options);
}

internal sealed record DemoCheck(string Name, bool Passed, double Seconds);
internal sealed class DemoReport(DateTimeOffset startedUtc)
{
    public DateTimeOffset StartedUtc { get; } = startedUtc;
    public DateTimeOffset? FinishedUtc { get; set; }
    public string? Database { get; set; }
    public string? BaseUrl { get; set; }
    public bool Passed { get; set; }
    public bool BrowserVerified { get; set; }
    public List<DemoCheck> Checks { get; } = [];
    public string? Error { get; set; }
}

internal static class DemoData
{
    public static async Task PrepareAsync(DemoSession session)
    {
        const string prefix = "FlyUpAdminDemo_";
        if (!session.Database.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(session.Database[prefix.Length..], "N", out _))
            throw new InvalidOperationException("Only a fresh GUID demo database is allowed.");
        var master = new SqlConnectionStringBuilder(session.Connection) { InitialCatalog = "master" };
        await using (var connection = new SqlConnection(master.ConnectionString))
        {
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT DB_ID(@name)", connection);
            command.Parameters.AddWithValue("@name", session.Database);
            if (await command.ExecuteScalarAsync() is not DBNull)
                throw new InvalidOperationException("Demo database already exists. Refusing to seed it.");
        }
        var collection = new ServiceCollection();
        collection.AddLogging(log => log.SetMinimumLevel(LogLevel.Warning));
        collection.AddDbContext<AppDbContext>(o => o.UseSqlServer(session.Connection));
        collection.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
        await using var provider = collection.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await DataSeeder.SeedAsync(db); // Use the actual migration/startup seed path.
        await DataSeeder.SeedRolesAndAdminAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(), users);
        var defaultAdmin = await users.FindByEmailAsync("admin@eduflyup.vn")
            ?? throw new InvalidOperationException("Startup Admin seed failed.");
        defaultAdmin.IsActive = false; Ensure(await users.UpdateAsync(defaultAdmin));
        foreach (var (key, role) in new[]
        {
            ("admin-a", "Admin"), ("admin-b", "Admin"), ("instructor-a", "Instructor"), ("instructor-b", "Instructor"),
            ("student-active", "Student"), ("student-completed", "Student"), ("student-cancelled", "Student"),
            ("student-none", "Student"), ("student-blocked", "Student"), ("teacher", "Teacher")
        }.Concat(Enumerable.Range(1, 12).Select(i => ("student-page-" + i, "Student"))))
        {
            var email = key + "@demo.example.test";
            var user = new ApplicationUser { UserName = email, Email = email, FullName = "DEMO " + key,
                EmailConfirmed = true, IsActive = key != "student-blocked" };
            Ensure(await users.CreateAsync(user, session.Password)); Ensure(await users.AddToRoleAsync(user, role));
            session.Accounts.Add(key, new(user.Id, email, role));
        }
        for (var i = 1; i <= 13; i++)
        {
            var key = i == 1 ? "a" : i == 2 ? "b" : i == 3 ? "empty" : "page-" + i;
            var course = new Course { Title = "DEMO course " + key, Description = "Isolated integration demo",
                InstructorId = session.Accounts[i == 2 ? "instructor-b" : "instructor-a"].Email,
                IsPublished = i == 1 || (i > 3 && i % 2 == 0), Price = i % 2 == 0 ? 250000 : 0,
                Level = (CourseLevel)(i % 3 + 1), Lessons = i == 3 ? [] :
                    [new Lesson { Title = "DEMO lesson " + key, Content = "PRIVATE_ADMIN_DEMO_CONTENT", VideoUrl = "https://example.test/demo.mp4", Order = 1 }]
            };
            db.Courses.Add(course); await db.SaveChangesAsync(); session.Courses.Add(key, course.Id);
        }
        foreach (var (key, status) in new[] { ("student-active", EnrollmentStatus.Active),
            ("student-completed", EnrollmentStatus.Completed), ("student-cancelled", EnrollmentStatus.Cancelled) })
            db.Enrollments.Add(new Enrollment { UserId = session.Accounts[key].Id, CourseId = session.Courses["a"], Status = status });
        await db.SaveChangesAsync();
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Demo Identity seed failed: " + string.Join(", ", result.Errors.Select(e => e.Code)));
    }
}
