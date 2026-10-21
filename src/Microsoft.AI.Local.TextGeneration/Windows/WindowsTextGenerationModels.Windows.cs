using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Windows.AI.Text;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new PhiSilicaModel(descriptor);
}

/// <summary>Phi Silica as a chat model.</summary>
internal sealed class PhiSilicaModel(LocalModelDescriptor descriptor)
    : WindowsLanguageModelBase<IChatClient>(descriptor), ITextGenerationModel
{
    protected override IChatClient Wrap(LanguageModel model) => new PhiSilicaChatClient(model, this);
}
