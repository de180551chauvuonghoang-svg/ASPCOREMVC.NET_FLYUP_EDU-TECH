using System.Data.Common;
using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EduFlyUp.Services;

public interface ICheckoutCouponService
{
    Task<CheckoutCouponResult> ApplyAsync(ClaimsPrincipal user, CheckoutCouponInput? input);
}

public sealed class CheckoutCouponService(ICheckoutCouponRepository repository, TimeProvider time,
    ILogger<CheckoutCouponService> logger) : ICheckoutCouponService
{
    public async Task<CheckoutCouponResult> ApplyAsync(ClaimsPrincipal user, CheckoutCouponInput? input)
    {
        var buyerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Student") || string.IsNullOrWhiteSpace(buyerId))
            return new(CheckoutCouponChange.Forbidden);
        if (buyerId.Length > 450 || !CheckoutCouponPolicy.Valid(input)) return new(CheckoutCouponChange.Invalid);
        try { return await repository.ApplyAsync(buyerId, input! with { Code = CheckoutCouponPolicy.Normalize(input!.Code) }, time.GetUtcNow().UtcDateTime); }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            logger.LogWarning(ex, "Không thể áp dụng mã giảm giá cho đơn hàng.");
            return new(CheckoutCouponChange.Unavailable);
        }
    }
}
