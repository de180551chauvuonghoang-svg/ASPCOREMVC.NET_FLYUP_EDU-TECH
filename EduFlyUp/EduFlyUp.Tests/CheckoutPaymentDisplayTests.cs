using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Checkout;

namespace EduFlyUp.Tests;

public sealed class CheckoutPaymentDisplayTests
{
    [Fact]
    public void Quicklink_uses_snapshot_and_rejects_unsafe_or_incomplete_configuration_and_payload()
    {
        var options = new BankTransferOptions { Enabled = true, BankBin = "970415", AccountNumber = "00001234", AccountName = "CONFIG NAME", QrTemplate = "my_template1" };
        var payment = new CheckoutPaymentInstructions("FU0123456789ABCDEF0123", "970436", "00005678", "SNAPSHOT NAME", 123456, DateTime.UtcNow.AddMinutes(5));
        var url = CheckoutPaymentDisplay.QrUrl(options, payment);
        Assert.Equal("https://img.vietqr.io/image/970436-00005678-my_template1.svg?amount=123456&addInfo=FU0123456789ABCDEF0123&accountName=SNAPSHOT%20NAME", url);
        Assert.Null(CheckoutPaymentDisplay.QrUrl(options, payment with { Amount = 1.5m }));
        Assert.Null(CheckoutPaymentDisplay.QrUrl(options, payment with { Amount = 0 }));
        Assert.Null(CheckoutPaymentDisplay.QrUrl(options, payment with { Amount = CheckoutPolicy.MaxVndAmount + 1 }));
        Assert.Null(CheckoutPaymentDisplay.QrUrl(options, payment with { Reference = "../attack?x=1" }));
        Assert.Null(CheckoutPaymentDisplay.QrUrl(options, payment with { AccountNumber = "bad/account" }));
        foreach (var template in new[] { "", "../qr_only", "https://evil.test/qr", "qr.svg?x=1", new string('a', 65) })
        {
            options.QrTemplate = template;
            Assert.False(CheckoutPaymentDisplay.Available(options)); Assert.Null(CheckoutPaymentDisplay.QrUrl(options, payment));
        }
        options.QrTemplate = "my_template1"; options.Enabled = false;
        Assert.Null(CheckoutPaymentDisplay.QrUrl(options, payment));
        options.Enabled = true; options.BankBin = "";
        Assert.Null(CheckoutPaymentDisplay.QrUrl(options, payment));
    }
}
