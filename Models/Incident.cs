namespace MaritimeCommand.Models;

// Contains the information belonging to one incident.
// Registration code will supply the ID, timestamp and other values.
public class Incident
{
    public int Id { get; set; }
    public IncidentType Type { get; set; }
    public string Description { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
    public IncidentStatus Status { get; set; }
}