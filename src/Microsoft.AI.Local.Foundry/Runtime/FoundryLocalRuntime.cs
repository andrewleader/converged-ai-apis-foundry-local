using System.Reflection;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.AI.Local.Foundry.Runtime;

/// <summary>The real runtime over the Foundry Local SDK (<c>FoundryLocalManager</c>).</summary>
internal sealed partial class FoundryLocalRuntime(Func<FoundryProviderOptions> options) : IFoundryRuntime
{
    private readonly object _gate = new();
    private Task<ICatalog>? _initialization;
    private Task? _autoExecutionProviders;

    private ILogger Logger => field ??= (options().LoggerFactory ?? LocalAIOptions.Default.LoggerFactory).CreateLogger("Microsoft.AI.Local.Foundry");

    public Task InitializeAsync(CancellationToken cancellationToken) => GetCatalogAsync(cancellationToken);

    public async Task<IFoundryCatalogModel?> GetModelAsync(string alias, CancellationToken cancellationToken)
    {
        var catalog = await GetCatalogAsync(cancellationToken).ConfigureAwait(false);
        var model = await catalog.GetModelAsync(alias, cancellationToken).ConfigureAwait(false);
        return model is null ? null : new FoundryCatalogModel(model);
    }

    public async Task EnsureExecutionProvidersAsync(FoundryExecutionProviders policy, Action<double> progress, CancellationToken cancellationToken)
    {
        if (policy.Mode == FoundryExecutionProviderMode.None)
        {
            return;
        }

        await GetCatalogAsync(cancellationToken).ConfigureAwait(false);
        var manager = FoundryLocalManager.Instance;

        if (policy.Mode == FoundryExecutionProviderMode.Explicit)
        {
            await RegisterAsync(manager, policy.Names, progress, throwOnFailure: true, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Auto: once per process. Other callers wait for the same registration.
        Task auto;
        lock (_gate)
        {
            auto = _autoExecutionProviders ??= Task.Run(
                async () =>
                {
                    var missing = manager.DiscoverEps().Where(ep => !ep.IsRegistered).Select(ep => ep.Name).ToArray();
                    if (missing.Length > 0)
                    {
                        await RegisterAsync(manager, missing, progress, throwOnFailure: false, CancellationToken.None).ConfigureAwait(false);
                    }
                },
                CancellationToken.None);
        }

        await auto.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RegisterAsync(FoundryLocalManager manager, IReadOnlyList<string> names, Action<double> progress, bool throwOnFailure, CancellationToken cancellationToken)
    {
        Log.RegisteringExecutionProviders(Logger, string.Join(", ", names));
        var result = await manager.DownloadAndRegisterEpsAsync(
            names,
            (name, percent) =>
            {
                var index = Math.Max(0, IndexOf(names, name));
                progress((index + Math.Clamp(percent / 100, 0, 1)) / names.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            var failed = string.Join(", ", result.FailedEps);
            if (throwOnFailure)
            {
                throw new LocalModelNotReadyException($"Foundry Local could not register the execution providers {failed}: {result.Status}");
            }

            Log.ExecutionProvidersFailed(Logger, failed, result.Status);
        }
    }

    private static int IndexOf(IReadOnlyList<string> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private Task<ICatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        Task<ICatalog> initialization;
        lock (_gate)
        {
            // Retry after a failed initialization (e.g. a transient network error while fetching the catalog).
            if (_initialization is null || _initialization.IsFaulted || _initialization.IsCanceled)
            {
                _initialization = Task.Run(InitializeCoreAsync, CancellationToken.None);
            }

            initialization = _initialization;
        }

        return initialization.WaitAsync(cancellationToken);
    }

    private async Task<ICatalog> InitializeCoreAsync()
    {
        if (!FoundryLocalManager.IsInitialized)
        {
            var o = options();
            var configuration = new Configuration
            {
                AppName = o.AppName ?? DefaultAppName(),
                AppDataDir = o.AppDataDirectory,
                ModelCacheDir = o.ModelCacheDirectory,
                LogsDir = o.LogsDirectory,
                CatalogRegion = o.CatalogRegion,
            };

            try
            {
                await FoundryLocalManager.CreateAsync(configuration, Logger).ConfigureAwait(false);
            }
            catch (FoundryLocalException) when (FoundryLocalManager.IsInitialized)
            {
                // The app (or another component) created the manager concurrently; reuse it.
            }
        }
        else
        {
            Log.ReusingManager(Logger);
        }

        return await FoundryLocalManager.Instance.GetCatalogAsync().ConfigureAwait(false);
    }

    private static string DefaultAppName() =>
        Assembly.GetEntryAssembly()?.GetName().Name is { Length: > 0 } name ? name : "Microsoft.AI.Local";

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Reusing the FoundryLocalManager created by the application.")]
        public static partial void ReusingManager(ILogger logger);

        [LoggerMessage(2, LogLevel.Information, "Downloading and registering execution providers: {Names}.")]
        public static partial void RegisteringExecutionProviders(ILogger logger, string names);

        [LoggerMessage(3, LogLevel.Warning, "Some execution providers could not be registered ({Failed}): {Status}. Models fall back to the variants that can run.")]
        public static partial void ExecutionProvidersFailed(ILogger logger, string failed, string status);
    }
}

internal sealed class FoundryCatalogModel : IFoundryCatalogModel
{
    public FoundryCatalogModel(IModel model)
    {
        Alias = model.Alias;
        Variants = model.Variants.Count == 0
            ? [new FoundryModelVariant(model)]
            : [.. model.Variants.Select(v => new FoundryModelVariant(v))];

        // The alias-level IModel delegates to the variant Foundry selected for this device.
        DefaultVariant = Variants.FirstOrDefault(v => v.Id == model.Id) ?? Variants[0];
    }

    public string Alias { get; }

    public IReadOnlyList<IFoundryModelVariant> Variants { get; }

    public IFoundryModelVariant DefaultVariant { get; }
}

internal sealed class FoundryModelVariant(IModel model) : IFoundryModelVariant
{
    public string Id => model.Id;

    public LocalDevice Device => model.Info.Runtime?.DeviceType switch
    {
        DeviceType.CPU => LocalDevice.Cpu,
        DeviceType.GPU => LocalDevice.Gpu,
        DeviceType.NPU => LocalDevice.Npu,
        _ => LocalDevice.Auto,
    };

    public string? ExecutionProvider => model.Info.Runtime?.ExecutionProvider;

    public object Native => model;

    public int? ContextLength => model.Info.ContextLength is { } value and > 0 and <= int.MaxValue ? (int)value : null;

    public int? MaxOutputTokens => model.Info.MaxOutputTokens is { } value and > 0 and <= int.MaxValue ? (int)value : null;

    public bool? SupportsToolCalling => model.Info.SupportsToolCalling;

    public Task<bool> IsCachedAsync(CancellationToken cancellationToken) => model.IsCachedAsync(cancellationToken);

    public Task<bool> IsLoadedAsync(CancellationToken cancellationToken) => model.IsLoadedAsync(cancellationToken);

    public Task DownloadAsync(Action<double> progress, CancellationToken cancellationToken) =>
        model.DownloadAsync(percent => progress(Math.Clamp(percent / 100.0, 0, 1)), cancellationToken);

    public Task LoadAsync(CancellationToken cancellationToken) => model.LoadAsync(cancellationToken);

    public Task UnloadAsync(CancellationToken cancellationToken) => model.UnloadAsync(cancellationToken);

    public IFoundryChatEngine CreateChatEngine() => new FoundryLocalChatEngine(model);

    public IFoundryEmbeddingEngine CreateEmbeddingEngine() => new FoundryLocalEmbeddingEngine(model);

    public IFoundrySpeechEngine CreateSpeechEngine() => new FoundryLocalSpeechEngine(model);

    public override string ToString() => Id;
}
