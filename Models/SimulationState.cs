namespace MaritimeCommand.Models;

// Stores simulation progress so loading can continue where we stopped.
public class SimulationState
{
    // Assignment ID → remaining journey time to the incident.
    public Dictionary<int, TimeSpan> RemainingTravel { get; set; } = new();

    // Unit ID → remaining journey time back to HQ.
    public Dictionary<int, TimeSpan> RemainingReturnTravel { get; set; } = new();

    // Assignment ID → remaining wait before its first situation report.
    public Dictionary<int, TimeSpan> RemainingAssessment { get; set; } = new();

    // Assignments that have already produced their automatic report.
    public HashSet<int> ReportedAssignments { get; set; } = new();

    // Preserve whether the operator paused the simulation.
    public bool IsRunning { get; set; } = true;

    // Save a duration rather than a real-world deadline.
    public TimeSpan UntilNextIncidentAttempt { get; set; } =
        TimeSpan.FromSeconds(10);

    // Preserve the increasing chance of an incoming incident.
    public int CurrentIncidentChance { get; set; } = 10;
}