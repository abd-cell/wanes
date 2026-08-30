namespace Wanes;

/// <summary>Static access to the ambient <see cref="HttpContext"/> where DI is awkward.</summary>
public static class AppHttpContext
{
    private static IHttpContextAccessor? _accessor;

    public static void Configure(IHttpContextAccessor accessor) => _accessor = accessor;

    public static HttpContext? Current => _accessor?.HttpContext;
}
