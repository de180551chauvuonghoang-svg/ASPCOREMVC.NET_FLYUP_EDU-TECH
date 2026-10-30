namespace EduFlyUp.BusinessObjects;

public sealed record InstructorPayoutInput(decimal Amount, string BankName, string AccountNumber, string AccountHolderName);
public sealed record InstructorPayoutBalance(decimal? CreditedEarnings, decimal PendingAmount, decimal ApprovedAmount, int PendingCount)
{
    public decimal? Available => CreditedEarnings.HasValue ? Math.Max(0, CreditedEarnings.Value - PendingAmount - ApprovedAmount) : null;
    public bool CanRequest => Available > 0 && PendingCount == 0;
}
public enum InstructorPayoutChange { Created, Invalid, EarningsUnavailable, PendingExists, InsufficientBalance }
public sealed record InstructorPayoutResult(InstructorPayoutChange Change, string Message);

public static class InstructorPayoutPolicy
{
    public static InstructorPayoutInput Normalize(InstructorPayoutInput input)
        => input with { BankName = input.BankName?.Trim() ?? "", AccountNumber = input.AccountNumber?.Trim() ?? "", AccountHolderName = input.AccountHolderName?.Trim() ?? "" };

    public static bool Valid(InstructorPayoutInput input)
        => input.Amount > 0 && input.Amount <= 9999999999999999.99m && decimal.Round(input.Amount, 2) == input.Amount
            && ValidText(input.BankName, 100) && ValidText(input.AccountHolderName, 100)
            && input.AccountNumber.Length is >= 4 and <= 34 && input.AccountNumber.All(c => char.IsAsciiLetterOrDigit(c));

    private static bool ValidText(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl);
}
