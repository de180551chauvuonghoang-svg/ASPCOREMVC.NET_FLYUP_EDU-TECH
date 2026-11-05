using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EduFlyUp.Tests;

public sealed class CheckoutCouponServiceTests
{
    private static ClaimsPrincipal User(string role = "Student") => new(new ClaimsIdentity(
        [new(ClaimTypes.NameIdentifier, "buyer"), new(ClaimTypes.Role, role)], "Test"));
    private static CheckoutCouponService Service(Repo r) => new(r, TimeProvider.System, NullLogger<CheckoutCouponService>.Instance);
    private static CheckoutCouponInput Input(string? code = "DEMO10") => new(Guid.NewGuid(), code, Convert.ToBase64String(new byte[8]));

    [Fact]
    public async Task Validates_identity_code_order_and_version_before_repository()
    {
        var r = new Repo(); var service = Service(r);
        foreach (var user in new[] { new ClaimsPrincipal(), User("Admin"), User("Instructor") })
            Assert.Equal(CheckoutCouponChange.Forbidden, (await service.ApplyAsync(user, Input())).Change);
        CheckoutCouponInput?[] invalid = [null, Input() with { OrderPublicId = Guid.Empty }, Input("A"), Input("<script>"),
            Input() with { Version = "invalid" }, Input() with { Version = Convert.ToBase64String([1, 2]) }];
        foreach (var input in invalid) Assert.Equal(CheckoutCouponChange.Invalid, (await service.ApplyAsync(User(), input)).Change);
        Assert.Equal(0, r.Calls);
    }

    [Fact]
    public async Task Normalizes_code_and_uses_session_buyer_and_utc_time_including_removal()
    {
        var r = new Repo(); var input = Input(" demo10 "); await Service(r).ApplyAsync(User(), input);
        Assert.Equal("DEMO10", r.Input!.Code); Assert.Equal(input.OrderPublicId, r.Input.OrderPublicId);
        Assert.Equal(input.Version, r.Input.Version); Assert.Equal("buyer", r.Buyer); Assert.Equal(DateTimeKind.Utc, r.Now.Kind);
        await Service(r).ApplyAsync(User(), Input(null)); Assert.Equal("", r.Input.Code);
    }

    [Fact]
    public async Task Database_failure_returns_unavailable_without_success_summary()
    {
        var result = await Service(new Repo { Fail = true }).ApplyAsync(User(), Input());
        Assert.Equal(CheckoutCouponChange.Unavailable, result.Change); Assert.Null(result.Order);
    }

    [Theory]
    [InlineData(101, 10, 10)]
    [InlineData(1, 90, 0)]
    [InlineData(999, 90, 899)]
    public void Discounts_round_down_to_whole_vnd(decimal original, int percent, decimal expected)
        => Assert.Equal(expected, CheckoutCouponPolicy.Discount(original, percent));

    private sealed class Repo : ICheckoutCouponRepository
    {
        public int Calls; public bool Fail; public string? Buyer; public DateTime Now; public CheckoutCouponInput? Input;
        public Task<CheckoutCouponResult> ApplyAsync(string buyerId, CheckoutCouponInput input, DateTime utcNow)
        {
            Calls++; Buyer = buyerId; Input = input; Now = utcNow;
            if (Fail) throw new DbUpdateException("Simulated failure");
            return Task.FromResult(new CheckoutCouponResult(CheckoutCouponChange.Applied));
        }
    }
}
