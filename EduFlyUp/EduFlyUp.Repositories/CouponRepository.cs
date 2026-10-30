using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;

namespace EduFlyUp.Repositories;

public interface ICouponRepository
{
    Task<CouponPage> SearchAsync(CouponOwner owner, string? search, int page);
    Task<Coupon?> GetAsync(CouponOwner owner, int id);
    Task<List<Course>> CoursesAsync(CouponOwner owner);
    Task<CouponChange> SaveAsync(CouponOwner owner, CouponInput input, int? id, string? version);
    Task<CouponChange> SetActiveAsync(CouponOwner owner, int id, bool active, string version, DateTime utcNow);
}

public sealed class CouponRepository(AppDbContext db) : ICouponRepository
{
    public Task<CouponPage> SearchAsync(CouponOwner owner, string? search, int page) => CouponDAO.Instance.SearchAsync(db, owner, search, page);
    public Task<Coupon?> GetAsync(CouponOwner owner, int id) => CouponDAO.Instance.GetAsync(db, owner, id);
    public Task<List<Course>> CoursesAsync(CouponOwner owner) => CouponDAO.Instance.CoursesAsync(db, owner);
    public Task<CouponChange> SaveAsync(CouponOwner owner, CouponInput input, int? id, string? version) => CouponDAO.Instance.SaveAsync(db, owner, input, id, version);
    public Task<CouponChange> SetActiveAsync(CouponOwner owner, int id, bool active, string version, DateTime utcNow) => CouponDAO.Instance.SetActiveAsync(db, owner, id, active, version, utcNow);
}
