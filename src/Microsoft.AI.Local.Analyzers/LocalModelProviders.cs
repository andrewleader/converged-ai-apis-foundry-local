using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.AI.Local.Analyzers;

/// <summary>Reads the provider attributes of Microsoft.AI.Local from a compilation's references.</summary>
internal static class LocalModelProviders
{
    public const string ProviderAttributeName = "Microsoft.AI.Local.Providers.LocalModelProviderAttribute";
    public const string RequiresProviderAttributeName = "Microsoft.AI.Local.Providers.RequiresLocalModelProviderAttribute";

    /// <summary>Returns the <c>[assembly: LocalModelProvider]</c> declarations of the referenced assemblies, ordered by package ID.</summary>
    public static ImmutableArray<(string PackageId, INamedTypeSymbol RegistrationType)> GetReferencedProviders(Compilation compilation)
    {
        var attributeType = compilation.GetTypeByMetadataName(ProviderAttributeName);
        if (attributeType is null)
        {
            return ImmutableArray<(string, INamedTypeSymbol)>.Empty;
        }

        var providers = new List<(string, INamedTypeSymbol)>();
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType) &&
                    attribute.ConstructorArguments.Length == 2 &&
                    attribute.ConstructorArguments[0].Value is string packageId &&
                    attribute.ConstructorArguments[1].Value is INamedTypeSymbol registrationType)
                {
                    providers.Add((packageId, registrationType));
                }
            }
        }

        return providers.OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
    }

    /// <summary>Reads <c>[RequiresLocalModelProvider]</c> from a catalog handle property.</summary>
    public static (string PackageId, string? ProviderName)? GetRequiredProvider(IPropertySymbol property, INamedTypeSymbol requiresAttributeType)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, requiresAttributeType) ||
                attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Value is not string packageId)
            {
                continue;
            }

            string? providerName = null;
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "ProviderName")
                {
                    providerName = argument.Value.Value as string;
                }
            }

            return (packageId, providerName);
        }

        return null;
    }
}
