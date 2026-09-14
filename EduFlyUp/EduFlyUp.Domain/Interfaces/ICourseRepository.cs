using EduFlyUp.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduFlyUp.Domain.Interfaces
{
    // Kế thừa IRepository<Course> → có sẵn tất cả methods của IRepository
    // Thêm các methods đặc thù chỉ Course mới cần
    public interface ICourseRepository : IRepository<Course>
    {
        // Lấy khóa học kèm theo danh sách bài học (Eager Loading)
        Task<Course?> GetCourseWithLessonsAsync(int courseId);

        // Lấy danh sách khóa học theo giảng viên
        Task<IEnumerable<Course>> GetByInstructorAsync(string instructorId);

        // Lấy khóa học đã published (công khai)
        Task<IEnumerable<Course>> GetPublishedCoursesAsync();
    }
}
