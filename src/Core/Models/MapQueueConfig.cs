namespace IdleExile.Core.Models;

/// <summary>
/// Configuration options for the automated Map Device Queue (Task 4.2 & GDD 4.1).
/// </summary>
public sealed record MapQueueConfig(
    int MaxSlots = 10,
    float AutoAdvanceDelaySeconds = 3.0f,
    bool AutoAdvanceEnabled = true,
    bool ContinuousRunEnabled = false
);
