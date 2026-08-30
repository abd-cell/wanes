using System.ComponentModel.DataAnnotations;

namespace Wanes.Shareds.Attributes;

/// <summary>Validates that a numeric value is greater than zero.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class GreaterThanZeroAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value switch
        {
            null => false,
            int i => i > 0,
            long l => l > 0,
            double d => d > 0,
            decimal m => m > 0,
            _ => false,
        };

    public override string FormatErrorMessage(string name) => $"{name} must be greater than zero.";
}

/// <summary>Validates that a boolean flag is true (e.g. accepted terms).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is true;

    public override string FormatErrorMessage(string name) => $"{name} must be accepted.";
}

/// <summary>Marks a DTO property as included in free-text search filtering.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SearchableAttribute : Attribute;
