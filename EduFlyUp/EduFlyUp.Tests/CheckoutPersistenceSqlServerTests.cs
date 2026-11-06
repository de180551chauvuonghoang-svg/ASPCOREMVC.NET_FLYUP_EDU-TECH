using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace EduFlyUp.Tests;

public sealed class CheckoutPersistenceSqlServerTests
{
    private static CheckoutOrder NewOrder() => new()
    {
        BuyerId = "learner-id", BuyerName = "Học viên kiểm thử", SubmissionKey = Guid.NewGuid(),
        OriginalAmount = 200000, PayableAmount = 200000, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
        Items = [new() { CourseId = 100, CourseTitle = "Tên khóa tại lúc mua", InstructorId = "instructor-at-purchase", OriginalAmount = 200000, PayableAmount = 200000 }]
    };

    private static PaymentTransaction NewTransaction(int orderId) => new()
    {
        CheckoutOrderId = orderId, Provider = "test-provider", ProviderTransactionId = Guid.NewGuid().ToString("N"),
        Amount = 200000, OccurredAtUtc = DateTime.UtcNow
    };

    [SqlServerFact]
    public async Task Migration_preserves_existing_data_and_round_trips_pending_order_snapshots()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync(useMigrations: true);
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261030234022_AddPayoutSubmissionKey");
        var course = new Course { Title = "Khóa cũ", InstructorId = "owner-old", Price = 123000 };
        db.Courses.Add(course); await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        Assert.Equal("Khóa cũ", (await db.Courses.AsNoTracking().SingleAsync()).Title);
        var order = NewOrder(); db.CheckoutOrders.Add(order); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.CheckoutOrders.Include(x => x.Items).SingleAsync();
        Assert.Equal(CheckoutOrderStatus.Pending, stored.Status); Assert.Null(stored.PaidAtUtc);
        Assert.Equal("VND", stored.Currency); Assert.NotEmpty(stored.RowVersion);
        Assert.Equal("Tên khóa tại lúc mua", Assert.Single(stored.Items).CourseTitle);
        Assert.Equal("instructor-at-purchase", Assert.Single(stored.Items).InstructorId);
        Assert.Empty(await db.Enrollments.ToListAsync());
        Assert.Empty(await db.PaymentTransactions.ToListAsync());
    }

    [SqlServerFact]
    public async Task Repeated_provider_transaction_is_rejected_across_orders()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync(useMigrations: true);
        using var scope = database.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = NewOrder(); var second = NewOrder(); db.AddRange(first, second); await db.SaveChangesAsync();
        var transaction = NewTransaction(first.Id); db.Add(transaction); await db.SaveChangesAsync();
        var duplicate = NewTransaction(second.Id); duplicate.ProviderTransactionId = transaction.ProviderTransactionId;
        db.Add(duplicate); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear(); Assert.Single(await db.PaymentTransactions.ToListAsync());
        Assert.Equal(PaymentTransactionStatus.Received, (await db.PaymentTransactions.SingleAsync()).Status);
    }

    [SqlServerFact]
    public async Task Submission_key_is_unique_for_buyer_and_course_is_unique_in_order()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = NewOrder(); db.Add(first); await db.SaveChangesAsync();
        var duplicate = NewOrder(); duplicate.SubmissionKey = first.SubmissionKey; db.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var anotherBuyer = NewOrder(); anotherBuyer.BuyerId = "another-buyer"; anotherBuyer.SubmissionKey = first.SubmissionKey;
        db.Add(anotherBuyer); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var item = NewOrder().Items.Single(); item.CheckoutOrderId = first.Id; db.Add(item);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Database_rejects_invalid_money_status_and_incomplete_coupon_snapshots()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Action<CheckoutOrder>[] invalid =
        [
            x => x.PayableAmount = -1,
            x => x.DiscountAmount = 300000,
            x => x.Status = CheckoutOrderStatus.Paid,
            x => x.Status = (CheckoutOrderStatus)99,
            x => x.Currency = "USD",
            x => x.SubmissionKey = Guid.Empty,
            x => x.Items.Single().CouponCode = "INCOMPLETE",
            x => { var i = x.Items.Single(); i.CouponId = 1; i.CouponCode = "INVALID"; i.CouponPercent = 91; },
            x => { var i = x.Items.Single(); i.DiscountAmount = 10000; i.PayableAmount = 190000; }
        ];
        foreach (var mutate in invalid)
        {
            var order = NewOrder(); mutate(order); db.Add(order);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        }
        Assert.Empty(await db.CheckoutOrders.ToListAsync());
    }

    [SqlServerFact]
    public async Task Transactions_require_positive_money_reference_and_verification_timestamp()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = NewOrder(); db.Add(order); await db.SaveChangesAsync();
        Action<PaymentTransaction>[] invalid =
        [
            x => x.Amount = 0,
            x => x.ProviderTransactionId = " ",
            x => x.Status = PaymentTransactionStatus.Verified,
            x => x.Status = (PaymentTransactionStatus)99,
            x => x.Currency = "USD"
        ];
        foreach (var mutate in invalid)
        {
            var transaction = NewTransaction(order.Id); mutate(transaction); db.Add(transaction);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        }
        var verified = NewTransaction(order.Id); verified.Status = PaymentTransactionStatus.Verified; verified.VerifiedAtUtc = DateTime.UtcNow;
        db.Add(verified); await db.SaveChangesAsync();
        Assert.NotEmpty(verified.RowVersion);
    }

    [SqlServerFact]
    public async Task Deleting_order_does_not_cascade_away_payment_audit()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync();
        using var scope = database.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = NewOrder(); db.Add(order); await db.SaveChangesAsync();
        db.Add(NewTransaction(order.Id)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.Remove(await db.CheckoutOrders.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Single(await db.PaymentTransactions.ToListAsync());
        Assert.Single(await db.CheckoutOrderItems.ToListAsync());
    }
}
