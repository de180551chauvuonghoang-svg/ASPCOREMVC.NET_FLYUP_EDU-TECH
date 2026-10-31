using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface IInstructorRevenueService
{
    Task<InstructorRevenuePage?> ReportAsync(InstructorIdentity instructor, int page);
}

public sealed class InstructorRevenueService(IInstructorRevenueRepository repository) : IInstructorRevenueService
{
    public async Task<InstructorRevenuePage?> ReportAsync(InstructorIdentity instructor, int page)
    {
        if (string.IsNullOrWhiteSpace(instructor.UserId) || string.IsNullOrWhiteSpace(instructor.UserName)) return null;
        var statement = await repository.StatementAsync(instructor);
        if (statement is null) return null;
        var count = statement.Items.Count;
        page = Math.Clamp(page, 1, Math.Max(1, (count + InstructorRevenuePage.PageSize - 1) / InstructorRevenuePage.PageSize));
        var items = statement.Items.OrderByDescending(i => i.Receipt.Info?.SettledAtUtc)
            .ThenBy(i => i.Receipt.Reference, StringComparer.Ordinal)
            .Skip((page - 1) * InstructorRevenuePage.PageSize).Take(InstructorRevenuePage.PageSize).ToList();
        return new(items, page, count, statement.TotalNet, statement.TotalInstructor, statement.TotalPlatform);
    }
}
