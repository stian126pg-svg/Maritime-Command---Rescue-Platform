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

    // Records the arrival for an active assignment.
    public Assignment RecordArrival(int assignmentId)
    {
        // Find the assignment we want to update.
        Assignment? selectedAssignment = null;

        foreach (var assignment in _assignments)
        {
            if (assignment.Id == assignmentId)
            {
                selectedAssignment = assignment;
                break;
            }
        }

        if (selectedAssignment is null)
        {
            throw new ArgumentException("Assignment not found.");
        }

        if (selectedAssignment.EndedAt is not null)
        {
            throw new InvalidOperationException("Cannot record arrival for an ended assignment.");
        }

        if (selectedAssignment.ArrivedAt is not null)
        {
            throw new InvalidOperationException("Arrival has already been recorded.");
        }

        // Find the incident and unit connected to this assignment.
        var incident = _incidentService.GetIncidentById(selectedAssignment.IncidentId);

        var unit = _rescueUnitService.GetUnitById(selectedAssignment.UnitId);

        if (incident is null || unit is null)
        {
            throw new InvalidOperationException("The assignment's incident or unit could not be found.");
        }

        if (incident.Status == IncidentStatus.Concluded)
        {
            throw new InvalidOperationException("Cannot record arrival at a concluded incident.");
        }

        if (unit.Status != UnitStatus.Assigned)
        {
            throw new InvalidOperationException("The unit must be assigned before recording arrival");
        }

        // All checks passed. Record the time and update the unit's position.
        selectedAssignment.ArrivedAt = DateTimeOffset.UtcNow;

        unit.Latitude = incident.Latitude;
        unit.Longitude = incident.Longitude;

        return selectedAssignment;
    }

    public IReadOnlyList<Assignment> GetAssignments()
    {
        return _assignments.AsReadOnly();
    }
}