using EduFlyUp.BusinessObjects;
using EduFlyUp.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    // GET: /Account/Register
    [AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    // POST: /Account/Register
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Gán role theo lựa chọn (Instructor hoặc mặc định Student)
            var assignedRole = string.Equals(model.Role, "Instructor", StringComparison.OrdinalIgnoreCase) 
                ? "Instructor" 
                : "Student";

            await _userManager.AddToRoleAsync(user, assignedRole);

            _logger.LogInformation("User {Email} đã đăng ký tài khoản với role {Role}.", model.Email, assignedRole);

            await _signInManager.SignInAsync(user, isPersistent: false);

            TempData["SuccessMessage"] = $"Chào mừng {user.FullName}! Bạn đã đăng ký thành công với vai trò {assignedRole}.";
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // GET: /Account/Login
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null, bool accountUnavailable = false)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (accountUnavailable)
        {
            ModelState.AddModelError(string.Empty,
                "Phiên đăng nhập không còn hợp lệ hoặc tài khoản đã bị vô hiệu hóa. Vui lòng đăng nhập lại hoặc liên hệ quản trị viên.");
        }
        return View(new LoginViewModel());
    }

    // POST: /Account/Login
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} đã đăng nhập.", model.Email);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty,
                "Tài khoản không được phép đăng nhập. Vui lòng liên hệ quản trị viên.");
            return View(model);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Tài khoản {Email} bị khóa.", model.Email);
            ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa do đăng nhập sai quá nhiều lần. Thử lại sau.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    // POST: /Account/ExternalLogin
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    // GET: /Account/ExternalLoginCallback
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");

        if (remoteError != null)
        {
            _logger.LogError("Lỗi từ nhà cung cấp dịch vụ ngoài: {Error}", remoteError);
            TempData["ErrorMessage"] = $"Lỗi từ dịch vụ ngoài: {remoteError}";
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("Không thể tải thông tin đăng nhập từ bên ngoài.");
            TempData["ErrorMessage"] = "Không thể lấy thông tin đăng nhập từ Google.";
            return RedirectToAction(nameof(Login));
        }

        // 1. Thử đăng nhập nếu user đã từng liên kết tài khoản Google này
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            _logger.LogInformation("Người dùng đăng nhập thành công qua tài khoản {Provider}.", info.LoginProvider);
            return LocalRedirect(returnUrl);
        }

        if (signInResult.IsNotAllowed)
        {
            TempData["ErrorMessage"] = "Tài khoản của bạn đã bị vô hiệu hóa hoặc chưa được cấp phép.";
            return RedirectToAction(nameof(Login));
        }

        if (signInResult.IsLockedOut)
        {
            TempData["ErrorMessage"] = "Tài khoản của bạn đang bị khóa tạm thời.";
            return RedirectToAction(nameof(Login));
        }

        // 2. Nếu chưa liên kết, kiểm tra theo Email trả về từ Google
        var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            TempData["ErrorMessage"] = "Không nhận được địa chỉ Email từ tài khoản Google của bạn.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Tự động đăng ký tài khoản mới với vai trò Student
            var name = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? email;
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = name,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Không thể tạo tài khoản từ Google: {Errors}", errors);
                TempData["ErrorMessage"] = $"Lỗi tạo tài khoản: {errors}";
                return RedirectToAction(nameof(Login));
            }

            // Gán role mặc định Student
            await _userManager.AddToRoleAsync(user, "Student");
            _logger.LogInformation("Đã tự động tạo tài khoản cho {Email} với role Student qua Google Login.", email);
        }
        else if (!user.IsActive)
        {
            TempData["ErrorMessage"] = "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.";
            return RedirectToAction(nameof(Login));
        }

        // 3. Liên kết tài khoản ngoài với user và đăng nhập
        var addLoginResult = await _userManager.AddLoginAsync(user, info);
        if (addLoginResult.Succeeded || await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey) != null)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            _logger.LogInformation("Đã liên kết và đăng nhập người dùng {Email} qua Google.", email);
            return LocalRedirect(returnUrl);
        }

        TempData["ErrorMessage"] = "Có lỗi xảy ra khi liên kết tài khoản Google.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User đã đăng xuất.");
        return RedirectToAction("Index", "Home");
    }
}
