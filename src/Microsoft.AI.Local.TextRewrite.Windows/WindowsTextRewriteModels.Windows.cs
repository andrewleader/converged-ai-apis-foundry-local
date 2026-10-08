using Microsoft.AI.Local.Providers;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Windows.AI.Text;
using NativeTextRewriter = Microsoft.Windows.AI.Text.TextRewriter;
using NativeTextRewriteTone = Microsoft.Windows.AI.Text.TextRewriteTone;

namespace Microsoft.AI.Local.Windows;

internal static partial class WindowsModelFactory
{
    public static ILocalModel Create(LocalModelDescriptor descriptor) => new WindowsTextRewriteModel(descriptor);
}

internal sealed class WindowsTextRewriteModel(LocalModelDescriptor descriptor)
    : WindowsLanguageModelBase<ITextRewriter>(descriptor), ITextRewriteModel
{
    protected override ITextRewriter Wrap(LanguageModel model) => new WindowsTextRewriter(model, this, new NativeTextRewriter(model));
}

internal sealed class WindowsTextRewriter : WindowsTextSkillClientBase, ITextRewriter
{
    private readonly NativeTextRewriter _native;

    public WindowsTextRewriter(LanguageModel model, ILocalModel handle, NativeTextRewriter native)
        : base(model, handle, native)
    {
        _native = native;
        Metadata = new TextRewriterMetadata(WindowsAIProvider.ProviderName, handle.Id);
    }

    public TextRewriterMetadata Metadata { get; }

    protected override object MetadataObject => Metadata;

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
