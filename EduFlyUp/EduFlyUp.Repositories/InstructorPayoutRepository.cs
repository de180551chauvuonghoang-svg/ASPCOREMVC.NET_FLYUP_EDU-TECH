using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface IInstructorPayoutRepository
{
    Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page);
}

public sealed class InstructorPayoutRepository(AppDbContext db) : IInstructorPayoutRepository
{
    public Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page)
        => InstructorPayoutDAO.Instance.HistoryAsync(db, instructor, page);
}
