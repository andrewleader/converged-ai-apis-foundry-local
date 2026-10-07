namespace Microsoft.AI.Local.Windows;

/// <summary>Creates the real Windows inbox model handles (Windows target frameworks).</summary>
internal static class WindowsModelFactory
{
    public static ITextGenerationModel CreatePhiSilica() => new PhiSilicaModel();

    public static ITextSummarizationModel CreateTextSummarization() => new WindowsTextSummarizationModel();

    public static ITextRewriteModel CreateTextRewrite() => new WindowsTextRewriteModel();

    public static ITextToTableModel CreateTextToTable() => new WindowsTextToTableModel();

    public static ITextRecognitionModel CreateTextRecognition() => new WindowsTextRecognitionModel();

    public static IImageDescriptionModel CreateImageDescription() => new WindowsImageDescriptionModel();

    public static IImageScalingModel CreateImageScaling() => new WindowsImageScalingModel();

    public static IImageSegmentationModel CreateForegroundExtraction() =>
        new WindowsImageSegmentationModel(WindowsModelIds.ForegroundExtraction, "Foreground extraction", requireHints: false);

    public static IImageSegmentationModel CreateObjectExtraction() =>
        new WindowsImageSegmentationModel(WindowsModelIds.ObjectExtraction, "Object extraction", requireHints: true);

    public static IObjectRemovalModel CreateObjectRemoval() => new WindowsObjectRemovalModel();
}
