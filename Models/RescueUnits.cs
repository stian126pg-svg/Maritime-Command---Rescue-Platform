namespace MaritimeCommand.Models;

// Describes one rescue unit.
// The service will handle registration and operational rules.
public class RescueUnit
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public UnitType Type { get; set; }
    public UnitStatus Status { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}