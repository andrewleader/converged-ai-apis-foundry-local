namespace Microsoft.AI.Local.Providers;

/// <summary>
/// Normalizes raw provider progress (per-stage fractions or percentages) into monotonic
/// <see cref="ModelAcquisitionProgress"/> updates with an overall fraction.
/// </summary>
public sealed class AcquisitionProgressReporter
{
    private readonly object _gate = new();
    private readonly IProgress<ModelAcquisitionProgress>? _sink;
    private readonly KeyValuePair<AcquisitionStage, double>[] _plan;
    private readonly double _totalWeight;
    private AcquisitionStage? _stage;
    private double _stageFraction;
    private double _overall;

    /// <summary>Initializes a new instance of the <see cref="AcquisitionProgressReporter"/> class.</summary>
    /// <param name="sink">The receiver of normalized progress.</param>
    /// <param name="plan">The relative weight of each stage, in order.</param>
    public AcquisitionProgressReporter(IProgress<ModelAcquisitionProgress>? sink, IEnumerable<KeyValuePair<AcquisitionStage, double>> plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _sink = sink;
        _plan = [.. plan.Where(p => p.Value > 0)];
        _totalWeight = _plan.Sum(p => p.Value);
    }

    /// <summary>Reports progress within a stage.</summary>
    /// <param name="stage">The current stage.</param>
    /// <param name="fraction">Progress within the stage, from 0.0 to 1.0. Values outside the range are clamped.</param>
    /// <param name="detail">An optional human-readable detail, such as the component being downloaded.</param>
    public void Report(AcquisitionStage stage, double fraction, string? detail = null)
    {
        if (double.IsNaN(fraction))
        {
            fraction = 0;
        }

        fraction = Math.Clamp(fraction, 0, 1);
        ModelAcquisitionProgress update;
        lock (_gate)
        {
            if (_stage == stage)
            {
                // Within a stage, progress never goes backwards (providers sometimes report retries as restarts).
                fraction = Math.Max(fraction, _stageFraction);
            }

            _stage = stage;
            _stageFraction = fraction;
            _overall = Math.Max(_overall, ComputeOverall(stage, fraction));
            update = new ModelAcquisitionProgress(stage, fraction, _overall, detail);
        }

        _sink?.Report(update);
    }

    /// <summary>Reports progress within a stage as a percentage.</summary>
    /// <param name="stage">The current stage.</param>
    /// <param name="percent">Progress within the stage, from 0 to 100.</param>
    /// <param name="detail">An optional human-readable detail.</param>
    public void ReportPercent(AcquisitionStage stage, double percent, string? detail = null) => Report(stage, percent / 100.0, detail);

    internal void Complete()
    {
        ModelAcquisitionProgress update;
        lock (_gate)
        {
            if (_overall >= 1)
            {
                return;
            }

            _stage ??= AcquisitionStage.Loading;
            _stageFraction = 1;
            _overall = 1;
            update = new ModelAcquisitionProgress(_stage.Value, 1, 1);
        }

        _sink?.Report(update);
    }

    private double ComputeOverall(AcquisitionStage stage, double fraction)
    {
        if (_totalWeight <= 0)
        {
            return 0;
        }

        double before = 0;
        foreach (var (s, weight) in _plan)
        {
            if (s == stage)
            {
                return (before + (weight * fraction)) / _totalWeight;
            }

            if (s < stage)
            {
                before += weight;
            }
        }

        // Stage not in the plan: everything before it is done.
        return before / _totalWeight;
    }
}
