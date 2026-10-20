using System.Net;
using AdminDemo;

namespace AdminDemo.Tests;

public sealed class DemoHarnessTests
{
    [Theory]
    [InlineData("/Account/Login?ReturnUrl=%2FAdmin")]
    [InlineData("http://127.0.0.1:59687/Account/Login?ReturnUrl=%2FAdmin")]
    public void Login_redirect_accepts_relative_and_Kestrel_absolute_locations(string location)
    {
        using var response = Response(location);
        HttpChecks.Redirect(response, "/Account/Login");
    }

    [Theory]
    [InlineData("http://example.test/Account/Login")]
    [InlineData("http://127.0.0.1:59688/Account/Login")]
    [InlineData("/Account/LoginExtra")]
    [InlineData("/Account/AccessDenied")]
    public void Login_redirect_still_rejects_wrong_origin_port_or_path(string location)
    {
        using var response = Response(location);
        Assert.Throws<InvalidOperationException>(() => HttpChecks.Redirect(response, "/Account/Login"));
    }

    [Fact]
    public async Task Browser_handoff_waits_for_stop_signal_before_completing()
    {
        var signal = Path.Combine(Path.GetTempPath(), "FlyUpDemoSignal_" + Guid.NewGuid().ToString("N"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var wait = DemoLifetime.WaitForStopFileAsync(signal, () => false, timeout.Token);
            Assert.False(wait.IsCompleted);
            await File.WriteAllTextAsync(signal, "browser verified", timeout.Token);
            await wait;
        }
        finally { if (File.Exists(signal)) File.Delete(signal); }
    }

    [Fact]
    public async Task Browser_handoff_fails_if_the_owned_Web_process_exits_before_signal()
    {
        var signal = Path.Combine(Path.GetTempPath(), "FlyUpDemoSignal_" + Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoLifetime.WaitForStopFileAsync(signal, () => true));
    }

    private static HttpResponseMessage Response(string location) => new(HttpStatusCode.Redirect)
    {
        RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:59687/Admin"),
        Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) }
    };
}
