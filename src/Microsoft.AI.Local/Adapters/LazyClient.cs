namespace Microsoft.AI.Local.Adapters;

/// <summary>Creates a model's client on first use; failed or cancelled creations are retried by the next caller.</summary>
internal sealed class LazyClient<TClient> : IDisposable
    where TClient : class, IDisposable
{
    private readonly ILocalModel<TClient> _model;
    private readonly object _gate = new();
    private Task<TClient>? _client;
    private bool _disposed;

    public LazyClient(ILocalModel<TClient> model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public ILocalModel<TClient> Model => _model;

    public TClient? CreatedClient
    {
        get
        {
            var task = _client;
            return task is { IsCompletedSuccessfully: true } ? task.Result : null;
        }
    }

    public Task<TClient> GetAsync(CancellationToken cancellationToken)
    {
        Task<TClient> task;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_client is null || _client.IsFaulted || _client.IsCanceled)
            {
                _client = _model.CreateClientAsync(cancellationToken);
            }

            task = _client;
        }

        return task.IsCompleted ? task : task.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        Task<TClient>? task;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            task = _client;
        }

        if (task is null)
        {
            return;
        }

        if (task.IsCompletedSuccessfully)
        {
            task.Result.Dispose();
        }
        else if (!task.IsCompleted)
        {
            // Dispose the client when an in-flight creation completes.
            _ = task.ContinueWith(
                static t => t.Result.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
