using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EduFlyUp.Tests;

public sealed class CheckoutServiceTests
{
    private static ClaimsPrincipal User(string role = "Student") => new(new ClaimsIdentity(
        [new(ClaimTypes.NameIdentifier, "server-buyer"), new(ClaimTypes.Role, role)], "Test"));
    private static CheckoutService Service(FakeRepository repo) => new(repo, TimeProvider.System, NullLogger<CheckoutService>.Instance);

    [Fact]
    public async Task Rejects_missing_key_empty_duplicate_negative_and_oversized_cart_before_database()
    {
        var repo = new FakeRepository(); var service = Service(repo);
        CheckoutInput?[] inputs = [null, new(Guid.Empty, [1]), new(Guid.NewGuid(), null), new(Guid.NewGuid(), []),
            new(Guid.NewGuid(), [1, 1]), new(Guid.NewGuid(), [-1]), new(Guid.NewGuid(), Enumerable.Range(1, 21).ToArray())];
        foreach (var input in inputs) Assert.Equal(CheckoutChange.Invalid, (await service.CreateAsync(User(), input)).Change);
        Assert.Equal(0, repo.Calls);
    }

    [Fact]
    public async Task Rejects_guest_wrong_role_and_missing_identity_before_database()
    {
        var repo = new FakeRepository(); var input = new CheckoutInput(Guid.NewGuid(), [1]);
        foreach (var user in new[] { new ClaimsPrincipal(), User("Admin"), User("Instructor"),
            new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Role, "Student")], "Test")) })
            Assert.Equal(CheckoutChange.Forbidden, (await Service(repo).CreateAsync(user, input)).Change);
        Assert.Equal(0, repo.Calls);
    }

    [Fact]
    public async Task Uses_authenticated_buyer_and_utc_time_and_preserves_submission_key()
    {
        var repo = new FakeRepository(); var key = Guid.NewGuid();
        await Service(repo).CreateAsync(User(), new(key, [2, 1]));
        Assert.Equal("server-buyer", repo.Buyer); Assert.Equal(key, repo.Input!.SubmissionKey);
        Assert.Equal(new[] { 1, 2 }, repo.Input.CourseIds); Assert.Equal(DateTimeKind.Utc, repo.Now.Kind);
    }

    [Fact]
    public async Task Database_failure_returns_unavailable_without_order_or_success()
    {
        var repo = new FakeRepository { Fail = true };
        var result = await Service(repo).CreateAsync(User(), new(Guid.NewGuid(), [1]));
        Assert.Equal(CheckoutChange.Unavailable, result.Change); Assert.Null(result.Order);
    }

    private sealed class FakeRepository : ICheckoutRepository
    {
        public int Calls; public bool Fail; public string? Buyer; public CheckoutInput? Input; public DateTime Now;
        public Task<CheckoutResult> CreateAsync(string buyerId, CheckoutInput input, DateTime utcNow)
        {
            Calls++; Buyer = buyerId; Input = input; Now = utcNow;
            if (Fail) throw new DbUpdateException("Simulated failure");
            return Task.FromResult(new CheckoutResult(CheckoutChange.Created));
        }
    }
}
