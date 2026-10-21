using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AI.Local.Windows.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Windows.AI.Text;

namespace Microsoft.AI.Local.Windows;

/// <summary>An <see cref="IChatClient"/> over Phi Silica (<see cref="LanguageModel"/>).</summary>
internal sealed class PhiSilicaChatClient : IChatClient
{
    private readonly LanguageModel _model;
    private readonly ILocalModel _handle;
    private readonly ChatClientMetadata _metadata;

    public PhiSilicaChatClient(LanguageModel model, ILocalModel handle)
    {
        _model = model;
        _handle = handle;
        _metadata = new ChatClientMetadata(WindowsAIProvider.ProviderName, null, handle.Id);
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var prompt = Prepare(messages, options, out var nativeOptions);
        string text;
        object raw;
        LanguageModelResponseStatus status;

        if (PhiSilicaPromptBuilder.GetJsonSchema(options) is { } schema)
        {
            var result = await _model.GenerateStructuredJsonResponseAsync(WithSystemPrompt(prompt), schema, nativeOptions).AsTask(cancellationToken).ConfigureAwait(false);
            (text, raw, status) = (result.Text, result, result.Status.ToResponseStatus());
            WindowsAIInterop.ThrowIfFailed(status, result.ExtendedError, _handle.Id, responseBlockedIsFinishReason: true);
        }
        else
        {
            using var context = CreateContext(prompt, options);
            var operation = context is null
                ? _model.GenerateResponseAsync(prompt.Prompt, nativeOptions)
                : _model.GenerateResponseAsync(context, prompt.Prompt, nativeOptions);
            var result = await operation.AsTask(cancellationToken).ConfigureAwait(false);
            (text, raw, status) = (result.Text, result, result.Status);
            WindowsAIInterop.ThrowIfFailed(status, result.ExtendedError, _handle.Id, responseBlockedIsFinishReason: true);
        }

        var blocked = status == LanguageModelResponseStatus.ResponseBlockedByContentModeration;
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, blocked ? string.Empty : text ?? string.Empty))
        {
            ResponseId = Guid.NewGuid().ToString("N"),
            ModelId = _handle.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            FinishReason = blocked ? ChatFinishReason.ContentFilter : ChatFinishReason.Stop,
            RawRepresentation = raw,
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (PhiSilicaPromptBuilder.GetJsonSchema(options) is not null)
        {
            // Structured output is produced in one piece.
            var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            foreach (var update in response.ToChatResponseUpdates())
            {
                yield return update;
            }

            yield break;
        }

        var prompt = Prepare(messages, options, out var nativeOptions);
        var responseId = Guid.NewGuid().ToString("N");
        var created = DateTimeOffset.UtcNow;
        using var context = CreateContext(prompt, options);
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        var operation = context is null
            ? _model.GenerateResponseAsync(prompt.Prompt, nativeOptions)
            : _model.GenerateResponseAsync(context, prompt.Prompt, nativeOptions);
        var task = operation.AsTask(cancellationToken, new ChannelProgress(channel.Writer));
        _ = task.ContinueWith(
            static (_, state) => ((ChannelWriter<string>)state!).TryComplete(),
            channel.Writer,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        await foreach (var delta in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (delta.Length > 0)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, delta)
                {
                    ResponseId = responseId,
                    MessageId = responseId,
                    ModelId = _handle.Id,
                    CreatedAt = created,
                };
            }
        }

        var result = await task.ConfigureAwait(false);
        WindowsAIInterop.ThrowIfFailed(result.Status, result.ExtendedError, _handle.Id, responseBlockedIsFinishReason: true);
        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            ResponseId = responseId,
            MessageId = responseId,
            ModelId = _handle.Id,
            CreatedAt = created,
            FinishReason = result.Status == LanguageModelResponseStatus.ResponseBlockedByContentModeration ? ChatFinishReason.ContentFilter : ChatFinishReason.Stop,
            RawRepresentation = result,
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType.IsInstanceOfType(this) ? this
            : serviceType.IsInstanceOfType(_model) ? _model
            : serviceType.IsInstanceOfType(_metadata) ? _metadata
            : serviceType.IsInstanceOfType(_handle) ? _handle
            : null;
    }

    public void Dispose() => _model.Dispose();

    private static PhiSilicaPrompt Prepare(IEnumerable<ChatMessage> messages, ChatOptions? options, out LanguageModelOptions nativeOptions)
    {
        PhiSilicaPromptBuilder.ReportUnsupportedOptions(options);
        var prompt = PhiSilicaPromptBuilder.Build(messages);
        if (PhiSilicaPromptBuilder.WantsUnstructuredJson(options))
        {
            prompt = prompt with { Prompt = prompt.Prompt + "\n\nRespond with valid JSON only." };
        }

        nativeOptions = new LanguageModelOptions();
        if (options?.Temperature is { } temperature)
        {
            nativeOptions.Temperature = temperature;
        }

        if (options?.TopP is { } topP)
        {
            nativeOptions.TopP = topP;
        }

        if (options?.TopK is { } topK and > 0)
        {
            nativeOptions.TopK = (uint)topK;
        }

        if (options?.AdditionalProperties.GetWindowsContentFilter().ToNative() is { } filter)
        {
            nativeOptions.ContentFilterOptions = filter;
        }

        return prompt;
    }

    private static string WithSystemPrompt(PhiSilicaPrompt prompt) =>
        prompt.SystemPrompt is null ? prompt.Prompt : $"{prompt.SystemPrompt}\n\n{prompt.Prompt}";

    private LanguageModelContext? CreateContext(PhiSilicaPrompt prompt, ChatOptions? options)
    {
        if (prompt.SystemPrompt is null)
        {
            return null;
        }

        return options?.AdditionalProperties.GetWindowsContentFilter().ToNative() is { } filter
            ? _model.CreateContext(prompt.SystemPrompt, filter)
            : _model.CreateContext(prompt.SystemPrompt);
    }

    private sealed class ChannelProgress(ChannelWriter<string> writer) : IProgress<string>
    {
        public void Report(string value) => writer.TryWrite(value);
    }
}
