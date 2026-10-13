namespace MaritimeCommand.Models;

// Records one event in an incident's operational history.
public class OperationalLogEntry
{
    public int Id { get; set; }

    public int IncidentId { get; set; }

    // Some future entires may concern the incident without a specific unit.
    public int? UnitId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Message { get; set; } = "";
}