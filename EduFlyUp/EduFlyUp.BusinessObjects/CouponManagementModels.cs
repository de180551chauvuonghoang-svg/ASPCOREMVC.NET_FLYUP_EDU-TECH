using System.Globalization;

namespace EduFlyUp.BusinessObjects;

public sealed record CouponOwner(string UserId, string UserName)
{
    public bool Owns(string owner) => !string.IsNullOrWhiteSpace(owner) &&
        (string.Equals(owner, UserId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(owner, UserName, StringComparison.OrdinalIgnoreCase));
}

public sealed record CouponInput(string Code, int DiscountPercent, int MaxUsage,
    DateTime StartDateUtc, DateTime EndDateUtc, int? CourseId);

public enum CouponChange { Saved, NotFound, DuplicateCode, InvalidCourse, Stale, UsageConflict, UsedTermsLocked, CannotActivate }
public sealed record CouponResult(bool Success, string Message, CouponChange? Change = null);
public sealed record CouponPage(IReadOnlyList<Coupon> Items, int Page, int TotalCount)
{
    public const int PageSize = 10;
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}

public static class CouponPresentation
{
    public static string Version(Coupon coupon) => (coupon.UpdatedAt ?? coupon.CreatedAt).Ticks.ToString(CultureInfo.InvariantCulture);
    public static string Status(Coupon coupon, DateTime utcNow) => !coupon.IsActive ? "Đã tắt"
        : coupon.EndDate <= utcNow ? "Hết hạn"
        : coupon.UsedCount >= coupon.MaxUsage ? "Hết lượt"
        : coupon.StartDate > utcNow ? "Chưa bắt đầu" : "Đang áp dụng";
}
