using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AdminDemo.Tests")]

namespace AdminDemo;

internal static class DemoLifetime
{
    public static async Task WaitForStopFileAsync(string stopFile, Func<bool> hasExited, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stopFile);
        cancellationToken.ThrowIfCancellationRequested();
        while (!File.Exists(stopFile))
        {
            if (File.Exists(stopFile + ".cancel")) throw new OperationCanceledException("Browser verification was canceled.");
            if (hasExited()) throw new InvalidOperationException("Demo Web stopped before browser verification.");
            await Task.Delay(100, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(stopFile + ".cancel")) throw new OperationCanceledException("Browser verification was canceled.");
    }

    public static string StageWebRuntime(string source, string destination)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        if (destination.Equals(source, StringComparison.OrdinalIgnoreCase) ||
            destination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Directory.Exists(destination))
            throw new InvalidOperationException("The demo runtime must use a fresh directory outside the build output.");
        if (!File.Exists(Path.Combine(source, "EduFlyUp.Web.dll"))) throw new FileNotFoundException("Build the Web project in Release before running the demo.");
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var copy = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file, copy, overwrite: false);
        }
        return Path.Combine(destination, "EduFlyUp.Web.dll");
    }
}
