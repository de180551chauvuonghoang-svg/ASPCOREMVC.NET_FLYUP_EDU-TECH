using EduFlyUp.BusinessObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace EduFlyUp.Web.Security;

public sealed class ActiveAccountCookieEvents(SignInManager<ApplicationUser> signInManager)
    : CookieAuthenticationEvents
{
    private const string AccountUnavailableKey = "EduFlyUp.AccountUnavailable";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        // Preserve Identity's principal renewal and security-stamp validation.
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal is null) return;

        // Check the persisted user and stamp on every request, not only after
        // Identity's default validation interval has elapsed.
        var user = await signInManager.ValidateSecurityStampAsync(context.Principal);
        if (user?.IsActive == true) return;

        context.RejectPrincipal();
        context.ShouldRenew = false;
        context.HttpContext.Items[AccountUnavailableKey] = true;
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.HttpContext.Items.ContainsKey(AccountUnavailableKey))
        {
            context.RedirectUri = QueryHelpers.AddQueryString(
                context.RedirectUri, "accountUnavailable", "true");
        }

        return base.RedirectToLogin(context);
    }
}
