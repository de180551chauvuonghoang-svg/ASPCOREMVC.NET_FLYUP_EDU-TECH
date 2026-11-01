using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface IInstructorRevenueService
{
    Task<InstructorRevenuePage?> ReportAsync(InstructorIdentity instructor, int page, string? search = null);
}

public sealed class InstructorRevenueService(IInstructorRevenueRepository repository) : IInstructorRevenueService
{
    public async Task<InstructorRevenuePage?> ReportAsync(InstructorIdentity instructor, int page, string? search = null)
    {
        if (string.IsNullOrWhiteSpace(instructor.UserId) || string.IsNullOrWhiteSpace(instructor.UserName) || search?.Length > 100) return null;
        var statement = await repository.StatementAsync(instructor);
        if (statement is null) return null;
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var filtered = statement.Items.Where(i => search is null || i.Receipt.Reference.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (i.Receipt.Info?.CourseTitle?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (i.Receipt.Info?.StudentName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        var count = filtered.Count;
        page = Math.Clamp(page, 1, Math.Max(1, (count + InstructorRevenuePage.PageSize - 1) / InstructorRevenuePage.PageSize));
        var items = filtered.OrderByDescending(i => i.Receipt.Info?.SettledAtUtc)
            .ThenBy(i => i.Receipt.Reference, StringComparer.Ordinal)
            .Skip((page - 1) * InstructorRevenuePage.PageSize).Take(InstructorRevenuePage.PageSize).ToList();
        return new(items, page, count, filtered.Sum(i => i.Share.NetCollected), filtered.Sum(i => i.Share.InstructorAmount),
            filtered.Sum(i => i.Share.PlatformAmount), search);
    }
}
