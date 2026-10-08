using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.AI.Local.Catalog.Generators;

/// <summary>
/// A task type: the manifest name, the package segment (<c>Microsoft.AI.Local.&lt;Segment&gt;</c>), the catalog class,
/// the model interface and the client contract.
/// </summary>
internal sealed class CatalogTask(string name, string segment, string catalogClass, string modelInterface, string clientType, string text)
{
    private const string Meai = "global::Microsoft.Extensions.AI.";
    private const string Local = "global::Microsoft.AI.Local.";

    public static readonly IReadOnlyList<CatalogTask> All =
    [
        new("text-generation", "TextGeneration", "LanguageModels", "ITextGenerationModel", Meai + "IChatClient", "text generation"),
        new("text-embedding", "TextEmbedding", "TextEmbeddingModels", "ITextEmbeddingModel", Meai + "IEmbeddingGenerator<string, " + Meai + "Embedding<float>>", "text embedding"),
        new("speech-to-text", "SpeechToText", "SpeechToTextModels", "ISpeechToTextModel", Meai + "ISpeechToTextClient", "speech to text"),
        new("text-summarization", "TextSummarization", "TextSummarizationModels", "ITextSummarizationModel", Local + "ITextSummarizer", "text summarization"),
        new("text-rewrite", "TextRewrite", "TextRewriteModels", "ITextRewriteModel", Local + "ITextRewriter", "text rewrite"),
        new("text-to-table", "TextToTable", "TextToTableModels", "ITextToTableModel", Local + "ITextToTableConverter", "text to table"),
        new("image-text-recognition", "ImageTextRecognition", "ImageTextRecognitionModels", "ITextRecognitionModel", Local + "ITextRecognizer", "text recognition (OCR)"),
        new("image-description", "ImageDescription", "ImageDescriptionModels", "IImageDescriptionModel", Local + "IImageDescriber", "image description"),
        new("image-scaling", "ImageScaling", "ImageScalingModels", "IImageScalingModel", Local + "IImageScaler", "image super-resolution"),
        new("image-segmentation", "ImageSegmentation", "ImageSegmentationModels", "IImageSegmentationModel", Local + "IImageSegmenter", "image segmentation"),
        new("image-object-removal", "ImageObjectRemoval", "ImageObjectRemovalModels", "IObjectRemovalModel", Local + "IImageObjectRemover", "object removal"),
    ];

    public string Name { get; } = name;

    public string Segment { get; } = segment;

    public string CatalogClass { get; } = catalogClass;

    public string ModelInterface { get; } = modelInterface;

    public string ClientType { get; } = clientType;

    public string Text { get; } = text;

    /// <summary>Gets the NuGet package ID of the task's contract package.</summary>
    public string Package => "Microsoft.AI.Local." + Segment;

    public static CatalogTask? Find(string name) => All.FirstOrDefault(t => t.Name == name);

