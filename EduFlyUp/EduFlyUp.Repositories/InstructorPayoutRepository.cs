using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface IInstructorPayoutRepository
{
    Task<InstructorPayoutBalance> BalanceAsync(InstructorIdentity instructor);
    Task<InstructorPayoutChange> CreateAsync(InstructorIdentity instructor, InstructorPayoutInput input);
    Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page, PayoutStatus? status = null);
}

public sealed class InstructorPayoutRepository(AppDbContext db, IInstructorEarningsSource? earnings = null) : IInstructorPayoutRepository
{
    private readonly IInstructorEarningsSource _earnings = earnings ?? new UnavailableInstructorEarningsSource();
    public Task<InstructorPayoutBalance> BalanceAsync(InstructorIdentity instructor)
        => InstructorPayoutDAO.Instance.BalanceAsync(db, _earnings, instructor);
    public Task<InstructorPayoutChange> CreateAsync(InstructorIdentity instructor, InstructorPayoutInput input)
        => InstructorPayoutDAO.Instance.CreateAsync(db, _earnings, instructor, input);
    public Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page, PayoutStatus? status = null)
        => InstructorPayoutDAO.Instance.HistoryAsync(db, instructor, page, status);
}
