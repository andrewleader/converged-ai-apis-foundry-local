namespace Microsoft.AI.Local.Windows;

/// <summary>
/// Strongly typed handles to the AI models built into Windows (Windows AI APIs, on Copilot+ PCs).
/// </summary>
/// <remarks>
/// <para>
/// Getting a handle performs no I/O. Use the <see cref="ILocalModel"/> flow on it: check availability,
/// call <see cref="ILocalModel.EnsureReadyAsync"/>, then create a client.
/// </para>
/// <para>
/// Inbox models need an app with package identity and the <c>systemAIModels</c> capability, and an app built for a
/// Windows target framework (<c>net8.0-windows10.0.19041.0</c> or later). In apps built for other target frameworks
/// every handle reports <see cref="ModelAvailabilityStatus.NotSupportedOnPlatform"/>, which lets cross-platform code
/// fall back to another model with <see cref="LocalModel.SelectFirstAvailableAsync{TModel}(TModel[])"/>.
/// </para>
/// </remarks>
public static partial class WindowsModels
{
    /// <summary>The provider name reported by Windows inbox models.</summary>
    public const string ProviderName = "Windows";

    /// <summary>Gets Phi Silica, the inbox small language model, as a chat model.</summary>
    public static ITextGenerationModel PhiSilica { get; } = WindowsModelFactory.CreatePhiSilica();

    /// <summary>Gets the inbox text summarizer (built on Phi Silica).</summary>
    public static ITextSummarizationModel TextSummarization { get; } = WindowsModelFactory.CreateTextSummarization();

    /// <summary>Gets the inbox text rewriter (built on Phi Silica).</summary>
    public static ITextRewriteModel TextRewrite { get; } = WindowsModelFactory.CreateTextRewrite();

    /// <summary>Gets the inbox text-to-table converter (built on Phi Silica).</summary>
    public static ITextToTableModel TextToTable { get; } = WindowsModelFactory.CreateTextToTable();

    /// <summary>Gets the inbox text recognizer (OCR).</summary>
    public static ITextRecognitionModel TextRecognition { get; } = WindowsModelFactory.CreateTextRecognition();

    /// <summary>Gets the inbox image description generator.</summary>
    public static IImageDescriptionModel ImageDescription { get; } = WindowsModelFactory.CreateImageDescription();

    /// <summary>Gets the inbox image super-resolution model.</summary>
    public static IImageScalingModel ImageScaling { get; } = WindowsModelFactory.CreateImageScaling();

    /// <summary>
    /// Gets a segmenter that extracts the main subject of an image. Hints in
    /// <see cref="ImageSegmentationOptions"/> are optional; without hints the whole image is considered.
    /// </summary>
    public static IImageSegmentationModel ForegroundExtraction { get; } = WindowsModelFactory.CreateForegroundExtraction();

    /// <summary>
    /// Gets a segmenter that extracts the object selected by the hints (rectangles and points) in
    /// <see cref="ImageSegmentationOptions"/>. At least one include hint is required.
    /// </summary>
    public static IImageSegmentationModel ObjectExtraction { get; } = WindowsModelFactory.CreateObjectExtraction();

    /// <summary>Gets the inbox object remover (generative erase).</summary>
    public static IObjectRemovalModel ObjectRemoval { get; } = WindowsModelFactory.CreateObjectRemoval();

    /// <summary>Gets every inbox model handle.</summary>
    public static IReadOnlyList<ILocalModel> All { get; } =
    [
        PhiSilica,
        TextSummarization,
        TextRewrite,
        TextToTable,
        TextRecognition,
        ImageDescription,
        ImageScaling,
        ForegroundExtraction,
        ObjectExtraction,
        ObjectRemoval,
    ];
}

/// <summary>The stable identifiers of the Windows inbox models.</summary>
internal static class WindowsModelIds
{
    public const string PhiSilica = "windows/phi-silica";
    public const string TextSummarization = "windows/text-summarization";
    public const string TextRewrite = "windows/text-rewrite";
    public const string TextToTable = "windows/text-to-table";
    public const string TextRecognition = "windows/text-recognition";
    public const string ImageDescription = "windows/image-description";
    public const string ImageScaling = "windows/image-scaling";
    public const string ForegroundExtraction = "windows/foreground-extraction";
    public const string ObjectExtraction = "windows/object-extraction";
    public const string ObjectRemoval = "windows/object-removal";
}

/// <summary>Capabilities of the Windows inbox models.</summary>
internal static class WindowsModelCapabilities
{
    public static LocalModelCapabilities PhiSilica { get; } = new()
    {
        SupportsStreaming = true,
        SupportsStructuredOutput = true,
    };

    public static LocalModelCapabilities TextSkill { get; } = new() { SupportsStreaming = false };

    public static LocalModelCapabilities Imaging { get; } = new() { SupportsImageInput = true };
}
