using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Windows.AI.Text;
using NativeTextSummarizer = Microsoft.Windows.AI.Text.TextSummarizer;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new WindowsTextSummarizationModel(descriptor);
}

internal sealed class WindowsTextSummarizationModel(LocalModelDescriptor descriptor)
    : WindowsLanguageModelBase<ITextSummarizer>(descriptor), ITextSummarizationModel
{
    protected override ITextSummarizer Wrap(LanguageModel model) => new WindowsTextSummarizer(model, this, new NativeTextSummarizer(model));
}

internal sealed class WindowsTextSummarizer : WindowsTextSkillClientBase, ITextSummarizer
{
    private readonly NativeTextSummarizer _native;

    public WindowsTextSummarizer(LanguageModel model, ILocalModel handle, NativeTextSummarizer native)
        : base(model, handle, native)
    {
        _native = native;
        Metadata = new TextSummarizerMetadata(WindowsAIProvider.ProviderName, handle.Id);
    }

    public TextSummarizerMetadata Metadata { get; }

    protected override object MetadataObject => Metadata;

    public async Task<TextSummarizationResult> SummarizeAsync(string text, TextSummarizationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var operation = options?.Format == TextSummaryFormat.Paragraph ? _native.SummarizeParagraphAsync(text) : _native.SummarizeAsync(text);
        var result = await operation.AsTask(cancellationToken).ConfigureAwait(false);
        return Complete(new TextSummarizationResult(result.Text ?? string.Empty), result.Status, result.ExtendedError, result);
    }
}
