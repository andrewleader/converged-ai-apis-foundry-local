using System.Reflection;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace Microsoft.AI.Local.Windows;

/// <summary>Checks the app-level requirements of the Windows AI APIs and turns failures into actionable availability.</summary>
internal static class WindowsAppRequirements
{
    private const int ClassNotRegistered = unchecked((int)0x80040154);
    private const int AccessDenied = unchecked((int)0x80070005);

    private static readonly Lazy<bool> PackageIdentity = new(DetectPackageIdentity);
    private static readonly object UnlockGate = new();
    private static (WindowsAILimitedAccessFeature Feature, ModelAvailability? Result)? unlockCache;

    public static ModelAvailability MissingIdentity { get; } = new(
        ModelAvailabilityStatus.MissingAppRequirement,
        "Windows AI APIs require an app with package identity (MSIX, or a sparse package for unpackaged apps) and the " +
        "'systemAIModels' capability. Add <rescap:Capability Name=\"systemAIModels\"/> to Package.appxmanifest.");

    public static ModelAvailability MissingCapability { get; } = new(
        ModelAvailabilityStatus.MissingAppRequirement,
        "The app is missing the 'systemAIModels' capability. Add <rescap:Capability Name=\"systemAIModels\"/> to Package.appxmanifest.");

    public static bool HasPackageIdentity => PackageIdentity.Value;

    /// <summary>Unlocks the configured Limited Access Feature, if any. Returns a non-ready availability on failure.</summary>
    public static ModelAvailability? EnsureLimitedAccessFeatureUnlocked()
    {
        var feature = WindowsAIProvider.Options.LimitedAccessFeature ?? FromEntryAssembly();
        if (feature is null)
        {
            return null;
        }

        lock (UnlockGate)
        {
            if (unlockCache is { } cached && ReferenceEquals(cached.Feature, feature))
            {
                return cached.Result;
            }
        }

        ModelAvailability? result;
        try
        {
            var unlock = LimitedAccessFeatures.TryUnlockFeature(feature.FeatureId, feature.Token, feature.Attestation);
            result = unlock.Status is LimitedAccessFeatureStatus.Available or LimitedAccessFeatureStatus.AvailableWithoutToken
                ? null
                : new ModelAvailability(
                    ModelAvailabilityStatus.MissingAppRequirement,
                    $"The Limited Access Feature '{feature.FeatureId}' could not be unlocked (status: {unlock.Status}). " +
                    "Check the WindowsAILimitedAccessFeatureToken/Attestation properties and that the app's package family name matches the registration.");
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or ArgumentException)
        {
            result = new ModelAvailability(
                ModelAvailabilityStatus.MissingAppRequirement,
                $"The Limited Access Feature '{feature.FeatureId}' could not be unlocked: {ex.Message}");
        }

        lock (UnlockGate)
        {
            unlockCache = (feature, result);
        }

        return result;
    }

    /// <summary>Maps an exception thrown by a Windows AI API into an availability, when it means "can't run here".</summary>
    public static ModelAvailability? TryMapException(Exception exception)
    {
        switch (exception)
        {
            case COMException { HResult: ClassNotRegistered }:
            case TypeLoadException: // includes DllNotFoundException
            case FileNotFoundException:
            case TypeInitializationException:
                return new ModelAvailability(
                    ModelAvailabilityStatus.MissingAppRequirement,
                    "The Windows App SDK runtime could not be found. Unpackaged apps must initialize the Windows App SDK " +
                    $"(self-contained deployment or the bootstrapper). {exception.Message}");
            case UnauthorizedAccessException:
            case COMException { HResult: AccessDenied }:
                return new ModelAvailability(
                    ModelAvailabilityStatus.MissingAppRequirement,
                    "Access to the Windows AI API was denied. Check the 'systemAIModels' capability and, for Phi Silica, the " +
                    $"Limited Access Feature registration (WindowsAILimitedAccessFeatureId/Token MSBuild properties). {exception.Message}");
            case PlatformNotSupportedException:
            case NotImplementedException:
                return new ModelAvailability(
                    ModelAvailabilityStatus.NotSupportedOnPlatform,
                    $"This version of Windows doesn't support the Windows AI APIs. {exception.Message}");
            default:
                return null;
        }
    }

    private static WindowsAILimitedAccessFeature? FromEntryAssembly()
    {
        var attribute = Assembly.GetEntryAssembly()?.GetCustomAttribute<WindowsAILimitedAccessFeatureAttribute>();
        return attribute is null || string.IsNullOrEmpty(attribute.FeatureId) || string.IsNullOrEmpty(attribute.Token)
            ? null
            : new WindowsAILimitedAccessFeature(attribute.FeatureId, attribute.Token, attribute.Attestation ?? string.Empty);
    }

    private static bool DetectPackageIdentity()
    {
        try
        {
            return Package.Current?.Id is not null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            return false;
        }
    }
}
