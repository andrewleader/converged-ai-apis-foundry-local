using Microsoft.AI.Local.Adapters;
using Microsoft.Extensions.AI;

namespace Microsoft.AI.Local;

/// <summary>A local model that describes images.</summary>
public interface IImageDescriptionModel : ILocalModel<IImageDescriber>;

/// <summary>
/// Image-description models from every provider, for example <c>ImageDescriptionModels.WindowsDefault</c>. Any
/// vision-capable chat model can also describe images through
/// <see cref="ImageDescriptionModelExtensions.AsImageDescriptionModel"/>.
/// </summary>
/// <remarks>
/// Getting a handle does no I/O. Each handle's documentation names the provider package it needs. Without it the
/// handle reports <see cref="ModelAvailabilityStatus.MissingAppRequirement"/>.
/// </remarks>
public static partial class ImageDescriptionModels
{
}

/// <summary>Adapts vision-capable chat models to image description.</summary>
public static class ImageDescriptionModelExtensions
{
    /// <summary>
    /// Returns an image-description model that prompts <paramref name="model"/> with the image.
    /// The model must accept image input (<see cref="LocalModelCapabilities.SupportsImageInput"/>).
    /// </summary>
    /// <param name="model">A vision-capable chat model.</param>
    /// <returns>An image-description model that shares <paramref name="model"/>'s acquisition and lifetime.</returns>
    /// <exception cref="ArgumentException"><paramref name="model"/> doesn't accept image input.</exception>
    public static IImageDescriptionModel AsImageDescriptionModel(this ILocalModel<IChatClient> model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!model.Capabilities.SupportsImageInput)
        {
            throw new ArgumentException($"The model '{model.Id}' does not accept image input.", nameof(model));
        }

        return new ChatBackedImageDescriptionModel(model);
    }

    /// <summary>Returns an <see cref="IImageDescriber"/> that prompts <paramref name="chatClient"/>, which must accept image input.</summary>
    /// <param name="chatClient">The chat client. It is not disposed with the describer.</param>
    /// <returns>An image describer.</returns>
    public static IImageDescriber AsImageDescriber(this IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        var metadata = chatClient.GetService<ChatClientMetadata>();
        return new ChatClientImageDescriber(chatClient, metadata?.ProviderName, metadata?.DefaultModelId, ownsChatClient: false);
    }
}
