using MaritimeCommand.Models;

namespace MaritimeCommand.Services;

// Coordinates dispatch using the existing incident and fleet services.
public class DispatchService
{
    private readonly IncidentService _incidentService;
    private readonly RescueUnitService _rescueUnitService;

    private readonly List<Assignment> _assignments = new();
    private int _nextAssignmentId = 1;

    private readonly List<OperationalLogEntry> _logEntries = new();
    private int _nextLogEntryId = 1;


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

        // Capture the unit's departure position and dispatch time.
        RecordLogEntry(incident, unit, assignment.AssignedAt, $"{unit.Name} dispatched to incident #{incident.Id}.");

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

        // Use one timestamp for both the assignment and its log entry.
        var arrivalTime = DateTimeOffset.UtcNow;

        selectedAssignment.ArrivedAt = arrivalTime;

        unit.Latitude = incident.Latitude;
        unit.Longitude = incident.Longitude;

        // Capture the unit's position after arrival.
        RecordLogEntry(incident, unit, arrivalTime, $"{unit.Name} arrived at incident #{incident.Id}.");

        return selectedAssignment;
    }

    // Adds a situation report from a unit assigned to this incident.
    public void AddSituationReport(int incidentId, int unitId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Please provide a report message.");
        }

        var incident = _incidentService.GetIncidentById(incidentId);

        if (incident is null)
        {
            throw new ArgumentException("Incident not found.");
        }

        var unit = _rescueUnitService.GetUnitById(unitId);

        if (unit is null)
        {
            throw new ArgumentException("Rescue Unit not found.");
        }

        if (incident.Status == IncidentStatus.Concluded)
        {
            throw new InvalidOperationException("Cannot add a situation report to a concluded incident.");
        }

        // The Unit must have an active assignment to this particular case.
        bool hasActiveAssignment = false;

        foreach (var assignment in _assignments)
        {
            if (assignment.IncidentId == incidentId &&
                assignment.UnitId == unitId &&
                assignment.EndedAt is null)
            {
                hasActiveAssignment = true;
                break;
            }
        }

        if (!hasActiveAssignment)
        {
            throw new InvalidOperationException("This Unit has no active assignment to this incident.");
        }

        if (unit.Status != UnitStatus.Assigned)
        {
            throw new InvalidOperationException("The reporting Unit must be assigned.");
        }

        // All checks passed? Store the report using the existing helper.
        RecordLogEntry(incident, unit, DateTimeOffset.UtcNow, $"{unit.Name} reports: {message.Trim()}");
    }

    public IReadOnlyList<Assignment> GetAssignments()
    {
        return _assignments.AsReadOnly();
    }

    // Used internally after a successful operational action.
    private void RecordLogEntry(Incident incident, RescueUnit rescueUnit, DateTimeOffset timestamp, string message)
    {
        var entry = new OperationalLogEntry
        {
            Id = _nextLogEntryId++,
            IncidentId = incident.Id,
            UnitId = rescueUnit.Id,
            Timestamp = timestamp,
            Latitude = rescueUnit.Latitude,
            Longitude = rescueUnit.Longitude,
            Message = message
        };

        _logEntries.Add(entry);
    }
    
    // Returns entries for one incident in their recorded order.
    public IReadOnlyList<OperationalLogEntry> GetIncidentLog(int incidentId)
    {
        if (_incidentService.GetIncidentById(incidentId) is null)
        {
            throw new ArgumentException("Incident not found.");
        }

        var incidentEntries = new List<OperationalLogEntry>();

        foreach (var entry in _logEntries)
        {
            if (entry.IncidentId == incidentId)
            {
                incidentEntries.Add(entry);
            }
        }

        return incidentEntries.AsReadOnly();
    }
}