namespace Microsoft.AI.Local;

/// <summary>
/// Helpers for working with several candidate models.
/// </summary>
public static class LocalModel
{
    /// <summary>
    /// Returns the first model that is ready or can be made ready on this device, in order of preference.
    /// For example, prefer an inbox model on Copilot+ PCs and fall back to a Foundry model everywhere else.
    /// </summary>
    /// <typeparam name="TModel">The model interface, for example <c>ITextGenerationModel</c>.</typeparam>
    /// <param name="models">The candidate models, in order of preference.</param>
    /// <returns>The first available model.</returns>
    /// <exception cref="LocalModelNotSupportedException">None of the models is available. The message lists each model's status.</exception>
    public static Task<TModel> SelectFirstAvailableAsync<TModel>(params TModel[] models)
        where TModel : ILocalModel =>
        SelectFirstAvailableAsync((IEnumerable<TModel>)models, CancellationToken.None);

    /// <summary>
    /// Returns the first model that is ready or can be made ready on this device, in order of preference.
    /// </summary>
    /// <typeparam name="TModel">The model interface, for example <c>ITextGenerationModel</c>.</typeparam>
    /// <param name="models">The candidate models, in order of preference.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The first available model.</returns>
    /// <exception cref="LocalModelNotSupportedException">None of the models is available. The message lists each model's status.</exception>
    public static async Task<TModel> SelectFirstAvailableAsync<TModel>(IEnumerable<TModel> models, CancellationToken cancellationToken = default)
        where TModel : ILocalModel
    {
        ArgumentNullException.ThrowIfNull(models);
        var reasons = new List<string>();
        foreach (var model in models)
        {
            ArgumentNullException.ThrowIfNull(model, nameof(models));
            ModelAvailability availability;
            try
            {
                availability = await model.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A provider that can't even report availability (e.g. a broken native install) is skipped.
                reasons.Add($"{model.Id}: {ex.Message}");
                continue;
            }

            if (availability.IsAvailable)
            {
                return model;
            }

            reasons.Add($"{model.Id}: {availability}");
        }

        var reason = reasons.Count == 0 ? "No candidate models were provided." : "No candidate model is available. " + string.Join("; ", reasons);
        throw new LocalModelNotSupportedException(new ModelAvailability(ModelAvailabilityStatus.NotSupportedOnDevice, reason));
    }
}
