namespace Microsoft.AI.Local.Foundry;

/// <summary>
/// Strongly typed handles for the Foundry Local catalog models. The property type carries the task type, so
/// <c>ITextGenerationModel model = FoundryModels.Phi4Mini;</c> compiles and assigning a speech model to it doesn't.
/// </summary>
/// <remarks>
/// <para>
/// The properties are generated from <c>eng/catalog/foundry-models.json</c>. Getting a handle does no I/O; Foundry
/// Local is initialized, and the model downloaded and loaded, only by
/// <see cref="ILocalModel.GetAvailabilityAsync"/>, <see cref="ILocalModel.EnsureReadyAsync"/> and
/// <c>CreateClientAsync</c>.
/// </para>
/// <para>
/// When a model leaves the Foundry Local catalog, its property is marked <see cref="ObsoleteAttribute"/> and the handle
/// reports <see cref="ModelAvailabilityStatus.Retired"/>.
/// </para>
/// </remarks>
public static partial class FoundryModels
{
}
