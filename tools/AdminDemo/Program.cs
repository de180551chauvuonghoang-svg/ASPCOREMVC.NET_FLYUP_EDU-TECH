using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AdminDemo;

var options = args.Select((value, index) => (value, index)).Where(x => x.value.StartsWith("--"))
    .ToDictionary(x => x.value, x => x.index + 1 < args.Length && !args[x.index + 1].StartsWith("--") ? args[x.index + 1] : "true");
var root = Path.GetFullPath(options.GetValueOrDefault("--repo", Path.Combine(AppContext.BaseDirectory, "../../../../../")));
var output = Path.Combine(root, ".admin-demo");
Directory.CreateDirectory(output);
var report = new DemoReport(DateTimeOffset.UtcNow);
Process? web = null;
StreamWriter? log = null;
var json = new JsonSerializerOptions { WriteIndented = true };
try
{
    var server = options.GetValueOrDefault("--server", @".\SQLEXPRESS");
    // A fresh database for every run; never accept an existing application DB name.
    var session = new DemoSession(server, "FlyUpAdminDemo_" + Guid.NewGuid().ToString("N"),
        "DemoOnly@" + Guid.NewGuid().ToString("N"));
    report.Database = session.Database;
    Console.WriteLine($"Preparing isolated demo database {session.Database} on {server}.");
    await DemoData.PrepareAsync(session);
    using (var port = new TcpListener(IPAddress.Loopback, 0))
    {
        port.Start();
        session.BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)port.LocalEndpoint).Port;
    }
    var webRoot = Path.Combine(root, "EduFlyUp", "EduFlyUp.Web");
    var assembly = Path.Combine(webRoot, "bin", "Release", "net8.0", "EduFlyUp.Web.dll");
    if (!File.Exists(assembly)) throw new InvalidOperationException("Build the Web project in Release before running the demo.");
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = webRoot, UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    start.ArgumentList.Add(assembly);
    start.ArgumentList.Add("--urls"); start.ArgumentList.Add(session.BaseUrl);
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["DOTNET_ENVIRONMENT"] = "Development";
    start.Environment["ConnectionStrings__DefaultConnection"] = session.Connection;
    start.Environment["ASPNETCORE_HTTPS_PORT"] = "";
    log = new StreamWriter(Path.Combine(output, "web.log")) { AutoFlush = true };
    web = new Process { StartInfo = start };
    void Log(object sender, DataReceivedEventArgs line) { if (line.Data is not null) lock (log) log.WriteLine(line.Data); }
    web.OutputDataReceived += Log; web.ErrorDataReceived += Log;
    if (!web.Start()) throw new InvalidOperationException("Could not start the Web process.");
    web.BeginOutputReadLine(); web.BeginErrorReadLine();
    session.ProcessId = web.Id;
    await File.WriteAllTextAsync(Path.Combine(output, "session.json"), JsonSerializer.Serialize(session, json));
    using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    var ready = false;
    for (var attempt = 0; attempt < 60 && !web.HasExited; attempt++)
    {
        try { using var response = await probe.GetAsync(session.BaseUrl + "/Account/Login"); ready = response.IsSuccessStatusCode; }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        if (ready) break;
        await Task.Delay(500);
    }
    if (!ready) throw new InvalidOperationException("Web did not become ready. See .admin-demo/web.log.");
    report.BaseUrl = session.BaseUrl;
    await new HttpChecks(session, report).RunAsync();
    report.Passed = true;
    report.FinishedUtc = DateTimeOffset.UtcNow;
    await File.WriteAllTextAsync(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, json));
    Console.WriteLine($"PASS: {report.Checks.Count} integration scenarios. Demo: {session.BaseUrl}/Admin");
    Console.WriteLine("Demo accounts and generated password: .admin-demo/session.json (local only; ignored by Git).");
    Console.WriteLine("Report: .admin-demo/report.json. Database is retained for the demo; no application database was modified.");
    if (options.ContainsKey("--keep-open"))
    {
        Console.WriteLine("Leave this window open for browser verification. Press Enter when finished to stop the demo server.");
        Console.ReadLine();
    }
    return 0;
}
catch (Exception error)
{
    report.Passed = false;
    report.FinishedUtc = DateTimeOffset.UtcNow;
    report.Error = error.Message;
    await File.WriteAllTextAsync(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, json));
    Console.Error.WriteLine("FAIL: " + error.Message);
    return 1;
}
finally
{
    if (web is { HasExited: false }) { web.Kill(entireProcessTree: true); await web.WaitForExitAsync(); }
    web?.Dispose();
    log?.Dispose();
}
