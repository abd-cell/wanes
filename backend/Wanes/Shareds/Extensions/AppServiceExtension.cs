using System.Reflection;
using Wanes.Shareds.Attributes;

namespace Wanes.Shareds.Extensions;

/// <summary>
/// Convention-based DI registration. A service is auto-registered when the
/// injectable attribute is placed on the <b>interface</b> (Parkaway style) —
/// the single implementing class is registered against it. For infrastructure
/// types the attribute may also sit on the class itself.
/// </summary>
public static class AppServiceExtension
{
    public static IServiceCollection RegisterTypes(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var concreteTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .ToList();

        // 1) Attribute on the interface → register the implementing class against it.
        foreach (var contract in assembly.GetTypes().Where(t => t.IsInterface))
        {
            var lifetime = ResolveLifetime(contract);
            if (lifetime is null) continue;

            var implementation = concreteTypes.FirstOrDefault(contract.IsAssignableFrom);
            if (implementation is not null)
                services.Add(new ServiceDescriptor(contract, implementation, lifetime.Value));
        }

        // 2) Attribute on the class → register against its I{Name} interface + itself.
        foreach (var type in concreteTypes)
        {
            var lifetime = ResolveLifetime(type);
            if (lifetime is null) continue;

            var contract = type.GetInterfaces().FirstOrDefault(i => i.Name == $"I{type.Name}");
            if (contract is not null && ResolveLifetime(contract) is null)
                services.Add(new ServiceDescriptor(contract, type, lifetime.Value));

            services.Add(new ServiceDescriptor(type, type, lifetime.Value));
        }

        return services;
    }

    private static ServiceLifetime? ResolveLifetime(MemberInfo type)
    {
        if (type.GetCustomAttribute<ScopedInjectableAttribute>() is not null) return ServiceLifetime.Scoped;
        if (type.GetCustomAttribute<TransientInjectableAttribute>() is not null) return ServiceLifetime.Transient;
        if (type.GetCustomAttribute<SingletonInjectableAttribute>() is not null) return ServiceLifetime.Singleton;
        return null;
    }
}
