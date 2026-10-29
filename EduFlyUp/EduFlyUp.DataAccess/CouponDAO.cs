using EduFlyUp.BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace EduFlyUp.DataAccess;

public sealed class CouponDAO : SingletonBase<CouponDAO>
{
    private static IQueryable<Coupon> Owned(AppDbContext db, CouponOwner owner) => db.Coupons
        .Where(c => c.InstructorId == owner.UserId || c.InstructorId == owner.UserName);

    public async Task<CouponPage> SearchAsync(AppDbContext db, CouponOwner owner, string? search, int page)
    {
        var query = Owned(db, owner).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(c => c.Code.Contains(search));
        var count = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (count + CouponPage.PageSize - 1) / CouponPage.PageSize));
        var items = await query.Include(c => c.Course).OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip((page - 1) * CouponPage.PageSize).Take(CouponPage.PageSize).ToListAsync();
        return new(items, page, count);
    }

    public Task<Coupon?> GetAsync(AppDbContext db, CouponOwner owner, int id) => Owned(db, owner).AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

    public Task<List<Course>> CoursesAsync(AppDbContext db, CouponOwner owner) => db.Courses.AsNoTracking()
        .Where(c => c.InstructorId == owner.UserId || c.InstructorId == owner.UserName).OrderBy(c => c.Title).ToListAsync();

    // All coupon management writers share this SQL lock, including other app instances.
    // A checkout writer must coordinate here too when changing UsedCount/MaxUsage.
    private static async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginAsync(AppDbContext db)
    {
        var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource=N'EduFlyUp.CouponManagement',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51001, N'Không thể lấy khóa quản lý mã giảm giá.', 1;
                """);
            foreach (var entry in db.ChangeTracker.Entries<Coupon>().ToArray()) entry.State = EntityState.Detached;
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    public async Task<CouponChange> SaveAsync(AppDbContext db, CouponOwner owner, CouponInput input, int? id, string? version)
    {
        await using var transaction = await BeginAsync(db);
        Coupon? stored = null;
        if (id.HasValue)
        {
            stored = await Owned(db, owner).FirstOrDefaultAsync(c => c.Id == id);
            if (stored is null) return CouponChange.NotFound;
            if (CouponPresentation.Version(stored) != version) return CouponChange.Stale;
            if (input.MaxUsage < stored.UsedCount) return CouponChange.UsageConflict;
            if (stored.UsedCount > 0 && (!string.Equals(stored.Code.Trim(), input.Code, StringComparison.OrdinalIgnoreCase) || stored.DiscountPercent != input.DiscountPercent || stored.CourseId != input.CourseId))
                return CouponChange.UsedTermsLocked;
        }
        if (input.CourseId.HasValue && !await db.Courses.AnyAsync(c => c.Id == input.CourseId &&
            (c.InstructorId == owner.UserId || c.InstructorId == owner.UserName))) return CouponChange.InvalidCourse;
        if (await db.Coupons.AnyAsync(c => c.Id != (id ?? 0) && c.Code.Trim().ToUpper() == input.Code))
            return CouponChange.DuplicateCode;
        if (stored is null)
        {
            stored = new Coupon { InstructorId = owner.UserId, IsActive = true, UsedCount = 0 };
            db.Coupons.Add(stored);
        }
        stored.Code = input.Code;
        stored.DiscountPercent = input.DiscountPercent;
        stored.MaxUsage = input.MaxUsage;
        stored.StartDate = input.StartDateUtc;
        stored.EndDate = input.EndDateUtc;
        stored.CourseId = input.CourseId;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return CouponChange.Saved;
    }

    public async Task<CouponChange> SetActiveAsync(AppDbContext db, CouponOwner owner, int id, bool active, string version, DateTime utcNow)
    {
        await using var transaction = await BeginAsync(db);
        var stored = await Owned(db, owner).FirstOrDefaultAsync(c => c.Id == id);
        if (stored is null) return CouponChange.NotFound;
        if (CouponPresentation.Version(stored) != version) return CouponChange.Stale;
        if (active && (stored.EndDate <= utcNow || stored.EndDate <= stored.StartDate || stored.MaxUsage <= stored.UsedCount ||
            stored.DiscountPercent is < 1 or > 90)) return CouponChange.CannotActivate;
        if (active && stored.CourseId.HasValue && !await db.Courses.AnyAsync(c => c.Id == stored.CourseId &&
            (c.InstructorId == owner.UserId || c.InstructorId == owner.UserName))) return CouponChange.InvalidCourse;
        if (stored.IsActive != active)
        {
            stored.IsActive = active;
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return CouponChange.Saved;
    }
}
