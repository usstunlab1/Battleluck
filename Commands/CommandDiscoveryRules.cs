namespace BattleLuck.Commands;

public static class CommandDiscoveryRules
{
    public static bool IsCommandContainer(Type type) =>
        type.IsClass &&
        !type.IsGenericType &&
        (!type.IsAbstract || type.IsSealed);
}
