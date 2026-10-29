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
        double longitude,
        int crewCount = 1,
        List<Capability>? capabilities = null)
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

        if (crewCount < 1)
        {
            throw new ArgumentException("A unit must have at least one crew member or operator.");
        }

        // Copy the supplied collection so the caller doesn't own our stored list.
        var registeredCapabilities = new List<Capability>();

        if (capabilities is not null)
        {
            foreach (var capability in capabilities)
            {
                if (!Enum.IsDefined(capability))
                {
                    throw new ArgumentException("Please selected supported unit capabilities.");
                }

                // Store each capability only once.
                if (!registeredCapabilities.Contains(capability))
                {
                    registeredCapabilities.Add(capability);
                }
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
            Longitude = longitude,
            CrewCount = crewCount,
            Capabilities = registeredCapabilities
        };

        _units.Add(unit);

        return unit;
    }

    // Allows callers to read the fleet without adding or removing units.
    public IReadOnlyList<RescueUnit> GetUnits()
    {
        return _units.AsReadOnly();
    }

    // Restores the saved fleet and continues numbering after its highest ID.
    public void RestoreUnits(List<RescueUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        // Check the incoming collection before replacing the current fleet.
        var restoredUnits = new List<RescueUnit>(units);

        var seenIds = new HashSet<int>();

        // Match registration's rule: names are unique regardless of casing.
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int highestId = 0;

        foreach (var unit in restoredUnits)
        {
            if (unit is null)
            {
                throw new InvalidDataException("The save contains a missing rescue unit record.");
            }

            if (unit.Id < 1 || unit.Id == int.MaxValue || !seenIds.Add(unit.Id))
            {
                throw new InvalidDataException("The save contains an invalid or duplicate rescue unit ID.");
            }

            if (string.IsNullOrWhiteSpace(unit.Name) || !seenNames.Add(unit.Name.Trim()))
            {
                throw new InvalidDataException("The save contains a missing or duplicate rescue unit name.");
            }

            if (!Enum.IsDefined(unit.Type) || !Enum.IsDefined(unit.Status))
            {
                throw new InvalidDataException($"Unit #{unit.Id} has an unsupported type or status.");
            }

            if (!double.IsFinite(unit.Latitude) ||
                unit.Latitude < -90 ||
                unit.Latitude > 90 ||
                !double.IsFinite(unit.Longitude) ||
                unit.Longitude < -180 ||
                unit.Longitude > 180)
            {
                throw new InvalidDataException($"Unit #{unit.Id} has invalid coordinates.");
            }

            if (unit.CrewCount < 1)
            {
                throw new InvalidDataException($"Unit #{unit.Id} must have at least one crew member or operator.");
            }

            if (unit.Capabilities is null)
            {
                throw new InvalidDataException($"Unit #{unit.Id} is missing its capability list.");
            }

            var seenCapabilities = new HashSet<Capability>();

            foreach (var capability in unit.Capabilities)
            {
                if (!Enum.IsDefined(capability) || !seenCapabilities.Add(capability))
                {
                    throw new InvalidDataException($"Unit #{unit.Id} has an unsupported or duplicate capability.");
                }
            }

            highestId = Math.Max(highestId, unit.Id);
        }

        // Every record passed: replace the fleet and restore its ID counter.
        _units.Clear();
        _units.AddRange(restoredUnits);

        _nextUnitId = highestId + 1;
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