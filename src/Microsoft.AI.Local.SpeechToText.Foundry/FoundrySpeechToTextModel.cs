using Microsoft.AI.Foundry.Local;
using Microsoft.AI.Local.Foundry.Providers;
using Microsoft.AI.Local.Foundry.Runtime;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Foundry;

internal static class FoundryModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => FoundrySpeechToTextModel.Get(descriptor, LocalDevice.Auto);
}

/// <summary>A Foundry Local speech-to-text model handle.</summary>
internal sealed class FoundrySpeechToTextModel : FoundryModelHandle<ISpeechToTextClient>, ISpeechToTextModel
{
    private FoundrySpeechToTextModel(LocalModelDescriptor descriptor, LocalDevice device)
        : base(descriptor, device)
    {
    }

    /// <summary>Gets or sets how engines are created from loaded variants (replaced in tests).</summary>
    internal static Func<IFoundryModelVariant, IFoundrySpeechEngine> SpeechEngineFactory { get; set; } = static variant => new FoundryLocalSpeechEngine((IModel)variant.Native);

    public static FoundrySpeechToTextModel Get(LocalModelDescriptor descriptor, LocalDevice device) =>
        GetOrCreate(descriptor, device, static (d, dev) => new FoundrySpeechToTextModel(d, dev));

    protected override FoundryModelHandle<ISpeechToTextClient> CreateForDevice(LocalModelDescriptor descriptor, LocalDevice device) => Get(descriptor, device);

    protected override ISpeechToTextClient CreateClient(IFoundryModelVariant variant, IDisposable lease) => new FoundrySpeechToTextClient(this, variant, SpeechEngineFactory(variant), lease);
}
