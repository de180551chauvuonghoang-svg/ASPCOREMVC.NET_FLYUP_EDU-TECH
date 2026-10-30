using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using EduFlyUp.Web.Models.Instructor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Instructor")]
public sealed class CouponsController(ICouponService coupons, TimeProvider time, ILogger<CouponsController> logger) : Controller
{
    private CouponOwner Owner => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.Identity?.Name ?? "");

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        if (!ModelState.IsValid || search?.Length > 100) return BadRequest();
        return View(new CouponListModel(await coupons.SearchAsync(Owner, search, page), search?.Trim(), time.GetUtcNow().UtcDateTime));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var now = CouponFormModel.VietnamTime(time.GetUtcNow().UtcDateTime);
        return View("Form", await Fill(new() { StartDate = now, EndDate = now.AddDays(7) }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CouponFormModel model)
    {
        model.Id = null;
        ModelState.Remove(nameof(model.Id));
        if (!ModelState.IsValid) return await Invalid(model);
        var result = await TrySave(() => coupons.SaveAsync(Owner, model.ToInput()));
        if (!result.Success) return await Invalid(model, result);
        TempData["SuccessMessage"] = "Đã tạo mã giảm giá.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] int id)
    {
        var coupon = await coupons.GetAsync(Owner, id);
        if (coupon is null) return NotFound();
        return View("Form", await Fill(new()
        {
            Id = id, Code = coupon.Code, DiscountPercent = coupon.DiscountPercent, MaxUsage = coupon.MaxUsage,
            StartDate = CouponFormModel.VietnamTime(coupon.StartDate), EndDate = CouponFormModel.VietnamTime(coupon.EndDate),
            CourseId = coupon.CourseId, Version = CouponPresentation.Version(coupon), UsedCount = coupon.UsedCount
        }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, CouponFormModel model)
    {
        if (model.Id != id) return BadRequest();
        var stored = await coupons.GetAsync(Owner, id);
        if (stored is null) return NotFound();
        model.UsedCount = stored.UsedCount;
        if (string.IsNullOrWhiteSpace(model.Version)) ModelState.AddModelError("", "Thiếu phiên bản mã giảm giá. Vui lòng tải lại trang.");
        if (!ModelState.IsValid) return await Invalid(model);
        var result = await TrySave(() => coupons.SaveAsync(Owner, model.ToInput(), id, model.Version));
        if (result.Change == CouponChange.NotFound) return NotFound();
        if (!result.Success) return await Invalid(model, result);
        TempData["SuccessMessage"] = "Đã cập nhật mã giảm giá.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Activate([FromRoute] int id, string version) => SetActive(id, true, version);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Deactivate([FromRoute] int id, string version) => SetActive(id, false, version);

    private async Task<IActionResult> SetActive(int id, bool active, string version)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(version)) return BadRequest();
        var result = await TrySave(() => coupons.SetActiveAsync(Owner, id, active, version));
        if (result.Change == CouponChange.NotFound) return NotFound();
        TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success ? (active ? "Đã bật mã giảm giá." : "Đã tắt mã giảm giá.") : result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<CouponFormModel> Fill(CouponFormModel model)
    {
        model.Courses = (await coupons.CoursesAsync(Owner)).Select(c => new SelectListItem(c.Title, c.Id.ToString())).ToList();
        return model;
    }
    private async Task<IActionResult> Invalid(CouponFormModel model, CouponResult? result = null)
    {
        if (result is not null) ModelState.AddModelError(result.Change == CouponChange.DuplicateCode ? nameof(model.Code) : "", result.Message);
        Response.StatusCode = result?.Change == CouponChange.Stale ? 409 : 400;
        return View("Form", await Fill(model));
    }
    private async Task<CouponResult> TrySave(Func<Task<CouponResult>> save)
    {
        try { return await save(); }
        catch (Exception ex) when (ex is System.Data.Common.DbException or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            logger.LogError(ex, "Không thể lưu mã giảm giá của giảng viên {InstructorId}.", Owner.UserId);
            return new(false, "Chưa lưu được mã giảm giá. Vui lòng thử lại.");
        }
    }
}
