namespace Microsoft.AI.Local;

/// <summary>The base class of exceptions thrown by local models and their clients.</summary>
public class LocalModelException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="LocalModelException"/> class.</summary>
    public LocalModelException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public LocalModelException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public LocalModelException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets or sets the identifier of the model involved, if known.</summary>
    public string? ModelId { get; init; }
}

/// <summary>Thrown when a model could not be made ready (for example a download or OS model delivery failed).</summary>
public class LocalModelNotReadyException : LocalModelException
{
    /// <summary>Initializes a new instance of the <see cref="LocalModelNotReadyException"/> class.</summary>
    public LocalModelNotReadyException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelNotReadyException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public LocalModelNotReadyException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelNotReadyException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public LocalModelNotReadyException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a model can't run here: on this platform, on this device, or in this app.</summary>
public class LocalModelNotSupportedException : LocalModelException
{
    /// <summary>Initializes a new instance of the <see cref="LocalModelNotSupportedException"/> class.</summary>
    public LocalModelNotSupportedException()
        : this(new ModelAvailability(ModelAvailabilityStatus.NotSupportedOnPlatform))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelNotSupportedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public LocalModelNotSupportedException(string? message)
        : this(new ModelAvailability(ModelAvailabilityStatus.NotSupportedOnPlatform, message))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelNotSupportedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public LocalModelNotSupportedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        Availability = new ModelAvailability(ModelAvailabilityStatus.NotSupportedOnPlatform, message);
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelNotSupportedException"/> class.</summary>
    /// <param name="availability">The availability that explains why the model is not supported.</param>
    /// <param name="modelId">The identifier of the model.</param>
    public LocalModelNotSupportedException(ModelAvailability availability, string? modelId = null)
        : base(CreateMessage(availability, modelId))
    {
        Availability = availability;
        ModelId = modelId;
    }

    /// <summary>Gets the availability that explains why the model is not supported.</summary>
    public ModelAvailability Availability { get; }

    private static string CreateMessage(ModelAvailability availability, string? modelId)
    {
        ArgumentNullException.ThrowIfNull(availability);
        var subject = modelId is null ? "The model" : $"The model '{modelId}'";
        return availability.Reason is null
            ? $"{subject} is not available ({availability.Status})."
            : $"{subject} is not available ({availability.Status}): {availability.Reason}";
    }
}

/// <summary>Thrown when a prompt, image or response is blocked by the provider's content moderation.</summary>
public class LocalModelContentFilteredException : LocalModelException
{
    /// <summary>Initializes a new instance of the <see cref="LocalModelContentFilteredException"/> class.</summary>
    public LocalModelContentFilteredException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelContentFilteredException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public LocalModelContentFilteredException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelContentFilteredException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public LocalModelContentFilteredException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets or sets a value indicating whether the input (as opposed to the output) was blocked.</summary>
    public bool IsInputFiltered { get; init; }
}

/// <summary>Thrown when the input is larger than the model's context.</summary>
public class LocalModelContextLengthExceededException : LocalModelException
{
    /// <summary>Initializes a new instance of the <see cref="LocalModelContextLengthExceededException"/> class.</summary>
    public LocalModelContextLengthExceededException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelContextLengthExceededException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public LocalModelContextLengthExceededException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelContextLengthExceededException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public LocalModelContextLengthExceededException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown, when <see cref="LocalAIOptions.ThrowOnUnsupportedOptions"/> is enabled, for a request option the
/// provider can't honor.
/// </summary>
public class LocalModelOptionNotSupportedException : LocalModelException
{
    /// <summary>Initializes a new instance of the <see cref="LocalModelOptionNotSupportedException"/> class.</summary>
    public LocalModelOptionNotSupportedException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelOptionNotSupportedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public LocalModelOptionNotSupportedException(string? message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalModelOptionNotSupportedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public LocalModelOptionNotSupportedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets or sets the name of the unsupported option, for example <c>ChatOptions.StopSequences</c>.</summary>
    public string? OptionName { get; init; }
}
