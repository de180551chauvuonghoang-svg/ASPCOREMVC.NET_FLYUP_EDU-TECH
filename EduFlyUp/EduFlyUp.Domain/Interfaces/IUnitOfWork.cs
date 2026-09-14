using EduFlyUp.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduFlyUp.Domain.Interfaces
{
    // Unit of Work: gom nhiều thao tác DB vào 1 transaction
    // VD: Tạo Enrollment + trừ số slot còn lại → phải commit cùng lúc
    // Nếu 1 cái lỗi → rollback toàn bộ
    public interface IUnitOfWork : IDisposable
    {
        ICourseRepository Courses { get; }
        IRepository<Lesson> Lessons { get; }
        IRepository<Enrollment> Enrollments { get; }

        // Lưu tất cả thay đổi vào DB — trả về số records bị ảnh hưởng
        Task<int> SaveChangesAsync();
    }
}
