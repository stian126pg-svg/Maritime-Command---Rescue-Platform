namespace MaritimeCommand.Models;

// Holds the information needed to save and restore a shift.
// This class describes the saved data; it does not read or write files.
public class ShiftState
{
    // Identifies the save format if we change its structure later.
    public int SaveVersion { get; set; } = 1;

    // Records when this snapshot was saved.
    public DateTimeOffset SavedAt { get; set; }

    // Preserve incidents, including their IDs and current statuses.
    public List<Incident> Incidents { get; set; } = new();

    // Preserve fleet positions, readiness, crews and capabilities.
    public List<RescueUnit> Units { get; set; } = new();

    // Preserve the connections between incidents and responding units.
    public List<Assignment> Assignments { get; set; } = new();

    // Preserve the chronological operational history.
    public List<OperationalLogEntry> LogEntries { get; set; } = new();

    // Preserve timers and automatic-event progress alongside our records.
    public SimulationState Simulation { get; set; } = new();
}