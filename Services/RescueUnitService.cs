using System.Net;
using MaritimeCommand.Models;

namespace MaritimeCommand.Services;

// Handles Rescue Unit registration and stores the fleet away in memory.
public class RescueUnitService
{
    private readonly List<RescueUnit> _units = new();

    private int _nextUnitId = 1;

    public RescueUnit RegisterUnit(
        string name,
        UnitType type,
        double latitude,
        double longitude)
    {
        // Each Unit needs a name operators can identify them by.
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Please provide a unit name.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException("Please select a supported unit type.");
        }

        if (!double.IsFinite(latitude) || latitude < -90 || latitude > 90)
        {
            throw new ArgumentException("Latitude is out of range.");
        }

        if (!double.IsFinite(longitude) || longitude < -90 || longitude > 90)
        {
            throw new ArgumentException("Longitude is out of range.");
        }

        // Clean the name before comparing or storing it.
        string cleanedName = name.Trim();

        // Avoid registering two units with the same name.
        // OrdinalIgnoreCase lets our name checks ignore upper or lowercase differences.
        foreach (var existingUnit in _units)
        {
            if (string.Equals(
            existingUnit.Name,
            cleanedName,
            StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("A Unit with that name already exists.");
            }
        }

        // Our prototype registers units that are ready for dispatch.
        var unit = new RescueUnit
        {
            Id = _nextUnitId++,
            Name = cleanedName,
            Type = type,
            Status = UnitStatus.Available,
            Latitude = latitude,
            Longitude = longitude
        };

        _units.Add(unit);

        return unit;
    }

    // Allows callers to read the fleet without adding or removing units.
    public IReadOnlyList<RescueUnit> GetUnits()
    {
        return _units.AsReadOnly();
    }

    // Finds a particular unit or returns null if it does not exist.
    public RescueUnit? GetUnitById(int id)
    {
        foreach (var unit in _units)
        {
            if (unit.Id == id)
            {
                return unit;
            }
        }

        return null;
    }
}