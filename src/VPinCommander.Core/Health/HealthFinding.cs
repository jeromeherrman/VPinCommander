namespace VPinCommander.Core.Health;

public enum HealthSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

/// <summary>One issue found by the health check.</summary>
public sealed record HealthFinding(
    HealthSeverity Severity,
    string Category,
    string Item,
    string Detail);
