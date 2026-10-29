using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface ICouponService
{
    Task<CouponPage> SearchAsync(CouponOwner owner, string? search, int page);
    Task<Coupon?> GetAsync(CouponOwner owner, int id);
    Task<List<Course>> CoursesAsync(CouponOwner owner);
    Task<CouponResult> SaveAsync(CouponOwner owner, CouponInput input, int? id = null, string? version = null);
    Task<CouponResult> SetActiveAsync(CouponOwner owner, int id, bool active, string version);
}

public sealed class CouponService(ICouponRepository repository, TimeProvider time) : ICouponService
{
    public Task<CouponPage> SearchAsync(CouponOwner owner, string? search, int page) => repository.SearchAsync(owner, search?.Trim().ToUpperInvariant(), Math.Max(1, page));
    public Task<Coupon?> GetAsync(CouponOwner owner, int id) => repository.GetAsync(owner, id);
    public Task<List<Course>> CoursesAsync(CouponOwner owner) => repository.CoursesAsync(owner);

    public async Task<CouponResult> SaveAsync(CouponOwner owner, CouponInput input, int? id = null, string? version = null)
    {
        if (string.IsNullOrWhiteSpace(owner.UserId) || string.IsNullOrWhiteSpace(owner.UserName)) return new(false, "Không xác định được tài khoản giảng viên.");
        input = input with { Code = input.Code?.Trim().ToUpperInvariant() ?? "" };
        if (!Regex.IsMatch(input.Code, @"\A[A-Z0-9_-]{3,40}\z")) return new(false, "Mã gồm 3–40 ký tự chữ không dấu, số, dấu gạch ngang hoặc gạch dưới.");
        if (input.DiscountPercent is < 1 or > 90) return new(false, "Mức giảm phải từ 1% đến 90%.");
        if (input.MaxUsage < 1) return new(false, "Giới hạn sử dụng phải lớn hơn 0.");
        if (input.StartDateUtc.Kind != DateTimeKind.Utc || input.EndDateUtc.Kind != DateTimeKind.Utc ||
            input.EndDateUtc <= input.StartDateUtc || input.EndDateUtc <= time.GetUtcNow().UtcDateTime)
            return new(false, "Thời gian kết thúc phải sau thời gian bắt đầu và sau thời điểm hiện tại.");
        if (input.CourseId is <= 0) return new(false, "Khóa học không hợp lệ.");
        return Result(await repository.SaveAsync(owner, input, id, version));
    }

    public async Task<CouponResult> SetActiveAsync(CouponOwner owner, int id, bool active, string version)
        => Result(await repository.SetActiveAsync(owner, id, active, version, time.GetUtcNow().UtcDateTime));

    private static CouponResult Result(CouponChange change) => new(change == CouponChange.Saved, change switch
    {
        CouponChange.Saved => "Đã lưu mã giảm giá.",
        CouponChange.NotFound => "Không tìm thấy mã giảm giá.",
        CouponChange.DuplicateCode => "Mã giảm giá đã tồn tại. Vui lòng chọn mã khác.",
        CouponChange.InvalidCourse => "Chỉ được chọn khóa học do chính bạn sở hữu.",
        CouponChange.Stale => "Mã giảm giá đã thay đổi. Vui lòng tải lại trang trước khi sửa.",
        CouponChange.UsageConflict => "Giới hạn sử dụng không được thấp hơn số lượt đã dùng.",
        CouponChange.UsedTermsLocked => "Mã đã có lượt dùng: không thể đổi mã, mức giảm hoặc khóa học áp dụng. Bạn có thể tắt mã và tạo mã mới.",
        CouponChange.CannotActivate => "Không thể bật mã đã hết hạn hoặc hết lượt. Hãy sửa thời hạn hoặc giới hạn trước.",
        _ => "Không thể lưu mã giảm giá."
    }, change);
}
