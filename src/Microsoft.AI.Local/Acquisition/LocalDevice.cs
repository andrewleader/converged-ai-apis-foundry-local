namespace Microsoft.AI.Local;

/// <summary>
/// A preferred compute device for running a model.
/// </summary>
public enum LocalDevice
{
    /// <summary>Let the provider pick the best device.</summary>
    Auto,

    /// <summary>The CPU.</summary>
    Cpu,

    /// <summary>A GPU.</summary>
    Gpu,

    /// <summary>A neural processing unit.</summary>
    Npu,
}
