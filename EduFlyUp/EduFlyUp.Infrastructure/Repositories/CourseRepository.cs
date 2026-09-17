using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EduFlyUp.Domain.Entities;
using EduFlyUp.Domain.Interfaces;
using EduFlyUp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.Infrastructure.Repositories
{
    public class CourseRepository : BaseRepository<Course>, ICourseRepository
    {
        public CourseRepository(AppDbContext context) : base(context) { }

        public async Task<Course?> GetCourseWithLessonsAsync(int courseId)
            => await _context.Courses
                .Include(c => c.Lessons.OrderBy(l => l.Order)) // Eager loading + sort
                .FirstOrDefaultAsync(c => c.Id == courseId);

        public async Task<IEnumerable<Course>> GetByInstructorAsync(string instructorId)
            => await _context.Courses
                .Where(c => c.InstructorId == instructorId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

        public async Task<IEnumerable<Course>> GetPublishedCoursesAsync()
            => await _context.Courses
                .Where(c => c.IsPublished)
                .Include(c => c.Lessons)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
    }
}
