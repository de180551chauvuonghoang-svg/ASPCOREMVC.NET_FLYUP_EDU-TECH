using System.Data.Common;
using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EduFlyUp.Services;

public interface ICheckoutService
{
    Task<CheckoutResult> CreateAsync(ClaimsPrincipal user, CheckoutInput? input);
}

public sealed class CheckoutService(ICheckoutRepository repository, TimeProvider time, ILogger<CheckoutService> logger) : ICheckoutService
{
    public async Task<CheckoutResult> CreateAsync(ClaimsPrincipal user, CheckoutInput? input)
    {
        var buyerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Student") || string.IsNullOrWhiteSpace(buyerId))
            return new(CheckoutChange.Forbidden);
        if (buyerId.Length > 450 || !CheckoutPolicy.Valid(input)) return new(CheckoutChange.Invalid);
        // Sao chép input trước await để giỏ hàng không bị thay đổi trong lúc xử lý.
        var request = input! with { CourseIds = input!.CourseIds!.Order().ToArray() };
        try { return await repository.CreateAsync(buyerId, request, time.GetUtcNow().UtcDateTime); }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            logger.LogWarning(ex, "Không thể tạo checkout cho tài khoản {BuyerId}.", buyerId);
            return new(CheckoutChange.Unavailable);
        }
    }
}
