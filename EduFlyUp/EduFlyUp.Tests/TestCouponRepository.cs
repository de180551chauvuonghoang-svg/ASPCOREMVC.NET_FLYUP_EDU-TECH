using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Tests;

internal sealed class TestCouponRepository : ICouponRepository
{
    public List<Coupon> Coupons { get; } = [];
    public List<Course> Courses { get; } = [];
    public CouponChange? NextSaveResult { get; set; }
    public Task<CouponPage> SearchAsync(CouponOwner owner, string? search, int page)
    {
        var items = Coupons.Where(c => owner.Owns(c.InstructorId) && (search is null || c.Code.Contains(search))).ToList();
        return Task.FromResult(new CouponPage(items, 1, items.Count));
    }
    public Task<Coupon?> GetAsync(CouponOwner owner, int id) => Task.FromResult(Coupons.Find(c => c.Id == id && owner.Owns(c.InstructorId)));
    public Task<List<Course>> CoursesAsync(CouponOwner owner) => Task.FromResult(Courses.Where(c => owner.Owns(c.InstructorId)).ToList());
    public Task<CouponChange> SaveAsync(CouponOwner owner, CouponInput input, int? id, string? version)
    {
        if (NextSaveResult.HasValue) return Task.FromResult(NextSaveResult.Value);
        var stored = id.HasValue ? Coupons.Find(c => c.Id == id && owner.Owns(c.InstructorId)) : null;
        if (id.HasValue && stored is null) return Task.FromResult(CouponChange.NotFound);
        if (stored is not null && CouponPresentation.Version(stored) != version) return Task.FromResult(CouponChange.Stale);
        if (input.CourseId.HasValue && !Courses.Any(c => c.Id == input.CourseId && owner.Owns(c.InstructorId))) return Task.FromResult(CouponChange.InvalidCourse);
        if (Coupons.Any(c => c.Id != id && c.Code == input.Code)) return Task.FromResult(CouponChange.DuplicateCode);
        if (stored is null)
        {
            stored = new Coupon { Id = Coupons.Count + 1, InstructorId = owner.UserId };
            Coupons.Add(stored);
        }
        stored.Code = input.Code; stored.DiscountPercent = input.DiscountPercent; stored.MaxUsage = input.MaxUsage;
        stored.StartDate = input.StartDateUtc; stored.EndDate = input.EndDateUtc; stored.CourseId = input.CourseId;
        stored.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(CouponChange.Saved);
    }
    public Task<CouponChange> SetActiveAsync(CouponOwner owner, int id, bool active, string version, DateTime utcNow)
    {
        var stored = Coupons.Find(c => c.Id == id && owner.Owns(c.InstructorId));
        if (stored is null) return Task.FromResult(CouponChange.NotFound);
        if (CouponPresentation.Version(stored) != version) return Task.FromResult(CouponChange.Stale);
        stored.IsActive = active; stored.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(CouponChange.Saved);
    }
}
