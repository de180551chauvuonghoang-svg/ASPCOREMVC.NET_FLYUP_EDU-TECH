using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AdminDemo.Tests")]

namespace AdminDemo;

internal static class DemoLifetime
{
    public static async Task WaitForStopFileAsync(string stopFile, Func<bool> hasExited, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stopFile);
        while (!File.Exists(stopFile))
        {
            if (hasExited()) throw new InvalidOperationException("Demo Web stopped before browser verification.");
            await Task.Delay(100, cancellationToken);
        }
    }
}
