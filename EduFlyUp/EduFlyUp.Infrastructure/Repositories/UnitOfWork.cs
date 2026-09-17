using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Infrastructure.Data;
namespace EduFlyUp.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        // Lazy init: chỉ tạo Repository khi cần dùng lần đầu
        private ICourseRepository? _courses;
        private IRepository<Lesson>? _lessons;
        private IRepository<Enrollment>? _enrollments;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public ICourseRepository Courses
            => _courses ??= new CourseRepository(_context);

        public IRepository<Lesson> Lessons
            => _lessons ??= new BaseRepository<Lesson>(_context);

        public IRepository<Enrollment> Enrollments
            => _enrollments ??= new BaseRepository<Enrollment>(_context);

        public async Task<int> SaveChangesAsync()
            => await _context.SaveChangesAsync();

        public void Dispose() => _context.Dispose();
    }
}
