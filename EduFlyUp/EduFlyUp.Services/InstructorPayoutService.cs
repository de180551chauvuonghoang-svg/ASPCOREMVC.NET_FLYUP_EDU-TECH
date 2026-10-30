using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface IInstructorPayoutService
{
    Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page);
}

public sealed class InstructorPayoutService(IInstructorPayoutRepository repository) : IInstructorPayoutService
{
    public Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page)
        => !string.IsNullOrWhiteSpace(instructor.UserId) && !string.IsNullOrWhiteSpace(instructor.UserName)
            ? repository.HistoryAsync(instructor, Math.Max(1, page))
            : Task.FromResult(new InstructorPayoutHistory([], 1, 0));
}
