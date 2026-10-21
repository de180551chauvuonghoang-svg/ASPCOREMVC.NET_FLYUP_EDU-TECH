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

    [Fact]
    public async Task Canceling_the_runner_ends_the_browser_wait_without_approval()
    {
        var signal = Path.Combine(Path.GetTempPath(), "FlyUpDemoSignal_" + Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();
        var wait = DemoLifetime.WaitForStopFileAsync(signal, () => false, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.False(File.Exists(signal));
    }

    [Fact]
    public async Task Browser_cancel_file_aborts_instead_of_approving_the_demo()
    {
        var signal = Path.Combine(Path.GetTempPath(), "FlyUpDemoSignal_" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllTextAsync(signal + ".cancel", "browser issue found");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DemoLifetime.WaitForStopFileAsync(signal, () => false));
            Assert.False(File.Exists(signal));
        }
        finally { File.Delete(signal + ".cancel"); }
    }

    [Fact]
    public void Running_demo_uses_an_isolated_copy_including_native_dependencies()
    {
        var root = Path.Combine(Path.GetTempPath(), "FlyUpRuntime_" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = Path.Combine(root, "build");
            Directory.CreateDirectory(Path.Combine(source, "runtimes", "win-x64", "native"));
            File.WriteAllText(Path.Combine(source, "EduFlyUp.Web.dll"), "old assembly");
            File.WriteAllText(Path.Combine(source, "runtimes", "win-x64", "native", "dependency.dll"), "native dependency");
            var runtime = Path.Combine(root, "demo");
            var assembly = DemoLifetime.StageWebRuntime(source, runtime);
            File.WriteAllText(Path.Combine(source, "EduFlyUp.Web.dll"), "rebuilt assembly");
            Assert.Equal("old assembly", File.ReadAllText(assembly));
            Assert.Equal("native dependency", File.ReadAllText(Path.Combine(runtime, "runtimes", "win-x64", "native", "dependency.dll")));
            Assert.Throws<InvalidOperationException>(() => DemoLifetime.StageWebRuntime(source, runtime));
        }
        finally
        {
            // The only recursively removed directory is the fresh GUID fixture root.
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("FlyUpRuntime_", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test cleanup target.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }

    private static HttpResponseMessage Response(string location) => new(HttpStatusCode.Redirect)
    {
        RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:59687/Admin"),
        Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) }
    };
}
