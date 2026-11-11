using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface IAdminCourseService
{
    Task<AdminCoursePage> SearchAsync(AdminCourseQuery query);
    Task<AdminCourseDetail?> GetDetailAsync(int id);
    Task<bool> SetPublicationAsync(int id, bool published, string? reason = null);
    Task<bool> RejectCourseAsync(int id, string reason);
}

public sealed class AdminCourseService(IAdminCourseRepository repository) : IAdminCourseService
{
    public Task<AdminCoursePage> SearchAsync(AdminCourseQuery query) => repository.SearchAsync(query with
    {
        Search = query.Search?.Trim(), Instructor = query.Instructor?.Trim(), Page = Math.Max(1, query.Page)
    });
    public Task<AdminCourseDetail?> GetDetailAsync(int id) => repository.GetDetailAsync(id);
    public Task<bool> SetPublicationAsync(int id, bool published, string? reason = null) => repository.SetPublicationAsync(id, published, reason);
    public Task<bool> RejectCourseAsync(int id, string reason) => repository.RejectCourseAsync(id, reason);
}
