using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace BattleLuck.Tests;

internal static class ServerRuntimeBootstrap
{
    static string? _interopPath;

    [ModuleInitializer]
    internal static void Initialize()
    {
        _interopPath = FindInteropPath();
        if (_interopPath == null)
            return;

        AssemblyLoadContext.Default.Resolving += ResolveServerAssembly;
    }

    internal static bool IsAvailable => _interopPath != null;

    static Assembly? ResolveServerAssembly(AssemblyLoadContext context, AssemblyName name)
    {
        if (_interopPath == null || string.IsNullOrWhiteSpace(name.Name))
            return null;

        var candidate = Path.Combine(_interopPath, name.Name + ".dll");
        return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
    }

    static string? FindInteropPath()
    {
        var directPath = Environment.GetEnvironmentVariable("BATTLELUCK_TEST_INTEROP_PATH");
        var configuredRoot = Environment.GetEnvironmentVariable("BATTLELUCK_SERVER_ROOT");
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var candidates = new List<string?>
        {
            directPath,
            string.IsNullOrWhiteSpace(configuredRoot) ? null : Path.Combine(configuredRoot, "BepInEx", "interop"),
            string.IsNullOrWhiteSpace(desktop) ? null : Path.Combine(desktop, "DedicatedServerLauncher", "VRisingServer", "BepInEx", "interop")
        };

        candidates.AddRange(FindNuGetReferencePaths());

        return candidates.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path) &&
            File.Exists(Path.Combine(path, "Unity.Entities.dll")) &&
            File.Exists(Path.Combine(path, "ProjectM.dll")));
    }

    static IEnumerable<string> FindNuGetReferencePaths()
    {
        var packageRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrWhiteSpace(packageRoot))
            packageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        var package = Path.Combine(packageRoot, "vampirereferenceassemblies");
        if (!Directory.Exists(package)) yield break;

        foreach (var version in Directory.GetDirectories(package).OrderByDescending(value => value, StringComparer.OrdinalIgnoreCase))
            yield return Path.Combine(version, "ref", "net6.0");
    }
}
