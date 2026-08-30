namespace Wanes.Shareds.Attributes;

/// <summary>
/// Marks a service for automatic scoped registration by the assembly scanner in
/// <c>AppServiceExtension.RegisterTypes()</c>. Place it on the service
/// <b>interface</b> (preferred) or on the implementation class.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false)]
public sealed class ScopedInjectableAttribute : Attribute;

/// <summary>Marks a service for automatic transient registration (interface or class).</summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false)]
public sealed class TransientInjectableAttribute : Attribute;

/// <summary>Marks a service for automatic singleton registration (interface or class).</summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false)]
public sealed class SingletonInjectableAttribute : Attribute;
