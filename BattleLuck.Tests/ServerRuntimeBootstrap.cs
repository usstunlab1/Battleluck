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
        var configuredRoot = Environment.GetEnvironmentVariable("BATTLELUCK_SERVER_ROOT");
        var candidates = new[]
        {
            string.IsNullOrWhiteSpace(configuredRoot) ? null : Path.Combine(configuredRoot, "BepInEx", "interop"),
            @"C:\Users\ahmad\OneDrive\Desktop\DedicatedServerLauncher\VRisingServer\BepInEx\interop"
        };

        return candidates.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path) &&
            File.Exists(Path.Combine(path, "Unity.Entities.dll")) &&
            File.Exists(Path.Combine(path, "ProjectM.dll")));
    }
}
