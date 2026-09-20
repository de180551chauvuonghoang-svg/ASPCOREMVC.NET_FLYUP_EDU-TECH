using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Web.Models.Courses;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.ViewComponents;

/// <summary>
/// ViewComponent: Widget C# độc lập, tự truy vấn database mà không cần Controller.
/// 
/// Khác biệt với Partial View:
///   - Partial View: chỉ render HTML từ Model truyền vào
///   - ViewComponent: có logic C# riêng, tự gọi service/repository, trả về View
/// 
/// Cách dùng trong .cshtml:
///   @await Component.InvokeAsync("FeaturedCourses", new { count = 3 })
///   hoặc dùng Tag Helper: <vc:featured-courses count="3" />
/// 
/// Dùng khi nào?
///   - Sidebar hiển thị khóa học nổi bật
///   - Widget thống kê
///   - Menu động (tải dữ liệu từ DB)
/// </summary>
public class FeaturedCoursesViewComponent : ViewComponent
{
    private readonly IUnitOfWork _unitOfWork;

    public FeaturedCoursesViewComponent(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// InvokeAsync: method chính của ViewComponent, tương đương Action trong Controller.
    /// </summary>
    /// <param name="count">Số lượng khóa học nổi bật muốn hiển thị</param>
    public async Task<IViewComponentResult> InvokeAsync(int count = 3)
    {
        var courses = await _unitOfWork.Courses.GetPublishedCoursesAsync();

        var featured = courses
            .Take(count)
            .Select(c => new CourseItemModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                ThumbnailUrl = string.IsNullOrEmpty(c.ThumbnailUrl)
                    ? "https://placehold.co/600x400/4f46e5/ffffff?text=Course"
                    : c.ThumbnailUrl,
                Price = c.Price,
                Level = c.Level,
                LessonCount = c.Lessons.Count
            })
            .ToList();

        // Render View tại: Views/Shared/Components/FeaturedCourses/Default.cshtml
        return View(featured);
    }
}
