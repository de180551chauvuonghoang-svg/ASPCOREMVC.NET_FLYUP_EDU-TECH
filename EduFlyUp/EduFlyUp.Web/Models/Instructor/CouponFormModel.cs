using System.ComponentModel.DataAnnotations;
using EduFlyUp.BusinessObjects;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduFlyUp.Web.Models.Instructor;

public sealed class CouponFormModel : IValidatableObject
{
    public int? Id { get; set; }
    public string? Version { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập mã giảm giá.")]
    [StringLength(40, MinimumLength = 3, ErrorMessage = "Mã có từ 3 đến 40 ký tự.")]
    [Display(Name = "Mã giảm giá")]
    public string Code { get; set; } = "";
    [Range(1, 90, ErrorMessage = "Mức giảm phải từ 1% đến 90%.")]
    [Display(Name = "Mức giảm (%)")]
    public int DiscountPercent { get; set; } = 10;
    [Range(1, int.MaxValue, ErrorMessage = "Giới hạn sử dụng phải lớn hơn 0.")]
    [Display(Name = "Giới hạn lượt sử dụng")]
    public int MaxUsage { get; set; } = 100;
    [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
    [Display(Name = "Bắt đầu")]
    public DateTime? StartDate { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
    [Display(Name = "Kết thúc")]
    public DateTime? EndDate { get; set; }
    [Display(Name = "Khóa học áp dụng")]
    public int? CourseId { get; set; }
    [BindNever] public List<SelectListItem> Courses { get; set; } = [];
    [BindNever] public int UsedCount { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is { Year: < 2000 or > 2100 }) yield return new("Thời gian bắt đầu phải trong năm 2000–2100.", [nameof(StartDate)]);
        if (EndDate is { Year: < 2000 or > 2100 }) yield return new("Thời gian kết thúc phải trong năm 2000–2100.", [nameof(EndDate)]);
    }
    public CouponInput ToInput() => new(Code, DiscountPercent, MaxUsage, ToUtc(StartDate!.Value), ToUtc(EndDate!.Value), CourseId);
    private static DateTime ToUtc(DateTime value) => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.FromHours(7)).UtcDateTime;
    public static DateTime VietnamTime(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);
}

public sealed record CouponListModel(CouponPage Coupons, string? Search, DateTime UtcNow);
