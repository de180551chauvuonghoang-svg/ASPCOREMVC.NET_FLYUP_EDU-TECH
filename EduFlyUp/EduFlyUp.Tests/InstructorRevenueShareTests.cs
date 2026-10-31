using EduFlyUp.BusinessObjects;
using System.Globalization;

namespace EduFlyUp.Tests;

public sealed class InstructorRevenueShareTests
{
    [Theory]
    [InlineData("1000000", "0", "700000", "300000")]
    [InlineData("1000000", "200000", "560000", "240000")]
    [InlineData("1000000", "1000000", "0", "0")]
    [InlineData("0", "0", "0", "0")]
    [InlineData("0.05", "0", "0.04", "0.01")]
    public void Share_is_based_on_net_collected_and_preserves_every_cent(string paid, string refunded, string instructor, string platform)
    {
        static decimal Money(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
        var share = InstructorRevenueShare.Calculate(Money(paid), Money(refunded));
        Assert.NotNull(share);
        Assert.Equal(Money(instructor), share.InstructorAmount); Assert.Equal(Money(platform), share.PlatformAmount);
        Assert.Equal(Money(paid) - Money(refunded), share.InstructorAmount + share.PlatformAmount);
    }

    [Theory]
    [InlineData("-1", "0")]
    [InlineData("1", "-1")]
    [InlineData("1", "2")]
    [InlineData("1.001", "0")]
    [InlineData("1", "0.001")]
    [InlineData("10000000000000000", "0")]
    public void Invalid_or_inconsistent_payment_amounts_do_not_generate_earnings(string paid, string refunded)
        => Assert.Null(InstructorRevenueShare.Calculate(decimal.Parse(paid, CultureInfo.InvariantCulture), decimal.Parse(refunded, CultureInfo.InvariantCulture)));
}
