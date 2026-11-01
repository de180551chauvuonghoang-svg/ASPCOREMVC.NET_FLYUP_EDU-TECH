using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Tests;

public sealed class InstructorPayoutPolicyTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.001")]
    [InlineData("10000000000000000")]
    public void Invalid_amounts_cannot_be_saved_to_decimal_18_2(string amount)
        => Assert.False(InstructorPayoutPolicy.Valid(new(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), "Demo Bank", "12345678", "DUONG THAI DUY")));

    [Theory]
    [InlineData("123")]
    [InlineData("1234<script>")]
    [InlineData("1234\n5678")]
    public void Invalid_account_numbers_are_rejected(string account)
        => Assert.False(InstructorPayoutPolicy.Valid(new(100000, "Demo Bank", account, "DUONG THAI DUY")));

    [Fact]
    public void Input_is_trimmed_but_bank_details_are_not_rewritten()
    {
        var normalized = InstructorPayoutPolicy.Normalize(new(100000.25m, " Ngân hàng demo ", " 0012AB34 ", " Dương Thái Duy "));
        Assert.True(InstructorPayoutPolicy.Valid(normalized)); Assert.Equal("0012AB34", normalized.AccountNumber);
        Assert.False(InstructorPayoutPolicy.Valid(normalized with { BankName = new string('a', 101) }));
        Assert.False(InstructorPayoutPolicy.Valid(normalized with { AccountHolderName = "Duy\nOther" }));
    }

    [Fact]
    public void Available_balance_reserves_pending_subtracts_approved_and_preserves_unknown()
    {
        var balance = new InstructorPayoutBalance(700000, 100000, 200000, 1);
        Assert.Equal(400000m, balance.Available); Assert.False(balance.CanRequest);
        Assert.True((balance with { PendingAmount = 0, PendingCount = 0 }).CanRequest);
        Assert.Null((balance with { CreditedEarnings = null }).Available);
        Assert.False((balance with { CreditedEarnings = null }).CanRequest);
        Assert.Equal(0m, (balance with { CreditedEarnings = 50000 }).Available);
    }
}
