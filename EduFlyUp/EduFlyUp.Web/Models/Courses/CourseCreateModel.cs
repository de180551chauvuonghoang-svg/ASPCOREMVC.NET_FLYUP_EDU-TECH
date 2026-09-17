using System.ComponentModel.DataAnnotations;
using EduFlyUp.Domain.Enums;

namespace EduFlyUp.Web.Models.Courses;

public class CourseCreateModel
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề khóa học.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5 đến 200 ký tự.")]
    [Display(Name = "Tiêu đề khóa học")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả chi tiết.")]
    [MaxLength(2000, ErrorMessage = "Mô tả tối đa 2000 ký tự.")]
    [Display(Name = "Mô tả khóa học")]
    public string Description { get; set; } = string.Empty;

    [Url(ErrorMessage = "Đường dẫn hình ảnh không hợp lệ.")]
    [Display(Name = "Link ảnh bìa (Thumbnail URL)")]
    public string? ThumbnailUrl { get; set; }

    [Range(0, 100000000, ErrorMessage = "Học phí phải từ 0 đến 100.000.000 VNĐ.")]
    [Display(Name = "Học phí (VNĐ)")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn cấp độ.")]
    [Display(Name = "Cấp độ")]
    public CourseLevel Level { get; set; } = CourseLevel.Beginner;

    [Display(Name = "Công khai khóa học ngay")]
    public bool IsPublished { get; set; } = true;
}
