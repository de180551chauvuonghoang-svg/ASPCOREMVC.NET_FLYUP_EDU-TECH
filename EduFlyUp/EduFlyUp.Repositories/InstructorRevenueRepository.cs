using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface IInstructorRevenueRepository
{
    Task<InstructorRevenueStatement?> StatementAsync(InstructorIdentity instructor);
}

public sealed class InstructorRevenueRepository(AppDbContext db, IInstructorSettlementSource settlements) : IInstructorRevenueRepository
{
    public Task<InstructorRevenueStatement?> StatementAsync(InstructorIdentity instructor)
        => new SettlementInstructorEarningsSource(settlements).ReadStatementAsync(db, instructor);
}
