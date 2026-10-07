using System.Collections.Concurrent;

namespace Microsoft.AI.Local.Foundry;

/// <summary>Creates and caches handles, so each (model, device) pair has exactly one handle per process.</summary>
internal static class FoundryModelFactory
{
    private static readonly ConcurrentDictionary<(string Alias, LocalDevice Device), ILocalModel> Handles = new();

    public static ITextGenerationModel TextGeneration(FoundryModelDescriptor descriptor) => (ITextGenerationModel)Get(descriptor, LocalDevice.Auto);

    public static ITextEmbeddingModel TextEmbedding(FoundryModelDescriptor descriptor) => (ITextEmbeddingModel)Get(descriptor, LocalDevice.Auto);

    public static ISpeechToTextModel SpeechToText(FoundryModelDescriptor descriptor) => (ISpeechToTextModel)Get(descriptor, LocalDevice.Auto);

    public static ILocalModel Get(FoundryModelDescriptor descriptor, LocalDevice device) =>
        Handles.GetOrAdd((descriptor.Alias, device), static (key, d) => Create(d, key.Device), descriptor);

    private static ILocalModel Create(FoundryModelDescriptor descriptor, LocalDevice device) => descriptor.Task switch
    {
        FoundryModelTask.TextGeneration => new FoundryTextGenerationModel(descriptor, device),
        FoundryModelTask.TextEmbedding => new FoundryTextEmbeddingModel(descriptor, device),
        FoundryModelTask.SpeechToText => new FoundrySpeechToTextModel(descriptor, device),
        _ => throw new ArgumentOutOfRangeException(nameof(descriptor), descriptor.Task, "Unknown task type."),
    };
}
