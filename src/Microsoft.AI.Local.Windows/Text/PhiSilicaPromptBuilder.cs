using System.Text;
using Microsoft.AI.Local.Providers;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local.Windows;

/// <summary>
/// Turns Microsoft.Extensions.AI chat messages into Phi Silica's prompt + system-prompt shape, and applies the
/// unsupported-option policy to <see cref="ChatOptions"/>. Platform-neutral so it can be unit tested anywhere.
/// </summary>
internal static class PhiSilicaPromptBuilder
{
    public static PhiSilicaPrompt Build(IEnumerable<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        StringBuilder? system = null;
        var turns = new List<(ChatRole Role, string Text)>();
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is DataContent or UriContent)
                {
                    throw new NotSupportedException(
                        "Phi Silica accepts text input only. Check LocalModelCapabilities.SupportsImageInput/SupportsAudioInput before sending media.");
                }
            }

            var text = message.Text;
            if (message.Role == ChatRole.System || message.Role.Value == "developer")
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    system ??= new StringBuilder();
                    if (system.Length > 0)
                    {
                        system.AppendLine();
                    }

                    system.Append(text);
                }

                continue;
            }

            if (message.Role == ChatRole.Tool || string.IsNullOrEmpty(text))
            {
                // Tool results (and tool-call-only messages) can't be represented; tools are reported unsupported.
                continue;
            }

            turns.Add((message.Role, text));
        }

        if (turns.Count == 0)
        {
            throw new ArgumentException("The chat history must contain at least one user message with text.", nameof(messages));
        }

        string prompt;
        if (turns.Count == 1)
        {
            prompt = turns[0].Text;
        }
        else
        {
            // Phi Silica takes a single prompt per call, so earlier turns are rendered as a transcript.
            var builder = new StringBuilder("The conversation so far:\n");
            foreach (var (role, text) in turns.Take(turns.Count - 1))
            {
                builder.Append(role == ChatRole.Assistant ? "Assistant: " : "User: ").AppendLine(text);
            }

            var last = turns[^1];
            builder.AppendLine()
                .Append(last.Role == ChatRole.Assistant ? "Continue the assistant's last message: " : "Respond to the user's latest message: ")
                .Append(last.Text);
            prompt = builder.ToString();
        }

        return new PhiSilicaPrompt(system?.ToString(), prompt);
    }

    /// <summary>Reports the <see cref="ChatOptions"/> Phi Silica can't honor.</summary>
    public static void ReportUnsupportedOptions(ChatOptions? options)
    {
        if (options is null)
        {
            return;
        }

        const string Provider = WindowsModels.ProviderName;
        if (options.MaxOutputTokens is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(Provider, "ChatOptions.MaxOutputTokens");
        }

        if (options.StopSequences is { Count: > 0 })
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(Provider, "ChatOptions.StopSequences");
        }

        if (options.FrequencyPenalty is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(Provider, "ChatOptions.FrequencyPenalty");
        }

        if (options.PresencePenalty is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(Provider, "ChatOptions.PresencePenalty");
        }

        if (options.Seed is not null)
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(Provider, "ChatOptions.Seed");
        }

        if (options.Tools is { Count: > 0 })
        {
            LocalAIProviderHelpers.ReportUnsupportedOption(Provider, "ChatOptions.Tools");
        }
    }

    /// <summary>Gets the JSON schema to pass to structured output, or <see langword="null"/>.</summary>
    public static string? GetJsonSchema(ChatOptions? options) =>
        options?.ResponseFormat is ChatResponseFormatJson { Schema: { } schema } ? schema.GetRawText() : null;

    /// <summary>Gets a value indicating whether JSON output without a schema was requested.</summary>
    public static bool WantsUnstructuredJson(ChatOptions? options) =>
        options?.ResponseFormat is ChatResponseFormatJson { Schema: null };
}

/// <summary>A Phi Silica request.</summary>
/// <param name="SystemPrompt">The system prompt, passed as a <c>LanguageModelContext</c>.</param>
/// <param name="Prompt">The prompt.</param>
internal sealed record PhiSilicaPrompt(string? SystemPrompt, string Prompt);
