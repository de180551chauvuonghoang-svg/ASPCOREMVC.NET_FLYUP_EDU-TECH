using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Courses
{
    public class MyCoursesPageViewModel
    {
        /// <summary>
        /// Danh sách khóa học đang học (chưa thi tốt nghiệp hoặc chưa nhận chứng chỉ)
        /// </summary>
        public List<MyCourseItemModel> InProgressCourses { get; set; } = new();

        /// <summary>
        /// Danh sách khóa học đã hoàn thành xuất sắc & đã nhận chứng chỉ (vẫn có thể vào học lại)
        /// </summary>
        public List<MyCourseItemModel> CompletedCourses { get; set; } = new();

        /// <summary>
        /// Toàn bộ các khóa học sở hữu
        /// </summary>
        public List<MyCourseItemModel> Courses { get; set; } = new();

        /// <summary>
        /// Danh sách chứng chỉ tốt nghiệp đã được cấp (mỗi khóa học chỉ 1 chứng chỉ duy nhất)
        /// </summary>
        public List<Certificate> Certificates { get; set; } = new();

        public int TotalCoursesCount => Courses.Count;
        public int InProgressCount => InProgressCourses.Count;
        public int CompletedCount => CompletedCourses.Count;
        public int TotalCertificatesCount => Certificates.Count;
    }
}
