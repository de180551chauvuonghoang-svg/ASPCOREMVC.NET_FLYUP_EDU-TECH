using System.Globalization;
using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;

namespace EduFlyUp.Web.Models.Checkout;

public static class CheckoutPaymentDisplay
{
    // Template do người vận hành cấu hình; không cho phép URL/đường dẫn từ form.
    public static bool Available(BankTransferOptions options) => options.Enabled &&
        CheckoutPaymentPolicy.ValidReceiver(new(options.BankBin?.Trim() ?? "", options.AccountNumber?.Trim() ?? "", options.AccountName?.Trim().ToUpperInvariant() ?? "")) &&
        Regex.IsMatch(options.QrTemplate ?? "", @"\A[A-Za-z0-9_]{1,64}\z");

    public static string? QrUrl(BankTransferOptions options, CheckoutPaymentInstructions? payment)
    {
        if (!Available(options) || payment is null || payment.Amount <= 0 || payment.Amount > CheckoutPolicy.MaxVndAmount ||
            decimal.Truncate(payment.Amount) != payment.Amount ||
            !CheckoutPaymentPolicy.ValidReceiver(new(payment.BankBin, payment.AccountNumber, payment.AccountName)) ||
            !Regex.IsMatch(payment.Reference ?? "", @"\AFU[A-F0-9]{20}\z")) return null;
        return $"https://img.vietqr.io/image/{payment.BankBin}-{payment.AccountNumber}-{options.QrTemplate}.svg" +
            $"?amount={payment.Amount.ToString("0", CultureInfo.InvariantCulture)}&addInfo={Uri.EscapeDataString(payment.Reference!)}" +
            $"&accountName={Uri.EscapeDataString(payment.AccountName)}";
    }
}
