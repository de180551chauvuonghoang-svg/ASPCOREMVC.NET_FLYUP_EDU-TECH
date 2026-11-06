using System.Text.RegularExpressions;

namespace EduFlyUp.BusinessObjects;

/// <summary>Thông tin chuyển khoản đã chốt; không phải bằng chứng đã nhận tiền.</summary>
public class CheckoutPaymentIntent : BaseEntity
{
    public int CheckoutOrderId { get; set; }
    public CheckoutOrder Order { get; set; } = null!;
    public string Reference { get; set; } = string.Empty;
    public string BankBin { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed record CheckoutPaymentInput(Guid OrderPublicId, string? Version);
// Receiver chỉ lấy từ cấu hình server, không bind từ form.
public sealed record CheckoutPaymentReceiver(string BankBin, string AccountNumber, string AccountName);
public enum CheckoutPaymentChange { Prepared, AlreadyPrepared, Invalid, Forbidden, NotFound, Stale, NotPayable, NotConfigured, Unavailable }
public sealed record CheckoutPaymentInstructions(string Reference, string BankBin, string AccountNumber,
    string AccountName, decimal Amount, DateTime ExpiresAtUtc);
public sealed record CheckoutPaymentResult(CheckoutPaymentChange Change, CheckoutPaymentInstructions? Instructions = null);

public static class CheckoutPaymentPolicy
{
    public static bool Valid(CheckoutPaymentInput? input)
    {
        if (input is null || input.OrderPublicId == Guid.Empty) return false;
        try { return Convert.FromBase64String(input.Version ?? "").Length == 8; }
        catch (FormatException) { return false; }
    }
    public static bool ValidReceiver(CheckoutPaymentReceiver receiver) =>
        Regex.IsMatch(receiver.BankBin ?? "", @"\A[0-9]{6}\z") &&
        Regex.IsMatch(receiver.AccountNumber ?? "", @"\A[A-Za-z0-9]{4,19}\z") &&
        Regex.IsMatch(receiver.AccountName ?? "", @"\A[A-Z0-9](?:[A-Z0-9 ]{3,48})[A-Z0-9]\z");
}
