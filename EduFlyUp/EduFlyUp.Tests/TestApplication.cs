using System.Net;
using System.Text.RegularExpressions;
using EduFlyUp.BusinessObjects;
using EduFlyUp.DataAccess;
using EduFlyUp.Repositories;
using EduFlyUp.Services;
using EduFlyUp.Web.Controllers;
using EduFlyUp.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EduFlyUp.Tests;

internal sealed class TestApplication : IDisposable
{
    public const string Password = "Test-only@12345";
    private readonly TestServer _server;
    public TestCourseRepository Courses { get; } = new();
    public TestEnrollmentRepository Enrollments { get; } = new();
    public TestLessonRepository Lessons { get; } = new();
    public TestCouponRepository Coupons { get; } = new();
    public IServiceProvider Services => _server.Services;

    public TestApplication(EduFlyUp.Web.Storage.ILessonMaterialStorage? materialStorage = null, EduFlyUp.Web.Storage.ILessonVideoStorage? videoStorage = null)
    {
        var database = Guid.NewGuid().ToString();
        _server = new TestServer(new WebHostBuilder()
            .UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../EduFlyUp.Web")))
            .ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(database));
                services.AddIdentity<ApplicationUser, IdentityRole>()
                    .AddEntityFrameworkStores<AppDbContext>()
                    .AddDefaultTokenProviders()
                    .AddActiveAccountProtection();
                services.ConfigureApplicationCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                });
                services.AddSingleton<ICourseRepository>(Courses);
                services.AddSingleton<IEnrollmentRepository>(Enrollments);
                services.AddSingleton<ILessonRepository>(Lessons);
                services.AddSingleton<EduFlyUp.Web.Storage.ILessonMaterialStorage>(materialStorage ?? new TestMaterialStorage());
                services.AddScoped<EduFlyUp.Web.Storage.LessonFileCleanup>();
                services.AddSingleton<EduFlyUp.Web.Storage.ILessonVideoStorage>(videoStorage ?? new TestVideoStorage());
                services.AddScoped<ICourseService, CourseService>();
                services.AddScoped<ILessonService, LessonService>();
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<ICouponRepository>(Coupons);
                services.AddScoped<ICouponService, CouponService>();
                services.AddScoped<IInstructorStudentRepository, InstructorStudentRepository>();
                services.AddScoped<IInstructorStudentService, InstructorStudentService>();
                services.AddScoped<IInstructorPayoutRepository, InstructorPayoutRepository>();
                services.AddScoped<IInstructorEarningsSource, UnavailableInstructorEarningsSource>();
                services.AddScoped<IInstructorPayoutService, InstructorPayoutService>();
                services.AddScoped<ICourseAccessService, CourseAccessService>();
                services.AddScoped<IAdminCourseRepository, AdminCourseRepository>();
                services.AddScoped<IAdminCourseService, AdminCourseService>();
                services.AddScoped<IAdminUserRepository, AdminUserRepository>();
                services.AddSingleton<IAdminUserStatusLock, TestAdminUserStatusLock>();
                services.AddScoped<IAdminUserService, AdminUserService>();
                services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
                services.AddScoped<IAdminDashboardService, AdminDashboardService>();
                services.AddControllersWithViews().AddApplicationPart(typeof(AccountController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllerRoute(
                    "default", "{controller=Home}/{action=Index}/{id?}"));
            }));
    }

    public TestBrowser CreateBrowser(string? authCookie = null)
    {
        var cookies = new CookieContainer();
        if (authCookie is not null) cookies.SetCookies(new Uri("http://localhost"), authCookie);
        return new TestBrowser(new HttpClient(new CookieHandler(_server.CreateHandler(), cookies))
        {
            BaseAddress = new Uri("http://localhost")
        }, cookies);
    }

    public async Task<string> CreateUserAsync(string email, string role, bool active = true)
    {
        using var scope = Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, FullName = role, IsActive = active };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user.Id;
    }

    public async Task ChangeUserAsync(string id, Func<UserManager<ApplicationUser>, ApplicationUser, Task> change)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(id);
        Assert.NotNull(user);
        await change(users, user);
    }

    public static async Task<HttpResponseMessage> LoginAsync(
        TestBrowser browser, string email, string? returnUrl = "/Admin", string? password = null, bool remember = false)
    {
        var url = "/Account/Login?returnUrl=" + Uri.EscapeDataString(returnUrl ?? "/");
        var token = await AntiForgeryTokenAsync(browser, url);
        return await browser.Client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Email"] = email,
            ["Password"] = password ?? Password,
            ["RememberMe"] = remember.ToString()
        }));
    }

    public static async Task<string> AntiForgeryTokenAsync(TestBrowser browser, string url)
    {
        var response = await browser.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "The real Razor form must provide an anti-forgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public void Dispose() => _server.Dispose();

    private sealed class CookieHandler(HttpMessageHandler innerHandler, CookieContainer cookies)
        : DelegatingHandler(innerHandler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var cookie = cookies.GetCookieHeader(uri);
            if (cookie.Length > 0) request.Headers.TryAddWithoutValidation("Cookie", cookie);
            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var headers))
                foreach (var header in headers) cookies.SetCookies(uri, header);
            return response;
        }
    }
}

internal sealed record TestBrowser(HttpClient Client, CookieContainer Cookies) : IDisposable
{
    public string? AuthCookie => Cookies.GetCookies(new Uri("http://localhost"))
        .Cast<Cookie>().Where(c => c.Name == ".AspNetCore.Identity.Application")
        .Select(c => $"{c.Name}={c.Value}").SingleOrDefault();
    public void Dispose() => Client.Dispose();
}
