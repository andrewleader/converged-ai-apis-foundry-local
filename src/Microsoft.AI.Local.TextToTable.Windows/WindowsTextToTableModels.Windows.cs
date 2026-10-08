using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Windows.AI.Text;
using NativeTextToTableConverter = Microsoft.Windows.AI.Text.TextToTableConverter;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new WindowsTextToTableModel(descriptor);
}

internal sealed class WindowsTextToTableModel(LocalModelDescriptor descriptor)
    : WindowsLanguageModelBase<ITextToTableConverter>(descriptor), ITextToTableModel
{
    protected override ITextToTableConverter Wrap(LanguageModel model) => new WindowsTextToTableConverter(model, this, new NativeTextToTableConverter(model));
}

internal sealed class WindowsTextToTableConverter : WindowsTextSkillClientBase, ITextToTableConverter
{
    private readonly NativeTextToTableConverter _native;

    public WindowsTextToTableConverter(LanguageModel model, ILocalModel handle, NativeTextToTableConverter native)
        : base(model, handle, native)
    {
        _native = native;
        Metadata = new TextToTableConverterMetadata(WindowsAIProvider.ProviderName, handle.Id);
    }

    public TextToTableConverterMetadata Metadata { get; }

    protected override object MetadataObject => Metadata;

    public async Task<TextTableResult> ConvertAsync(string text, TextToTableOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = await _native.ConvertAsync(text).AsTask(cancellationToken).ConfigureAwait(false);
        WindowsAIInterop.ThrowIfFailed(result.Status, result.ExtendedError, Handle.Id);
        IReadOnlyList<IReadOnlyList<string>> rows = [.. result.GetRows().Select(r => (IReadOnlyList<string>)r.GetColumns())];
        return Complete(new TextTableResult(rows), result.Status, result.ExtendedError, result);
    }
}
