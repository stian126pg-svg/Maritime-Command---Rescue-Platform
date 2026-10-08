using MaritimeCommand.Models;

namespace MaritimeCommand.Services;

// Coordinates dispatch using the existing incident and fleet services.
public class DispatchService
{
    private readonly IncidentService _incidentService;
    private readonly RescueUnitService _rescueUnitService;

    private readonly List<Assignment> _assignments = new();

    private int _nextAssignmentId = 1;

    // Receive the services that already hold our incidents and units.
    public DispatchService(IncidentService incidentService, RescueUnitService rescueUnitService)
    {
        _incidentService = incidentService;
        _rescueUnitService = rescueUnitService;
    }

    public Assignment AssignUnit(int incidentId, int unitId)
    {
        //Both the incident and unit must exist.
        var incident = _incidentService.GetIncidentById(incidentId);

        if (incident is null)
        {
            throw new ArgumentException("Incident not found.");
        }

        var unit = _rescueUnitService.GetUnitById(unitId);

        if (unit is null)
        {
            throw new ArgumentException("Rescue unit not found.");
        }

        // Concluded incidents cannot receive new assignments.
        if (incident.Status == IncidentStatus.Concluded)
        {
            throw new InvalidOperationException("Cannot dispatch to a concluded incident.");
        }

        if (unit.Status != UnitStatus.Available)
        {
            throw new InvalidOperationException("This unit is not available for dispatch.");
        }

        // Also check assignment records before committing a dispatch.
        foreach (var existingAssignment in _assignments)
        {
            if (existingAssignment.UnitId == unitId && existingAssignment.EndedAt is null)
            {
                throw new InvalidOperationException("This unit already has an active assignment.");
            }
        }

        // All checks passed? Record the dispatch.
        var assignment = new Assignment
        {
            Id = _nextAssignmentId++,
            IncidentId = incident.Id,
            UnitId = unit.Id,
            AssignedAt = DateTimeOffset.UtcNow
        };

        _assignments.Add(assignment);

        // Dispatch changes both operational states.
        unit.Status = UnitStatus.Assigned;
        incident.Status = IncidentStatus.Ongoing;

        return assignment;
    }

    public IReadOnlyList<Assignment> GetAssignments()
    {
        return _assignments.AsReadOnly();
    }
}