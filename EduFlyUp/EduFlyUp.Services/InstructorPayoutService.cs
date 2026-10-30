using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface IInstructorPayoutService
{
    Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page, PayoutStatus? status = null);
}

public sealed class InstructorPayoutService(IInstructorPayoutRepository repository) : IInstructorPayoutService
{
    public Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page, PayoutStatus? status = null)
        => !string.IsNullOrWhiteSpace(instructor.UserId) && !string.IsNullOrWhiteSpace(instructor.UserName) && (!status.HasValue || Enum.IsDefined(status.Value))
            ? repository.HistoryAsync(instructor, Math.Max(1, page), status)
            : Task.FromResult(new InstructorPayoutHistory([], 1, 0));
}
