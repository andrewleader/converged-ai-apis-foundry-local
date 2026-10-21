using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new Unsupported(descriptor);

    private sealed class Unsupported(LocalModelDescriptor descriptor) : WindowsUnsupportedModel<IImageObjectRemover>(descriptor), IObjectRemovalModel;
}
