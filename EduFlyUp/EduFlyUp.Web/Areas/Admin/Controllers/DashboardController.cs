using EduFlyUp.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")] // ← Chỉ Admin mới vào được — [Authorize] ở class áp dụng cho tất cả action
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
