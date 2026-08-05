namespace Motion.Avalonia;

/// <summary>
/// Stores one live endpoint value and the metadata authored at that endpoint.
/// </summary>
public readonly record struct MotionEndpointValue(
    object? Value,
    MotionInterpolationMetadata Metadata
);