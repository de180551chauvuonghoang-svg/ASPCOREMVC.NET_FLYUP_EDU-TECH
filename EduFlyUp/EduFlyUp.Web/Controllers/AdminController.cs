using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ICourseService _courseService;
    private readonly ILessonService _lessonService;

    public AdminController(ICourseService courseService, ILessonService lessonService)
    {
        _courseService = courseService;
        _lessonService = lessonService;
    }

    public async Task<IActionResult> Index()
    {
        var courses = (await _courseService.GetAllCoursesAsync()).ToList();

        ViewBag.TotalCourses = courses.Count;
        ViewBag.PublishedCourses = courses.Count(c => c.IsPublished);
        ViewBag.TotalLessons = await _lessonService.GetTotalLessonsCountAsync();

        return View(courses);
    }

    public IActionResult Dashboard()
    {
        return RedirectToAction(nameof(Index));
    }
}
