using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using EduFlyUp.Services;

namespace EduFlyUp.Tests;

public sealed class CouponServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData("ok", 50, 1, 1, false)]
    [InlineData("ABC", 0, 1, 1, false)]
    [InlineData("ABC", 91, 1, 1, false)]
    [InlineData("ABC", 90, 0, 1, false)]
    [InlineData("ABC", 90, 1, 0, false)]
    [InlineData(" mã việt ", 10, 1, 1, false)]
    [InlineData(" abc-10 ", 90, 1, 1, true)]
    public async Task Validation_enforces_code_discount_limit_usage_and_dates(string code, int discount, int maxUsage, int days, bool valid)
    {
        var repository = new CaptureRepository();
        var service = new CouponService(repository, new FixedTime());
        var result = await service.SaveAsync(new("id", "owner@example.test"), new(code, discount, maxUsage, Now, Now.AddDays(days), null));
        Assert.Equal(valid, result.Success);
        Assert.Equal(valid ? 1 : 0, repository.Writes);
        if (valid) Assert.Equal("ABC-10", repository.Input?.Code);
    }
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class CaptureRepository : ICouponRepository
    {
        public int Writes { get; private set; }
        public CouponInput? Input { get; private set; }
        public Task<CouponChange> SaveAsync(CouponOwner owner, CouponInput input, int? id, string? version)
        { Writes++; Input = input; return Task.FromResult(CouponChange.Saved); }
        public Task<CouponChange> SetActiveAsync(CouponOwner owner, int id, bool active, string version, DateTime utcNow) => throw new NotSupportedException();
        public Task<CouponPage> SearchAsync(CouponOwner owner, string? search, int page) => throw new NotSupportedException();
        public Task<Coupon?> GetAsync(CouponOwner owner, int id) => throw new NotSupportedException();
        public Task<List<Course>> CoursesAsync(CouponOwner owner) => throw new NotSupportedException();
    }
}
