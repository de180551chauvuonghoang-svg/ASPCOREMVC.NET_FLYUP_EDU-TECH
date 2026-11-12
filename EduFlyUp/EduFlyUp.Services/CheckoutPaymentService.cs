using System.Data.Common;
using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduFlyUp.Services;

public sealed class BankTransferOptions
{
    public bool Enabled { get; set; }
    public string BankBin { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string QrTemplate { get; set; } = "";
}
public interface ICheckoutPaymentService
{
    Task<CheckoutPaymentResult> PrepareAsync(ClaimsPrincipal user, CheckoutPaymentInput? input);
}
public sealed class CheckoutPaymentService(ICheckoutPaymentRepository repository, IOptions<BankTransferOptions> options,
    TimeProvider time, ILogger<CheckoutPaymentService> logger) : ICheckoutPaymentService
{
    public async Task<CheckoutPaymentResult> PrepareAsync(ClaimsPrincipal user, CheckoutPaymentInput? input)
    {
        var buyer = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Student") || string.IsNullOrWhiteSpace(buyer)) return new(CheckoutPaymentChange.Forbidden);
        if (buyer.Length > 450 || !CheckoutPaymentPolicy.Valid(input)) return new(CheckoutPaymentChange.Invalid);
        var settings = options.Value;
        var receiver = new CheckoutPaymentReceiver(settings.BankBin?.Trim() ?? "", settings.AccountNumber?.Trim() ?? "", settings.AccountName?.Trim().ToUpperInvariant() ?? "");
        if (!settings.Enabled || !CheckoutPaymentPolicy.ValidReceiver(receiver)) return new(CheckoutPaymentChange.NotConfigured);
        try { return await repository.PrepareAsync(buyer, input!, receiver, time.GetUtcNow().UtcDateTime); }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            logger.LogWarning(ex, "Không thể chốt thông tin chuyển khoản.");
            return new(CheckoutPaymentChange.Unavailable);
        }
    }
}
