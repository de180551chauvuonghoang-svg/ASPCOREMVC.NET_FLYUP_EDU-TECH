using EduFlyUp.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace EduFlyUp.Domain.Interfaces
{
    // T phải là class và kế thừa BaseEntity
    // Generic interface: dùng cho Course, Lesson, Enrollment đều được
    public interface IRepository<T> where T : BaseEntity
    {
        // Trả về tất cả records (IQueryable cho phép thêm .Where(), .OrderBy() sau)
        IQueryable<T> GetAll();

        // Tìm theo Id — async vì I/O operation
        Task<T?> GetByIdAsync(int id);

        // Tìm theo điều kiện — Expression<Func<T, bool>> là lambda predicate
        // VD: GetAsync(c => c.IsPublished == true)
        Task<T?> GetAsync(Expression<Func<T, bool>> predicate);

        // Thêm mới
        Task AddAsync(T entity);

        // Cập nhật
        void Update(T entity);

        // Xóa
        void Delete(T entity);

        // Kiểm tra tồn tại
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
    }
}
