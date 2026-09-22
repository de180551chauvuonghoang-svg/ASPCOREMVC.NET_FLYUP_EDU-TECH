using EduFlyUp.Infrastructure.Identity;
using EduFlyUp.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduFlyUp.Web.Controllers
{
    public class AccountController : Controller
    {
        // UserManager: quản lý User (tạo, xóa, đổi mật khẩu, thêm role...)
        private readonly UserManager<ApplicationUser> _userManager;

        // SignInManager: quản lý đăng nhập/đăng xuất (Cookie)
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

        // ──────── REGISTER ────────

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

            // Tạo ApplicationUser từ ViewModel
            var user = new ApplicationUser
            {
                UserName = model.Email,   // Identity dùng UserName để login
                Email = model.Email,
                FullName = model.FullName,
                IsActive = true
            };

            // CreateAsync: hash password và lưu vào DB
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Tự động gán role "Student" khi đăng ký
                await _userManager.AddToRoleAsync(user, "Student");

                _logger.LogInformation("User {Email} đã đăng ký tài khoản mới.", model.Email);

                // Đăng nhập ngay sau khi đăng ký thành công
                await _signInManager.SignInAsync(user, isPersistent: false);

                TempData["SuccessMessage"] = $"Chào mừng {user.FullName}! Tài khoản đã được tạo thành công.";
                return RedirectToAction("Index", "Home");
            }

            // Nếu tạo tài khoản thất bại — hiện lỗi từ Identity
            // VD: "Passwords must have at least one digit"
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // ──────── LOGIN ────────

        // GET: /Account/Login
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // returnUrl: URL trang người dùng muốn vào trước khi bị redirect về Login
            ViewData["ReturnUrl"] = returnUrl;
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

            // PasswordSignInAsync: kiểm tra email + password → tạo Cookie
            // isPersistent: true = nhớ qua phiên, lockoutOnFailure: khóa sau N lần sai
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} đã đăng nhập.", model.Email);

                // LocalRedirect: chỉ cho phép redirect về URL nội bộ (tránh Open Redirect Attack)
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }
                return RedirectToAction("Index", "Home");
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

        // ──────── LOGOUT ────────

        // POST: /Account/Logout (phải là POST để tránh CSRF logout attack)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User đã đăng xuất.");
            return RedirectToAction("Index", "Home");
        }
    }
}
