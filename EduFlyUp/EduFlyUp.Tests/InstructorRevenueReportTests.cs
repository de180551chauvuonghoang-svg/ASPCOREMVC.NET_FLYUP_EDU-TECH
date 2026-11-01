using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;

namespace EduFlyUp.Tests;

public sealed class InstructorRevenueReportTests
{
    [Fact]
    public async Task Search_trims_ignores_case_filters_before_totals_and_does_not_infer_missing_fields()
    {
        using var db = new AppDbContext();
        var owner = new InstructorIdentity("owner-id", "owner@example.test");
        var service = new InstructorRevenueService(new InstructorRevenueRepository(db, new InstructorSettlementEarningsTests.TestSettlements([
            new("order-1", owner.UserId, 1000000, 200000, new(1, "C# Course", "Student One", 1200000, null)),
            new("order-2", owner.UserId, 500000, 0),
            new("order-3", owner.UserId, 3000000, 3000000, new(2, "Other", "Student Two", 3000000, null))])));
        var result = await service.ReportAsync(owner, int.MaxValue, "  c# COURSE  ");
        Assert.NotNull(result); Assert.Equal(1, result.TotalCount); Assert.Equal(1, result.Page);
        Assert.Equal("c# COURSE", result.Search); Assert.Equal(800000m, result.TotalNet); Assert.Equal(560000m, result.TotalInstructor);
        Assert.Equal(2, (await service.ReportAsync(owner, 1, "STUDENT"))!.TotalCount);
        Assert.Equal(350000m, (await service.ReportAsync(owner, 1, "ORDER-2"))!.TotalInstructor);
        var empty = await service.ReportAsync(owner, 1, "nomatch");
        Assert.NotNull(empty); Assert.Empty(empty.Items); Assert.Equal(0, empty.TotalNet);
    }

    [Fact]
    public async Task Report_uses_verified_receipts_pages_and_orders_without_inventing_missing_metadata()
    {
        using var db = new AppDbContext();
        var owner = new InstructorIdentity("owner-id", "owner@example.test");
        var rows = Enumerable.Range(0, 12).Select(i => new InstructorSettledReceipt($"receipt-{i:D2}", owner.UserId, 100, 0,
            i == 0 ? null : new(1, "Course", "Student", 150, new DateTime(2026, 11, 1).AddMinutes(i)))).ToArray();
        var service = new InstructorRevenueService(new InstructorRevenueRepository(db, new InstructorSettlementEarningsTests.TestSettlements(rows)));
        var first = await service.ReportAsync(owner, -1); var last = await service.ReportAsync(owner, int.MaxValue);
        Assert.NotNull(first); Assert.NotNull(last); Assert.Equal(12, first.TotalCount); Assert.Equal(10, first.Items.Count);
        Assert.Equal(1, first.Page); Assert.Equal(2, last.Page); Assert.Equal(2, last.Items.Count);
        Assert.Equal("receipt-11", first.Items[0].Receipt.Reference); Assert.Equal("receipt-00", last.Items[^1].Receipt.Reference);
        Assert.Null(last.Items[^1].Receipt.Info);
        Assert.Equal(1200m, first.TotalNet); Assert.Equal(840m, first.TotalInstructor); Assert.Equal(360m, first.TotalPlatform);
        Assert.Equal(first.TotalInstructor, last.TotalInstructor);
        Assert.Null(await service.ReportAsync(new("", owner.UserName), 1));
        var missing = new InstructorRevenueService(new InstructorRevenueRepository(db, new UnavailableInstructorSettlementSource()));
        Assert.Null(await missing.ReportAsync(owner, 1));
        var empty = await new InstructorRevenueService(new InstructorRevenueRepository(db, new InstructorSettlementEarningsTests.TestSettlements([]))).ReportAsync(owner, 1);
        Assert.NotNull(empty); Assert.Empty(empty.Items); Assert.Equal(0, empty.TotalInstructor);
    }
}
