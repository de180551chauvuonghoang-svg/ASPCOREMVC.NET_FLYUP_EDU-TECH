using System.ComponentModel.DataAnnotations;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Admin;

public sealed class AdminCourseFilter
{
    [StringLength(200)] public string? Search { get; set; }
    [StringLength(256)] public string? Instructor { get; set; }
    [EnumDataType(typeof(CourseLevel))] public CourseLevel? Level { get; set; }
    public bool? IsPublished { get; set; }
    public int Page { get; set; } = 1;
    public AdminCourseQuery ToQuery() => new(Search?.Trim(), Instructor?.Trim(), Level, IsPublished, Page);
}

public sealed record AdminCourseListModel(AdminCourseQuery Filter, AdminCoursePage Result);
