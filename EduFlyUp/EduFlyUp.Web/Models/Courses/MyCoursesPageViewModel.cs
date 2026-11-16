using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Courses
{
    public class MyCoursesPageViewModel
    {
        public List<MyCourseItemModel> Courses { get; set; } = new();
        public List<Certificate> Certificates { get; set; } = new();

        public int TotalCoursesCount => Courses.Count;
        public int TotalCertificatesCount => Certificates.Count;
    }
}
