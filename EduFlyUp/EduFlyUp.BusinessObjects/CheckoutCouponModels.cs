using System.Text.RegularExpressions;

namespace EduFlyUp.BusinessObjects;

public sealed record CheckoutCouponInput(Guid OrderPublicId, string? Code, string? Version);
public enum CheckoutCouponChange { Applied, Invalid, Forbidden, NotFound, Stale, NotEditable, InvalidCoupon, Exhausted, Unavailable }
public sealed record CheckoutCouponSummary(Guid PublicId, decimal OriginalAmount, decimal DiscountAmount,
    decimal PayableAmount, string? Code, string Version);
public sealed record CheckoutCouponResult(CheckoutCouponChange Change, CheckoutCouponSummary? Order = null);

public static class CheckoutCouponPolicy
{
    public static string Normalize(string? code) => code?.Trim().ToUpperInvariant() ?? "";
    public static bool Valid(CheckoutCouponInput? input)
    {
        if (input is null || input.OrderPublicId == Guid.Empty) return false;
        var code = Normalize(input.Code);
        if (code.Length != 0 && !Regex.IsMatch(code, @"\A[A-Z0-9_-]{3,40}\z")) return false;
        try { return Convert.FromBase64String(input.Version ?? "").Length == 8; }
        catch (FormatException) { return false; }
    }

    // VND nguyên: làm tròn xuống tiền giảm, không vượt tỷ lệ công bố.
    public static decimal Discount(decimal original, int percent) => decimal.Floor(original * percent / 100m);
}
