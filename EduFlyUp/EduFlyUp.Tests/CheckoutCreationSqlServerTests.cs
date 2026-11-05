using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class CheckoutCreationSqlServerTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static Course Course(decimal price = 200000) => new()
    {
        Title = "Khóa lúc tạo đơn", InstructorId = "instructor-snapshot", Price = price,
        IsPublished = true, Status = CourseStatus.Approved
    };
    private static Task<CheckoutResult> Create(AppDbContext db, string buyer, Guid key, params int[] ids)
        => CheckoutDAO.Instance.CreateAsync(db, buyer, new(key, ids), Now);

    [SqlServerFact]
    public async Task Paid_cart_uses_server_prices_and_snapshots_without_granting_access()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student");
        using var scope = data.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var paid = Course(); var free = Course(0); db.AddRange(paid, free); await db.SaveChangesAsync();
        var result = await Create(db, buyer, Guid.NewGuid(), free.Id, paid.Id);
        Assert.Equal(CheckoutChange.Created, result.Change); Assert.Equal(200000, result.Order!.PayableAmount);
        Assert.Equal(CheckoutOrderStatus.Pending, result.Order.Status); Assert.Equal(Now.AddMinutes(30), result.Order.ExpiresAtUtc);
        paid.Price = 999000; paid.Title = "Đã đổi tên"; paid.InstructorId = "new-owner"; await db.SaveChangesAsync();
        var snapshot = await db.CheckoutOrderItems.AsNoTracking().SingleAsync(i => i.CourseId == paid.Id);
        Assert.Equal(200000, snapshot.OriginalAmount); Assert.Equal("Khóa lúc tạo đơn", snapshot.CourseTitle);
        Assert.Equal("instructor-snapshot", snapshot.InstructorId);
        Assert.Empty(await db.Enrollments.ToListAsync()); Assert.Empty(await db.PaymentTransactions.ToListAsync());
    }

    [SqlServerFact]
    public async Task Same_key_replays_original_order_after_price_changes_but_rejects_other_cart()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student");
        using var scope = data.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = Course(); var b = Course(); var c = Course(); db.AddRange(a, b, c); await db.SaveChangesAsync();
        var key = Guid.NewGuid(); var first = await Create(db, buyer, key, a.Id, b.Id);
        a.Price = 300000; a.IsPublished = false; await db.SaveChangesAsync();
        var replay = await Create(db, buyer, key, b.Id, a.Id);
        Assert.Equal(CheckoutChange.AlreadySubmitted, replay.Change); Assert.Equal(first.Order, replay.Order);
        Assert.Equal(CheckoutChange.SubmissionConflict, (await Create(db, buyer, key, c.Id)).Change);
        Assert.Single(await db.CheckoutOrders.ToListAsync());
    }

    [SqlServerFact]
    public async Task Free_order_is_fulfilled_once_and_cancelled_enrollment_is_reactivated()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student");
        using var scope = data.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = Course(0); var b = Course(0); db.AddRange(a, b); await db.SaveChangesAsync();
        db.Enrollments.Add(new() { UserId = buyer, CourseId = a.Id, Status = EnrollmentStatus.Cancelled, CompletedAt = Now.AddDays(-1) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var key = Guid.NewGuid(); var result = await Create(db, buyer, key, a.Id, b.Id);
        Assert.Equal(CheckoutChange.Created, result.Change); Assert.Equal(CheckoutOrderStatus.Paid, result.Order!.Status);
        Assert.Equal(0, result.Order.PayableAmount);
        var rows = await db.Enrollments.AsNoTracking().ToListAsync(); Assert.Equal(2, rows.Count);
        Assert.All(rows, e => { Assert.Equal(EnrollmentStatus.Active, e.Status); Assert.Null(e.CompletedAt); Assert.Equal(Now, e.EnrolledAt); });
        Assert.Equal(CheckoutChange.AlreadySubmitted, (await Create(db, buyer, key, b.Id, a.Id)).Change);
        Assert.Equal(CheckoutChange.AlreadyEnrolled, (await Create(db, buyer, Guid.NewGuid(), a.Id)).Change);
        Assert.Empty(await db.PaymentTransactions.ToListAsync());
    }

    [SqlServerFact]
    public async Task Unpublished_unapproved_own_or_invalid_price_courses_are_not_purchasable()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student");
        using var scope = data.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Action<Course>[] mutations =
        [
            c => c.IsPublished = false, c => c.Status = CourseStatus.Archived,
            c => c.Status = CourseStatus.PendingApproval, c => c.InstructorId = buyer,
            c => c.InstructorId = "BUYER@EXAMPLE.TEST", c => c.Price = -1,
            c => c.Price = 1.50m, c => c.Price = CheckoutPolicy.MaxVndAmount + 1
        ];
        foreach (var mutate in mutations)
        {
            var course = Course(); mutate(course); db.Add(course); await db.SaveChangesAsync();
            Assert.Equal(CheckoutChange.CourseUnavailable, (await Create(db, buyer, Guid.NewGuid(), course.Id)).Change);
        }
        Assert.Equal(CheckoutChange.CourseUnavailable, (await Create(db, buyer, Guid.NewGuid(), int.MaxValue)).Change);
        Assert.Empty(await db.CheckoutOrders.ToListAsync());
    }

    [SqlServerFact]
    public async Task Current_account_and_role_are_checked_even_for_existing_submission()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student");
        var admin = await data.CreateUserAsync("admin@example.test");
        using var scope = data.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = Course(); db.Add(course); await db.SaveChangesAsync(); var key = Guid.NewGuid();
        Assert.Equal(CheckoutChange.Created, (await Create(db, buyer, key, course.Id)).Change);
        await db.Users.Where(u => u.Id == buyer).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(CheckoutChange.Forbidden, (await Create(db, buyer, key, course.Id)).Change);
        Assert.Equal(CheckoutChange.Forbidden, (await Create(db, admin, Guid.NewGuid(), course.Id)).Change);
        Assert.Equal(CheckoutChange.Forbidden, (await Create(db, "missing", Guid.NewGuid(), course.Id)).Change);
        await db.Users.Where(u => u.Id == buyer).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, true));
        await db.UserRoles.Where(r => r.UserId == buyer).ExecuteDeleteAsync();
        Assert.Equal(CheckoutChange.Forbidden, (await Create(db, buyer, key, course.Id)).Change);
    }

    [SqlServerFact]
    public async Task Pending_overlap_blocks_another_key_but_expired_pending_does_not()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student");
        var another = await data.CreateUserAsync("another@example.test", "Student");
        using var scope = data.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = Course(); db.Add(course); await db.SaveChangesAsync(); var key = Guid.NewGuid();
        Assert.Equal(CheckoutChange.Created, (await Create(db, buyer, key, course.Id)).Change);
        Assert.Equal(CheckoutChange.PendingExists, (await Create(db, buyer, Guid.NewGuid(), course.Id)).Change);
        Assert.Equal(CheckoutChange.Created, (await Create(db, another, key, course.Id)).Change);
        await db.CheckoutOrders.Where(o => o.BuyerId == buyer).ExecuteUpdateAsync(s => s.SetProperty(o => o.ExpiresAtUtc, Now));
        Assert.Equal(CheckoutChange.Created, (await Create(db, buyer, Guid.NewGuid(), course.Id)).Change);
        Assert.Equal(3, await db.CheckoutOrders.CountAsync());
    }

    [SqlServerFact]
    public async Task Concurrent_free_submissions_create_one_order_and_one_enrollment()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student"); int id;
        using (var setup = data.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>(); var course = Course(0);
            db.Add(course); await db.SaveChangesAsync(); id = course.Id;
        }
        using var a = data.Services.CreateScope(); using var b = data.Services.CreateScope(); var key = Guid.NewGuid();
        var results = await Task.WhenAll(Create(a.ServiceProvider.GetRequiredService<AppDbContext>(), buyer, key, id),
            Create(b.ServiceProvider.GetRequiredService<AppDbContext>(), buyer, key, id));
        Assert.Contains(results, r => r.Change == CheckoutChange.Created);
        Assert.Contains(results, r => r.Change == CheckoutChange.AlreadySubmitted);
        Assert.Equal(results[0].Order, results[1].Order);
        using var check = data.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await context.CheckoutOrders.CountAsync()); Assert.Equal(1, await context.Enrollments.CountAsync());
    }

    [SqlServerFact]
    public async Task Failed_order_save_rolls_back_free_enrollment_reactivation()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student");
        using var scope = data.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = Course(0); db.Add(course); await db.SaveChangesAsync();
        db.Enrollments.Add(new() { UserId = buyer, CourseId = course.Id, Status = EnrollmentStatus.Cancelled }); await db.SaveChangesAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(db.Database.GetConnectionString())
            .AddInterceptors(new FailOrderSave()).Options;
        await using (var failing = new AppDbContext(options))
            await Assert.ThrowsAsync<DbUpdateException>(() => Create(failing, buyer, Guid.NewGuid(), course.Id));
        Assert.Equal(EnrollmentStatus.Cancelled, (await db.Enrollments.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await db.CheckoutOrders.ToListAsync());
    }

    [SqlServerFact]
    public async Task Concurrent_paid_carts_with_different_keys_do_not_create_overlapping_live_orders()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student"); int id;
        using (var setup = data.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>(); var course = Course();
            db.Add(course); await db.SaveChangesAsync(); id = course.Id;
        }
        using var a = data.Services.CreateScope(); using var b = data.Services.CreateScope();
        var results = await Task.WhenAll(Create(a.ServiceProvider.GetRequiredService<AppDbContext>(), buyer, Guid.NewGuid(), id),
            Create(b.ServiceProvider.GetRequiredService<AppDbContext>(), buyer, Guid.NewGuid(), id));
        Assert.Contains(results, r => r.Change == CheckoutChange.Created);
        Assert.Contains(results, r => r.Change == CheckoutChange.PendingExists);
        using var check = data.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await context.CheckoutOrders.CountAsync()); Assert.Empty(await context.Enrollments.ToListAsync());
    }

    [SqlServerFact]
    public async Task Oversized_total_or_already_completed_course_rejects_whole_cart()
    {
        await using var data = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        var buyer = await data.CreateUserAsync("buyer@example.test", "Student");
        using var scope = data.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expensive = Course(CheckoutPolicy.MaxVndAmount); var small = Course(1); db.AddRange(expensive, small); await db.SaveChangesAsync();
        Assert.Equal(CheckoutChange.Invalid, (await Create(db, buyer, Guid.NewGuid(), expensive.Id, small.Id)).Change);
        db.Enrollments.Add(new() { CourseId = small.Id, UserId = buyer, Status = EnrollmentStatus.Completed, CompletedAt = Now });
        await db.SaveChangesAsync();
        Assert.Equal(CheckoutChange.AlreadyEnrolled, (await Create(db, buyer, Guid.NewGuid(), expensive.Id, small.Id)).Change);
        Assert.Empty(await db.CheckoutOrders.ToListAsync()); Assert.Empty(await db.PaymentTransactions.ToListAsync());
    }

    private sealed class FailOrderSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Simulated checkout save failure");
    }
}
