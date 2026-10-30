using System.Security.Claims;
using EduFlyUp.BusinessObjects;
using EduFlyUp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EduFlyUp.Web.Models.Instructor;

namespace EduFlyUp.Web.Controllers;

[Authorize(Roles = "Instructor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InstructorPayoutsController(IInstructorPayoutService payouts, ILogger<InstructorPayoutsController> logger) : Controller
{
    private InstructorIdentity Instructor => new(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.Identity?.Name ?? "");

    [HttpGet]
    public async Task<IActionResult> Create() => await Form(new());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Amount,BankName,AccountNumber,AccountHolderName")] InstructorPayoutForm model)
    {
        if (!ModelState.IsValid) { Response.StatusCode = 400; return await Form(model); }
        try
        {
            var result = await payouts.CreateAsync(Instructor, new(model.Amount!.Value, model.BankName, model.AccountNumber, model.AccountHolderName));
            if (result.Change == InstructorPayoutChange.Created)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError("", result.Message);
            Response.StatusCode = result.Change == InstructorPayoutChange.EarningsUnavailable ? 503 : result.Change == InstructorPayoutChange.Invalid ? 400 : 409;
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            logger.LogError(ex, "Không thể gửi yêu cầu rút tiền của giảng viên {InstructorId}.", Instructor.UserId);
            ModelState.AddModelError("", "Chưa gửi được yêu cầu rút tiền. Vui lòng kiểm tra lịch sử trước khi thử lại.");
            Response.StatusCode = 503;
        }
        return await Form(model);
    }

    private async Task<IActionResult> Form(InstructorPayoutForm model)
    {
        // Balance is always read on the server, never trusted from posted form values.
        try { model.Balance = await payouts.BalanceAsync(Instructor); }
        catch (Exception ex) when (ex is System.Data.Common.DbException or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            logger.LogError(ex, "Không thể đọc số dư của giảng viên {InstructorId}.", Instructor.UserId);
            model.Balance = new(null, 0, 0, 0);
            Response.StatusCode = 503;
        }
        return View("Create", model);
    }
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, PayoutStatus? status = null)
    {
        if (!ModelState.IsValid || (status.HasValue && !Enum.IsDefined(status.Value))) return BadRequest();
        return View(await payouts.HistoryAsync(Instructor, page, status));
    }
}
