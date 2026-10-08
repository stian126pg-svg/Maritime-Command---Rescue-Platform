namespace MaritimeCommand.Models;

// Records a unit's assignment to an incident.
public class Assignment
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int UnitId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }

    // Null means the assignment hasn't ended/concluded.
    public DateTimeOffset? EndedAt { get; set; }
}