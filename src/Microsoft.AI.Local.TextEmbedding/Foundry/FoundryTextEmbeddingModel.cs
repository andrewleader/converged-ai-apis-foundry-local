using Microsoft.AI.Foundry.Local;
using Microsoft.AI.Local.Foundry.Providers;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Foundry;

internal static class FoundryModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => FoundryTextEmbeddingModel.Get(descriptor, LocalDevice.Auto);
}

/// <summary>A Foundry Local text-embedding model handle.</summary>
internal sealed class FoundryTextEmbeddingModel : FoundryModelHandle<IEmbeddingGenerator<string, Embedding<float>>>, ITextEmbeddingModel
{
    private FoundryTextEmbeddingModel(LocalModelDescriptor descriptor, LocalDevice device)
        : base(descriptor, device)
    {
    }

    /// <summary>Gets or sets how engines are created from loaded variants (replaced in tests).</summary>
    internal static Func<IFoundryModelVariant, IFoundryEmbeddingEngine> EmbeddingEngineFactory { get; set; } = static variant => new FoundryLocalEmbeddingEngine((IModel)variant.Native);

    public static FoundryTextEmbeddingModel Get(LocalModelDescriptor descriptor, LocalDevice device) =>
        GetOrCreate(descriptor, device, static (d, dev) => new FoundryTextEmbeddingModel(d, dev));

    protected override FoundryModelHandle<IEmbeddingGenerator<string, Embedding<float>>> CreateForDevice(LocalModelDescriptor descriptor, LocalDevice device) => Get(descriptor, device);

    protected override IEmbeddingGenerator<string, Embedding<float>> CreateClient(IFoundryModelVariant variant, IDisposable lease) => new FoundryEmbeddingGenerator(this, variant, EmbeddingEngineFactory(variant), lease);
}
