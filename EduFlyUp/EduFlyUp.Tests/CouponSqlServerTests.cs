using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class CouponSqlServerTests
{
    private static CouponService Service(AppDbContext db) => new(new CouponRepository(db), TimeProvider.System);
    private static CouponInput Input(string code = "TEST50", int? courseId = null) => new(code, 50, 10, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(7), courseId);
    private static readonly CouponOwner Owner = new("owner-id", "owner@example.test");

    [SqlServerFact]
    public async Task Concurrent_creates_normalize_code_and_allow_only_one_globally()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var first = database.Services.CreateScope(); using var second = database.Services.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<AppDbContext>();
        var b = second.ServiceProvider.GetRequiredService<AppDbContext>();
        var results = await Task.WhenAll(Service(a).SaveAsync(Owner, Input(" test50 ")),
            Service(b).SaveAsync(new("other-id", "other@example.test"), Input()));
        Assert.Single(results.Where(r => r.Success));
        Assert.Single(results.Where(r => r.Change == CouponChange.DuplicateCode));
        Assert.Equal("TEST50", (await a.Coupons.AsNoTracking().SingleAsync()).Code);
    }

    [SqlServerFact]
    public async Task Own_course_scope_usage_history_and_stale_forms_are_enforced()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mine = new Course { Title = "Mine", InstructorId = Owner.UserName };
        var other = new Course { Title = "Other", InstructorId = "other" };
        db.Courses.AddRange(mine, other); await db.SaveChangesAsync();
        var service = Service(db);
        Assert.Equal(CouponChange.InvalidCourse, (await service.SaveAsync(Owner, Input(courseId: other.Id))).Change);
        Assert.True((await service.SaveAsync(Owner, Input(courseId: mine.Id))).Success);
        var stored = await db.Coupons.AsNoTracking().SingleAsync();
        Assert.Equal(Owner.UserId, stored.InstructorId);
        Assert.Equal(0, stored.UsedCount);
        Assert.Equal(CouponChange.NotFound, (await service.SetActiveAsync(new("x", "x@test"), stored.Id, false, CouponPresentation.Version(stored))).Change);
        await db.Coupons.Where(c => c.Id == stored.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, 2));
        var version = CouponPresentation.Version(stored);
        Assert.Equal(CouponChange.UsageConflict, (await service.SaveAsync(Owner, Input(courseId: mine.Id) with { MaxUsage = 1 }, stored.Id, version)).Change);
        Assert.Equal(CouponChange.UsedTermsLocked, (await service.SaveAsync(Owner, Input(courseId: mine.Id) with { DiscountPercent = 20 }, stored.Id, version)).Change);
        Assert.True((await service.SaveAsync(Owner, Input(courseId: mine.Id) with { MaxUsage = 20 }, stored.Id, version)).Success);
        Assert.Equal(CouponChange.Stale, (await service.SetActiveAsync(Owner, stored.Id, false, version)).Change);
        stored = (await service.GetAsync(Owner, stored.Id))!;
        Assert.True((await service.SetActiveAsync(Owner, stored.Id, false, CouponPresentation.Version(stored))).Success);
        stored = (await service.GetAsync(Owner, stored.Id))!;
        Assert.False(stored.IsActive); Assert.Equal(2, stored.UsedCount); Assert.Equal(20, stored.MaxUsage);
        await db.Coupons.Where(c => c.Id == stored.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.EndDate, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(CouponChange.CannotActivate, (await service.SetActiveAsync(Owner, stored.Id, true, CouponPresentation.Version(stored))).Change);
    }

    [SqlServerFact]
    public async Task Lists_only_owned_coupons_with_search_and_paging_and_accepts_legacy_owner()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var i = 0; i < 12; i++) db.Coupons.Add(new Coupon { Code = "MINE" + i, InstructorId = i % 2 == 0 ? Owner.UserId : Owner.UserName });
        db.Coupons.Add(new Coupon { Code = "OTHER", InstructorId = "another-id" });
        await db.SaveChangesAsync();
        var service = Service(db);
        var first = await service.SearchAsync(Owner, null, 1);
        Assert.Equal(12, first.TotalCount); Assert.Equal(10, first.Items.Count);
        var second = await service.SearchAsync(Owner, " mine ", int.MaxValue);
        Assert.Equal(2, second.Page); Assert.Equal(2, second.Items.Count);
        Assert.All(second.Items, c => Assert.True(Owner.Owns(c.InstructorId)));
        Assert.Null(await service.GetAsync(Owner, (await db.Coupons.SingleAsync(c => c.Code == "OTHER")).Id));
    }
}
