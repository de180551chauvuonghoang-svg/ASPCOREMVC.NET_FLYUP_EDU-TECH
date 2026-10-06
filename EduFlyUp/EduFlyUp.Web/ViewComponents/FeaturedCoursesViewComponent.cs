using EduFlyUp.Services;
using EduFlyUp.Web.Models.Courses;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.ViewComponents;

public class FeaturedCoursesViewComponent : ViewComponent
{
    private readonly ICourseService _courseService;

    public FeaturedCoursesViewComponent(ICourseService courseService)
    {
        _courseService = courseService;
    }

    public async Task<IViewComponentResult> InvokeAsync(int count = 3)
    {
        var courses = await _courseService.GetPublishedCoursesAsync();

        var featured = courses
            .Take(count)
            .Select(c => new CourseItemModel
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                ThumbnailUrl = string.IsNullOrEmpty(c.ThumbnailUrl)
                    ? "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80"
                    : c.ThumbnailUrl,
                Price = c.Price,
                Level = c.Level,
                LessonCount = c.Lessons.Count
            }).ToList();

        return View(featured);
    }
}
