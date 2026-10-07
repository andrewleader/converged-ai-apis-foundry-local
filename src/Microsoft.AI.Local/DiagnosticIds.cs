namespace Microsoft.AI.Local;

/// <summary>Diagnostic identifiers used by this library and its analyzers.</summary>
internal static class DiagnosticIds
{
    /// <summary>Identifiers of experimental APIs.</summary>
    internal static class Experiments
    {
        /// <summary>Speech-to-text builds on the experimental <c>ISpeechToTextClient</c> from Microsoft.Extensions.AI.</summary>
        public const string SpeechToText = "MSAILOCAL001";
    }
}
