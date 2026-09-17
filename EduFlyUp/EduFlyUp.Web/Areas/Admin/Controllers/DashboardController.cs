using EduFlyUp.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Areas.Admin.Controllers;

[Area("Admin")] // Bắt buộc để nhận diện Area
public class DashboardController : Controller
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public IActionResult Index()
    {
        var courses = _unitOfWork.Courses.GetAll().ToList();

        ViewBag.TotalCourses = courses.Count;
        ViewBag.PublishedCourses = courses.Count(c => c.IsPublished);
        ViewBag.TotalLessons = _unitOfWork.Lessons.GetAll().Count();

        return View(courses);
    }
}
