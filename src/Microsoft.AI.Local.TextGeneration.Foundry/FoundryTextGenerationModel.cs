using Microsoft.AI.Foundry.Local;
using Microsoft.AI.Local.Foundry.Providers;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Foundry;

internal static class FoundryModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => FoundryTextGenerationModel.Get(descriptor, LocalDevice.Auto);
}

/// <summary>A Foundry Local text-generation model handle.</summary>
internal sealed class FoundryTextGenerationModel : FoundryModelHandle<IChatClient>, ITextGenerationModel
{
    private FoundryTextGenerationModel(LocalModelDescriptor descriptor, LocalDevice device)
        : base(descriptor, device)
    {
    }

    /// <summary>Gets or sets how engines are created from loaded variants (replaced in tests).</summary>
    internal static Func<IFoundryModelVariant, IFoundryChatEngine> ChatEngineFactory { get; set; } = static variant => new FoundryLocalChatEngine((IModel)variant.Native);

    public static FoundryTextGenerationModel Get(LocalModelDescriptor descriptor, LocalDevice device) =>
        GetOrCreate(descriptor, device, static (d, dev) => new FoundryTextGenerationModel(d, dev));

    protected override FoundryModelHandle<IChatClient> CreateForDevice(LocalModelDescriptor descriptor, LocalDevice device) => Get(descriptor, device);

    protected override IChatClient CreateClient(IFoundryModelVariant variant, IDisposable lease) => new FoundryChatClient(this, variant, ChatEngineFactory(variant), lease);
}
