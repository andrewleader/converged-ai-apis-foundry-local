using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.AI.Local.Foundry.Generators;

/// <summary>A parsed and validated <c>foundry-models.json</c>.</summary>
internal sealed class CatalogManifest
{
    public static readonly string[] KnownTasks = ["text-generation", "text-embedding", "speech-to-text"];

    public static readonly string[] KnownPlatforms = ["win-x64", "win-arm64", "osx-arm64", "osx-x64", "linux-x64", "linux-arm64"];

    private static readonly Regex IdentifierPattern = new("^[A-Z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    public CatalogManifest(IReadOnlyList<CatalogModel> models) => Models = models;

    public IReadOnlyList<CatalogModel> Models { get; }

    /// <summary>Parses and validates the manifest. Throws <see cref="FormatException"/> with an actionable message.</summary>
    public static CatalogManifest Parse(string json)
    {
        if (MiniJson.Parse(json) is not Dictionary<string, object?> root)
        {
            throw new FormatException("The manifest must be a JSON object.");
        }

        if (!root.TryGetValue("models", out var modelsValue) || modelsValue is not List<object?> modelList)
        {
            throw new FormatException("The manifest must have a 'models' array.");
        }

        var models = new List<CatalogModel>();
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var properties = new HashSet<string>(StringComparer.Ordinal) { "All" };
        for (var i = 0; i < modelList.Count; i++)
        {
            if (modelList[i] is not Dictionary<string, object?> entry)
            {
                throw new FormatException($"models[{i}] must be an object.");
            }

            var model = ParseModel(entry, $"models[{i}]");
            if (!aliases.Add(model.Alias))
            {
                throw new FormatException($"models[{i}]: duplicate alias '{model.Alias}'.");
            }

            if (!properties.Add(model.Property))
            {
                throw new FormatException($"models[{i}]: duplicate or reserved property name '{model.Property}'.");
            }

            models.Add(model);
        }

        foreach (var model in models)
        {
            if (model.RetiredReplacement is { } replacement && !models.Any(m => m.Property == replacement && m.RetiredMessage is null))
            {
                throw new FormatException($"Model '{model.Alias}': retired.replacement '{replacement}' must name a model that isn't retired.");
            }
        }

        return new CatalogManifest(models);
    }

    private static CatalogModel ParseModel(Dictionary<string, object?> entry, string path)
    {
        var alias = RequiredString(entry, "alias", path);
        var property = RequiredString(entry, "property", path);
        if (!IdentifierPattern.IsMatch(property))
        {
            throw new FormatException($"{path}.property '{property}' must be a PascalCase C# identifier.");
        }

        var task = RequiredString(entry, "task", path);
        if (Array.IndexOf(KnownTasks, task) < 0)
        {
            throw new FormatException($"{path}.task '{task}' is not one of: {string.Join(", ", KnownTasks)}.");
        }

        if (!entry.TryGetValue("platforms", out var platformsValue) || platformsValue is not List<object?> platformList || platformList.Count == 0)
        {
            throw new FormatException($"{path}.platforms must be a non-empty array.");
        }

        var platforms = new List<string>();
        foreach (var p in platformList)
        {
            if (p is not string platform || Array.IndexOf(KnownPlatforms, platform) < 0)
            {
                throw new FormatException($"{path}.platforms contains '{p}'; expected one of: {string.Join(", ", KnownPlatforms)}.");
            }

            platforms.Add(platform);
        }

        var capabilities = entry.TryGetValue("capabilities", out var c) && c is Dictionary<string, object?> caps ? caps : new Dictionary<string, object?>();
        string? retiredMessage = null;
        string? retiredReplacement = null;
        if (entry.TryGetValue("retired", out var retiredValue) && retiredValue is Dictionary<string, object?> retired)
        {
            retiredMessage = RequiredString(retired, "message", path + ".retired");
            retiredReplacement = OptionalString(retired, "replacement");
        }

        return new CatalogModel(
            alias,
            property,
            OptionalString(entry, "displayName") ?? alias,
            OptionalString(entry, "publisher"),
            OptionalString(entry, "description"),
            task,
            platforms,
            entry.TryGetValue("crossPlatform", out var cp) && cp is true,
            Flag(capabilities, "streaming"),
            Flag(capabilities, "toolCalling"),
            Flag(capabilities, "imageInput"),
            Flag(capabilities, "audioInput"),
            Flag(capabilities, "structuredOutput"),
            Flag(capabilities, "reasoning"),
            Number(capabilities, "contextLength", path),
            Number(capabilities, "maxOutputTokens", path),
            retiredMessage,
            retiredReplacement);
    }

    private static string RequiredString(Dictionary<string, object?> entry, string name, string path) =>
        OptionalString(entry, name) is { Length: > 0 } value ? value : throw new FormatException($"{path}.{name} is required and must be a non-empty string.");

    private static string? OptionalString(Dictionary<string, object?> entry, string name) =>
        entry.TryGetValue(name, out var value) ? value as string : null;

    private static bool Flag(Dictionary<string, object?> entry, string name) =>
        entry.TryGetValue(name, out var value) && value is true;

    private static int? Number(Dictionary<string, object?> entry, string name, string path)
    {
        if (!entry.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        if (value is double d && d > 0 && d <= int.MaxValue && Math.Floor(d) == d)
        {
            return (int)d;
        }

        throw new FormatException($"{path}.capabilities.{name} must be a positive integer.");
    }
}

/// <summary>One model of the manifest.</summary>
internal sealed class CatalogModel(
    string alias,
    string property,
    string displayName,
    string? publisher,
    string? description,
    string task,
    IReadOnlyList<string> platforms,
    bool crossPlatform,
    bool streaming,
    bool toolCalling,
    bool imageInput,
    bool audioInput,
    bool structuredOutput,
    bool reasoning,
    int? contextLength,
    int? maxOutputTokens,
    string? retiredMessage,
    string? retiredReplacement)
{
    public string Alias { get; } = alias;

    public string Property { get; } = property;

    public string DisplayName { get; } = displayName;

    public string? Publisher { get; } = publisher;

    public string? Description { get; } = description;

    public string Task { get; } = task;

    public IReadOnlyList<string> Platforms { get; } = platforms;

    public bool CrossPlatform { get; } = crossPlatform;

    public bool Streaming { get; } = streaming;

    public bool ToolCalling { get; } = toolCalling;

    public bool ImageInput { get; } = imageInput;

    public bool AudioInput { get; } = audioInput;

    public bool StructuredOutput { get; } = structuredOutput;

    public bool Reasoning { get; } = reasoning;

    public int? ContextLength { get; } = contextLength;

    public int? MaxOutputTokens { get; } = maxOutputTokens;

    public string? RetiredMessage { get; } = retiredMessage;

    public string? RetiredReplacement { get; } = retiredReplacement;
}
