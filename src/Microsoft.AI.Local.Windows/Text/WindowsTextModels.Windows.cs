using Microsoft.Extensions.AI;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using Windows.Foundation;

namespace Microsoft.AI.Local.Windows;

/// <summary>Base class of the models backed by Phi Silica (<see cref="LanguageModel"/>).</summary>
internal abstract class LanguageModelBackedModel<TClient>(string id, string displayName, LocalModelCapabilities capabilities)
    : WindowsModelBase<TClient>(id, displayName, capabilities, usesLanguageModel: true)
    where TClient : class
{
    protected sealed override AIFeatureReadyState GetNativeReadyState() => LanguageModel.GetReadyState();

    protected sealed override IAsyncOperationWithProgress<AIFeatureReadyResult, double> EnsureNativeReadyAsync() => LanguageModel.EnsureReadyAsync();

    protected sealed override async Task<TClient> CreateNativeClientAsync(CancellationToken cancellationToken)
    {
        var model = await LanguageModel.CreateAsync().AsTask(cancellationToken).ConfigureAwait(false);
        try
        {
            return Wrap(model);
        }
        catch
        {
            model.Dispose();
            throw;
        }
    }

    protected abstract TClient Wrap(LanguageModel model);
}

internal sealed class PhiSilicaModel()
    : LanguageModelBackedModel<IChatClient>(WindowsModelIds.PhiSilica, "Phi Silica", WindowsModelCapabilities.PhiSilica), ITextGenerationModel
{
    protected override IChatClient Wrap(LanguageModel model) => new PhiSilicaChatClient(model, this);
}

internal sealed class WindowsTextSummarizationModel()
    : LanguageModelBackedModel<ITextSummarizer>(WindowsModelIds.TextSummarization, "Text summarization (Phi Silica)", WindowsModelCapabilities.TextSkill), ITextSummarizationModel
{
    protected override ITextSummarizer Wrap(LanguageModel model) => WindowsTextSummarizer.Create(model, this);
}

internal sealed class WindowsTextRewriteModel()
    : LanguageModelBackedModel<ITextRewriter>(WindowsModelIds.TextRewrite, "Text rewrite (Phi Silica)", WindowsModelCapabilities.TextSkill), ITextRewriteModel
{
    protected override ITextRewriter Wrap(LanguageModel model) => WindowsTextRewriter.Create(model, this);
}

internal sealed class WindowsTextToTableModel()
    : LanguageModelBackedModel<ITextToTableConverter>(WindowsModelIds.TextToTable, "Text to table (Phi Silica)", WindowsModelCapabilities.TextSkill), ITextToTableModel
{
    protected override ITextToTableConverter Wrap(LanguageModel model) => WindowsTextToTableConverter.Create(model, this);
}
