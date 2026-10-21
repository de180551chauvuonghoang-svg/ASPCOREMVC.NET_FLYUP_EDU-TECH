using Microsoft.AspNetCore.Identity;

namespace EduFlyUp.Web.Security;

public static class IdentityRegistration
{
    public static IdentityBuilder AddActiveAccountProtection(this IdentityBuilder builder)
    {
        builder.AddSignInManager<ActiveAccountSignInManager>();
        builder.Services.AddScoped<ActiveAccountCookieEvents>();
        builder.Services.ConfigureApplicationCookie(options =>
            options.EventsType = typeof(ActiveAccountCookieEvents));
        return builder;
    }
}
