using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EduFlyUp.Tests;

public sealed class SePayWebhookTests
{
    [Theory]
    [InlineData("demo-key", null, "in", 1)]
    [InlineData("demo-key", "Apikey wrong-key", "in", 1)]
    [InlineData("demo-key", "Bearer demo-key", "in", 1)]
    [InlineData(null, "Apikey demo-key", "in", 1)]
    [InlineData("demo-key", "Apikey demo-key", "out", 1)]
    [InlineData("demo-key", "Apikey demo-key", "in", 0)]
    public async Task Rejects_invalid_webhooks_before_reading_orders(string? key, string? header, string type, long id)
    {
        var service = BuildService(null!, key);
        var payload = new SePayWebhookPayload { id = id, transferAmount = 200000, transferType = type, content = "EDUPAY123456", accountNumber = "00001234" };
        Assert.False(await service.ProcessSePayWebhookAsync(header, payload));
    }

    [SqlServerFact]
    public async Task Valid_webhook_records_receipt_and_enrollment_once_with_snapshot()
    {
        await using var database = await AdminUserSqlServerTests.SqlDatabase.CreateAsync(useMigrations: true);
        var ownerId = await database.CreateUserAsync("owner@example.test", "Instructor");
        var newOwnerId = await database.CreateUserAsync("new@example.test", "Instructor");
        using var seed = database.Services.CreateScope();
        var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = new Course { Title = "Khóa thử nghiệm", InstructorId = "owner@example.test", Price = 200000 };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var first = new Order { CourseId = course.Id, UserId = "student-1", Amount = 180000, OrderCode = "EDUPAY123456" };
        var second = new Order { CourseId = course.Id, UserId = "student-2", Amount = 180000, OrderCode = "EDUPAY654321" };
        var legacy = new Order { CourseId = course.Id, UserId = "student-3", Amount = 180000, OrderCode = "EDUPAY555555", Status = OrderStatus.Paid };
        db.Orders.AddRange(first, second, legacy);
        await db.SaveChangesAsync();

        SePayWebhookPayload Payment(string code, long id = 123, decimal amount = 180000, string account = "00001234")
            => new() { id = id, content = code, transferAmount = amount, accountNumber = account, transferType = "in" };
        async Task<bool> Send(SePayWebhookPayload payment)
        {
            using var scope = database.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await BuildService(new OrderRepository(context), "demo-key").ProcessSePayWebhookAsync("Apikey demo-key", payment);
        }

        Assert.False(await Send(Payment(first.OrderCode, amount: 179999)));
        Assert.False(await Send(Payment(first.OrderCode, amount: 180001)));
        Assert.False(await Send(Payment(first.OrderCode, account: "1234")));
        Assert.False(await Send(Payment(first.OrderCode + " " + second.OrderCode)));
        Assert.False(await Send(Payment(legacy.OrderCode, 500)));
        Assert.All(await Task.WhenAll(Send(Payment(first.OrderCode)), Send(Payment(first.OrderCode))), result => Assert.True(result));
        Assert.False(await Send(Payment(second.OrderCode)));
        Assert.False(await Send(Payment(first.OrderCode, 124)));

        await db.Courses.ExecuteUpdateAsync(update => update.SetProperty(c => c.InstructorId, newOwnerId).SetProperty(c => c.Price, 999999));
        var receipt = await db.SePayReceipts.AsNoTracking().SingleAsync();
        Assert.Equal(180000m, receipt.Amount);
        Assert.Equal(ownerId, receipt.InstructorId);
        Assert.Equal(123, receipt.TransactionId);
        Assert.Equal(first.Id, receipt.OrderId);
        Assert.Equal(OrderStatus.Paid, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == first.Id)).Status);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == second.Id)).Status);
        Assert.Equal(first.UserId, (await db.Enrollments.AsNoTracking().SingleAsync()).UserId);

        var earnings = new SettlementInstructorEarningsSource(new SePayInstructorSettlementSource());
        var owner = new InstructorIdentity(ownerId, "renamed@example.test");
        var newOwner = new InstructorIdentity(newOwnerId, "new@example.test");
        var statement = Assert.IsType<InstructorRevenueStatement>(await earnings.ReadStatementAsync(db, owner));
        Assert.Equal(126000m, statement.TotalInstructor);
        Assert.Equal(54000m, statement.TotalPlatform);
        Assert.Equal(0m, await earnings.CreditedEarningsAsync(db, newOwner));

        var payout = new InstructorPayoutService(new InstructorPayoutRepository(db, earnings));
        var request = new InstructorPayoutInput(125000, "Demo Bank", "000012345678", "DEMO HOLDER", Guid.NewGuid());
        Assert.Equal(InstructorPayoutChange.InsufficientBalance, (await payout.CreateAsync(owner, request with { Amount = 126001 })).Change);
        Assert.Equal(InstructorPayoutChange.Created, (await payout.CreateAsync(owner, request)).Change);
        Assert.Equal(1000m, (await payout.BalanceAsync(owner)).Available);
    }

    private static PaymentService BuildService(IOrderRepository orders, string? key)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["SePay:ApiKey"] = key, ["SePay:BankAccountNumber"] = "00001234" }).Build();
        return new PaymentService(orders, null!, null!, configuration, NullLogger<PaymentService>.Instance);
    }
}
