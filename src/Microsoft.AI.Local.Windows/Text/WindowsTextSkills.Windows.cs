using Microsoft.Windows.AI.Text;
using NativeTextRewriteTone = Microsoft.Windows.AI.Text.TextRewriteTone;
using NativeTextRewriter = Microsoft.Windows.AI.Text.TextRewriter;
using NativeTextSummarizer = Microsoft.Windows.AI.Text.TextSummarizer;
using NativeTextToTableConverter = Microsoft.Windows.AI.Text.TextToTableConverter;

namespace Microsoft.AI.Local.Windows;

/// <summary>Base class of the clients that wrap a Phi Silica text skill.</summary>
internal abstract class WindowsTextSkillClient(LanguageModel model, ILocalModel handle, object native) : ILocalAIClient
{
    protected abstract object MetadataObject { get; }

    protected LanguageModel Model { get; } = model;

    protected ILocalModel Handle { get; } = handle;

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType.IsInstanceOfType(this) ? this
            : serviceType.IsInstanceOfType(native) ? native
            : serviceType.IsInstanceOfType(Model) ? Model
            : serviceType.IsInstanceOfType(MetadataObject) ? MetadataObject
            : serviceType.IsInstanceOfType(Handle) ? Handle
            : null;
    }

    public void Dispose() => Model.Dispose();

    protected TResult Complete<TResult>(TResult result, LanguageModelResponseStatus status, Exception? error, object raw)
        where TResult : LocalAIResult
    {
        WindowsInterop.ThrowIfFailed(status, error, Handle.Id);
        result.ModelId = Handle.Id;
        result.RawRepresentation = raw;
        return result;
    }
}

internal sealed class WindowsTextSummarizer : WindowsTextSkillClient, ITextSummarizer
{
    private readonly NativeTextSummarizer _native;

    private WindowsTextSummarizer(LanguageModel model, ILocalModel handle, NativeTextSummarizer native)
        : base(model, handle, native)
    {
        _native = native;
        Metadata = new TextSummarizerMetadata(WindowsModels.ProviderName, handle.Id);
    }

    public TextSummarizerMetadata Metadata { get; }

    protected override object MetadataObject => Metadata;

    public static WindowsTextSummarizer Create(LanguageModel model, ILocalModel handle) => new(model, handle, new NativeTextSummarizer(model));

    public async Task<TextSummarizationResult> SummarizeAsync(string text, TextSummarizationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var operation = options?.Format == TextSummaryFormat.Paragraph ? _native.SummarizeParagraphAsync(text) : _native.SummarizeAsync(text);
        var result = await operation.AsTask(cancellationToken).ConfigureAwait(false);
        return Complete(new TextSummarizationResult(result.Text ?? string.Empty), result.Status, result.ExtendedError, result);
    }
}

internal sealed class WindowsTextRewriter : WindowsTextSkillClient, ITextRewriter
{
    private readonly NativeTextRewriter _native;

    private WindowsTextRewriter(LanguageModel model, ILocalModel handle, NativeTextRewriter native)
        : base(model, handle, native)
    {
        _native = native;
        Metadata = new TextRewriterMetadata(WindowsModels.ProviderName, handle.Id);
    }

    public TextRewriterMetadata Metadata { get; }

    protected override object MetadataObject => Metadata;

    public static WindowsTextRewriter Create(LanguageModel model, ILocalModel handle) => new(model, handle, new NativeTextRewriter(model));

    public async Task<TextRewriteResult> RewriteAsync(string text, TextRewriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var operation = !string.IsNullOrWhiteSpace(options?.CustomTone)
            ? _native.RewriteCustomAsync(text, options!.CustomTone)
            : _native.RewriteAsync(text, (options?.Tone ?? TextRewriteTone.Default) switch
            {
                TextRewriteTone.General => NativeTextRewriteTone.General,
                TextRewriteTone.Casual => NativeTextRewriteTone.Casual,
                TextRewriteTone.Concise => NativeTextRewriteTone.Concise,
                TextRewriteTone.Formal => NativeTextRewriteTone.Formal,
                _ => NativeTextRewriteTone.Default,
            });
        var result = await operation.AsTask(cancellationToken).ConfigureAwait(false);
        return Complete(new TextRewriteResult(result.Text ?? string.Empty), result.Status, result.ExtendedError, result);
    }
}

internal sealed class WindowsTextToTableConverter : WindowsTextSkillClient, ITextToTableConverter
{
    private readonly NativeTextToTableConverter _native;

    private WindowsTextToTableConverter(LanguageModel model, ILocalModel handle, NativeTextToTableConverter native)
        : base(model, handle, native)
    {
        _native = native;
        Metadata = new TextToTableConverterMetadata(WindowsModels.ProviderName, handle.Id);
    }

    public TextToTableConverterMetadata Metadata { get; }

    protected override object MetadataObject => Metadata;

    public static WindowsTextToTableConverter Create(LanguageModel model, ILocalModel handle) => new(model, handle, new NativeTextToTableConverter(model));

    public async Task<TextTableResult> ConvertAsync(string text, TextToTableOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = await _native.ConvertAsync(text).AsTask(cancellationToken).ConfigureAwait(false);
        WindowsInterop.ThrowIfFailed(result.Status, result.ExtendedError, Handle.Id);
        IReadOnlyList<IReadOnlyList<string>> rows = [.. result.GetRows().Select(r => (IReadOnlyList<string>)r.GetColumns())];
        return Complete(new TextTableResult(rows), result.Status, result.ExtendedError, result);
    }
}
