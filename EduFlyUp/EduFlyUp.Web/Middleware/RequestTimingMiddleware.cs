using System.Diagnostics;

namespace EduFlyUp.Web.Middleware;

public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        _logger.LogInformation("⚡ [PERF] {Method} {Path} thực thi trong {Elapsed} ms",
            context.Request.Method, context.Request.Path, sw.ElapsedMilliseconds);
    }
}
