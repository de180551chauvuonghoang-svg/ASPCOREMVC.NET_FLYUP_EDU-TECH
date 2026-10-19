using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Tests;

public sealed class AdminCourseQueryTests
{
    [Fact]
    public void Production_queries_translate_to_sql_server_with_filters_paging_and_optional_owner()
    {
        // Compile the actual repository queries with SQL Server's provider; no
        // server connection is opened. InMemory alone cannot verify translation.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=TranslationOnly;Integrated Security=True").Options);
        var filtered = AdminCourseRepository.Filter(db, new AdminCourseQuery("needle", "owner", CourseLevel.Advanced, false));
        var sql = AdminCourseRepository.PageQuery(db, filtered, 10, 10).ToQueryString();
        Assert.Contains("WHERE", sql); Assert.Contains("[Title]", sql);
        Assert.Contains("[Level]", sql); Assert.Contains("[IsPublished]", sql);
        Assert.Contains("OFFSET", sql); Assert.Contains("FETCH NEXT", sql);
        Assert.Contains("LEFT JOIN", sql); Assert.Contains("COUNT(*)", sql);
        var ownersSql = AdminCourseRepository.InstructorQuery(db).ToQueryString();
        Assert.Contains("DISTINCT", ownersSql); Assert.Contains("LEFT JOIN", ownersSql);
    }
}
