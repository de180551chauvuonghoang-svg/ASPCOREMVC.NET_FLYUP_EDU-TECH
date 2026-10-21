using System.Net;
using System.Text.RegularExpressions;

namespace AdminDemo;

internal sealed class DemoBrowser : IDisposable
{
    private readonly Uri origin;
    private readonly CookieContainer cookies = new();
    public HttpClient Client { get; }
    public string? AuthCookie => cookies.GetCookies(origin).Cast<Cookie>()
        .Where(c => c.Name == ".AspNetCore.Identity.Application").Select(c => c.Name + "=" + c.Value).SingleOrDefault();

    public DemoBrowser(string baseUrl, string? cookie = null)
    {
        origin = new Uri(baseUrl);
        if (cookie is not null) cookies.SetCookies(origin, cookie);
        Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies })
        { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string> HtmlAsync(string url)
    {
        using var response = await Client.GetAsync(url);
        HttpChecks.Require(response.StatusCode == HttpStatusCode.OK, "Expected HTTP 200: " + url + ", got " + response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    public async Task<string> TokenAsync(string url)
    {
        var html = await HtmlAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        HttpChecks.Require(match.Success, "Missing real Razor anti-forgery token: " + url);
        return match.Groups[1].Value;
    }

    public async Task<HttpResponseMessage> LoginAsync(DemoAccount account, string password)
    {
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TokenAsync("/Account/Login"),
            ["Email"] = account.Email, ["Password"] = password, ["RememberMe"] = "true"
        };
        return await Client.PostAsync("/Account/Login", new FormUrlEncodedContent(fields));
    }

    public async Task<HttpResponseMessage> PostAsync(string url, string formUrl, Dictionary<string, string>? fields = null)
    {
        fields ??= [];
        fields["__RequestVerificationToken"] = await TokenAsync(formUrl);
        return await Client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    public void Dispose() => Client.Dispose();
}