    public static CatalogTask? FindBySegment(string segment) => All.FirstOrDefault(t => string.Equals(t.Segment, segment, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The provider block of a manifest.</summary>
internal sealed class CatalogProvider(string name, string @namespace, string idPrefix, string? remarks)
{
    public string Name { get; } = name;

    /// <summary>Gets the namespace of the provider's types, for example <c>Microsoft.AI.Local.Foundry</c>.</summary>
    public string Namespace { get; } = @namespace;

    public string IdPrefix { get; } = idPrefix;

    public string? Remarks { get; } = remarks;

    /// <summary>Gets the NuGet package ID that implements <paramref name="task"/> for this provider.</summary>
    public string PackageFor(CatalogTask task) => task.Package + "." + Name;

    /// <summary>Gets the full name of the registration type of the provider's <paramref name="task"/> package.</summary>
    public string RegistrationTypeFor(CatalogTask task) => Namespace + "." + Name + task.Segment + "Registration";
}

/// <summary>A parsed and validated provider manifest (<c>eng/catalog/&lt;provider&gt;-models.json</c>).</summary>
internal sealed class CatalogManifest
{
    public static readonly string[] KnownPlatforms = ["win-x64", "win-arm64", "osx-arm64", "osx-x64", "linux-x64", "linux-arm64"];

    private static readonly Regex IdentifierPattern = new("^[A-Z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly Regex NamePattern = new("^[A-Za-z][A-Za-z0-9.]*$", RegexOptions.CultureInvariant);
    private static readonly Regex AliasPattern = new("^[a-z0-9][a-z0-9.\\-]*$", RegexOptions.CultureInvariant);

    public CatalogManifest(CatalogProvider provider, IReadOnlyList<CatalogModel> models)
    {
        Provider = provider;
        Models = models;
    }

    public CatalogProvider Provider { get; }

    public IReadOnlyList<CatalogModel> Models { get; }

    /// <summary>Parses and validates one manifest. Throws <see cref="FormatException"/> with an actionable message.</summary>
    public static CatalogManifest Parse(string json)
    {
        if (MiniJson.Parse(json) is not Dictionary<string, object?> root)
        {
            throw new FormatException("The manifest must be a JSON object.");
        }

        if (!root.TryGetValue("provider", out var providerValue) || providerValue is not Dictionary<string, object?> providerEntry)
        {
            throw new FormatException("The manifest must have a 'provider' object.");
        }

        var provider = new CatalogProvider(
            RequiredName(providerEntry, "name", "provider"),
            RequiredName(providerEntry, "namespace", "provider"),
            RequiredString(providerEntry, "idPrefix", "provider"),
            OptionalString(providerEntry, "remarks"));
        if (!AliasPattern.IsMatch(provider.IdPrefix))
        {
            throw new FormatException($"provider.idPrefix '{provider.IdPrefix}' must be lowercase letters, digits, '.' and '-'.");
        }

        if (!root.TryGetValue("models", out var modelsValue) || modelsValue is not List<object?> modelList)
        {
            throw new FormatException("The manifest must have a 'models' array.");
        }

        var models = new List<CatalogModel>();
        for (var i = 0; i < modelList.Count; i++)
        {
            if (modelList[i] is not Dictionary<string, object?> entry)
            {
                throw new FormatException($"models[{i}] must be an object.");
            }

            models.Add(ParseModel(provider, entry, $"models[{i}]"));
        }

        var manifest = new CatalogManifest(provider, models);
        Validate([manifest]);
        return manifest;
    }

    /// <summary>
    /// Validates manifests together: model IDs are unique, each catalog class has unique property names, and
    /// retirement replacements name a supported model of the same catalog class.
    /// </summary>
    public static void Validate(IReadOnlyList<CatalogManifest> manifests)
    {
        var models = manifests.SelectMany(m => m.Models).ToList();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var properties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            if (!ids.Add(model.Id))
            {
                throw new FormatException($"Duplicate model ID '{model.Id}'.");
            }

            if (model.Property == "All" || !properties.Add(model.Task.CatalogClass + "." + model.Property))
            {
                throw new FormatException($"Model '{model.Id}': {model.Task.CatalogClass}.{model.Property} is duplicate or reserved.");
            }
        }

        foreach (var model in models)
        {
            if (model.RetiredReplacement is { } replacement &&
                !models.Any(m => m.Task == model.Task && m.Property == replacement && m.RetiredMessage is null))
            {
                throw new FormatException(
                    $"Model '{model.Id}': retired.replacement '{replacement}' must name a {model.Task.CatalogClass} model that isn't retired.");
            }
        }
    }

    private static CatalogModel ParseModel(CatalogProvider provider, Dictionary<string, object?> entry, string path)
    {
        var alias = RequiredString(entry, "alias", path);
        if (!AliasPattern.IsMatch(alias))
        {
            throw new FormatException($"{path}.alias '{alias}' must be lowercase letters, digits, '.' and '-'.");
        }

        var property = RequiredString(entry, "property", path);
        if (!IdentifierPattern.IsMatch(property))
        {
            throw new FormatException($"{path}.property '{property}' must be a PascalCase C# identifier.");
        }

        var taskName = RequiredString(entry, "task", path);
        var task = CatalogTask.Find(taskName)
            ?? throw new FormatException($"{path}.task '{taskName}' is not one of: {string.Join(", ", CatalogTask.All.Select(t => t.Name))}.");

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
            provider,
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

    private static string RequiredName(Dictionary<string, object?> entry, string name, string path)
    {
        var value = RequiredString(entry, name, path);
        return NamePattern.IsMatch(value) ? value : throw new FormatException($"{path}.{name} '{value}' must be a dotted identifier.");
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

/// <summary>One model of a manifest.</summary>
internal sealed class CatalogModel(
    CatalogProvider provider,
    string alias,
    string property,
    string displayName,
    string? publisher,
    string? description,
    CatalogTask task,
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
    public CatalogProvider Provider { get; } = provider;

    public string Id => Provider.IdPrefix + "/" + Alias;

    public string Package => Provider.PackageFor(Task);

    public string RegistrationType => Provider.RegistrationTypeFor(Task);

    public string Alias { get; } = alias;

    public string Property { get; } = property;

    public string DisplayName { get; } = displayName;

    public string? Publisher { get; } = publisher;

    public string? Description { get; } = description;

    public CatalogTask Task { get; } = task;

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
