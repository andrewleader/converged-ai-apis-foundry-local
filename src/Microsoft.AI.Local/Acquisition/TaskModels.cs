using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>A local model that generates text and chat responses. Its client is a standard <see cref="IChatClient"/>.</summary>
public interface ITextGenerationModel : ILocalModel<IChatClient>;

/// <summary>A local model that produces text embeddings. Its client is a standard <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.</summary>
public interface ITextEmbeddingModel : ILocalModel<IEmbeddingGenerator<string, Embedding<float>>>;

/// <summary>A local model that transcribes speech. Its client is a standard <see cref="ISpeechToTextClient"/>.</summary>
[Experimental(DiagnosticIds.Experiments.SpeechToText)]
public interface ISpeechToTextModel : ILocalModel<ISpeechToTextClient>;

/// <summary>A local model that summarizes text.</summary>
public interface ITextSummarizationModel : ILocalModel<ITextSummarizer>;

/// <summary>A local model that rewrites text.</summary>
public interface ITextRewriteModel : ILocalModel<ITextRewriter>;

/// <summary>A local model that converts text to a table.</summary>
public interface ITextToTableModel : ILocalModel<ITextToTableConverter>;

/// <summary>A local model that recognizes text in images (OCR).</summary>
public interface ITextRecognitionModel : ILocalModel<ITextRecognizer>;

/// <summary>A local model that describes images.</summary>
public interface IImageDescriptionModel : ILocalModel<IImageDescriber>;

/// <summary>A local model that scales images (super-resolution).</summary>
public interface IImageScalingModel : ILocalModel<IImageScaler>;

/// <summary>A local model that separates an object from the rest of an image.</summary>
public interface IImageSegmentationModel : ILocalModel<IImageSegmenter>;

/// <summary>A local model that removes objects from images.</summary>
public interface IImageObjectRemovalModel : ILocalModel<IImageObjectRemover>;
