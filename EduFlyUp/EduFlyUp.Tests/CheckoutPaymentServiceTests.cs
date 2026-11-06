using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EduFlyUp.Tests;

public sealed class CheckoutPaymentServiceTests
{
    private static ClaimsPrincipal User(string role = "Student") => new(new ClaimsIdentity(
        [new(ClaimTypes.NameIdentifier, "buyer"), new(ClaimTypes.Role, role)], "Test"));
    private static BankTransferOptions Settings() => new() { Enabled = true, BankBin = "970415", AccountNumber = "000000123456", AccountName = " demo account " };
    private static CheckoutPaymentInput Input() => new(Guid.NewGuid(), Convert.ToBase64String(new byte[8]));
    private static CheckoutPaymentService Service(Repo repo, BankTransferOptions? settings = null) =>
        new(repo, Options.Create(settings ?? Settings()), TimeProvider.System, NullLogger<CheckoutPaymentService>.Instance);

    [Fact]
    public async Task Disabled_or_missing_bank_configuration_never_reaches_repository()
    {
        var r = new Repo();
        Assert.Equal(CheckoutPaymentChange.NotConfigured, (await Service(r, new()).PrepareAsync(User(), Input())).Change);
        var invalid = Settings(); invalid.AccountNumber = "<invalid>";
        Assert.Equal(CheckoutPaymentChange.NotConfigured, (await Service(r, invalid).PrepareAsync(User(), Input())).Change);
        Assert.Equal(0, r.Calls);
    }
    [Fact]
    public async Task Uses_only_session_buyer_and_server_receiver_preserving_leading_zeroes()
    {
        var r = new Repo(); var input = Input(); await Service(r).PrepareAsync(User(), input);
        Assert.Equal("buyer", r.Buyer); Assert.Equal(input, r.Input);
        Assert.Equal(new CheckoutPaymentReceiver("970415", "000000123456", "DEMO ACCOUNT"), r.Receiver);
        Assert.Equal(DateTimeKind.Utc, r.Now.Kind);
    }
    [Fact]
    public async Task Rejects_invalid_identity_order_and_version_before_database()
    {
        var r = new Repo();
        foreach (var user in new[] { new ClaimsPrincipal(), User("Admin"), User("Instructor") })
            Assert.Equal(CheckoutPaymentChange.Forbidden, (await Service(r).PrepareAsync(user, Input())).Change);
        foreach (var input in new CheckoutPaymentInput?[] { null, Input() with { OrderPublicId = Guid.Empty }, Input() with { Version = "bad" }, Input() with { Version = "" } })
            Assert.Equal(CheckoutPaymentChange.Invalid, (await Service(r).PrepareAsync(User(), input)).Change);
        Assert.Equal(0, r.Calls);
    }
    [Fact]
    public async Task Database_failure_returns_unavailable_without_payment_instructions()
    {
        var result = await Service(new Repo { Fail = true }).PrepareAsync(User(), Input());
        Assert.Equal(CheckoutPaymentChange.Unavailable, result.Change); Assert.Null(result.Instructions);
    }
    private sealed class Repo : ICheckoutPaymentRepository
    {
        public int Calls; public bool Fail; public string? Buyer; public CheckoutPaymentInput? Input;
        public CheckoutPaymentReceiver? Receiver; public DateTime Now;
        public Task<CheckoutPaymentResult> PrepareAsync(string buyer, CheckoutPaymentInput input, CheckoutPaymentReceiver receiver, DateTime now)
        {
            Calls++; Buyer = buyer; Input = input; Receiver = receiver; Now = now;
            if (Fail) throw new DbUpdateException("Simulated failure");
            return Task.FromResult(new CheckoutPaymentResult(CheckoutPaymentChange.Prepared));
        }
    }
}
