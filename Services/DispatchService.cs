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

    // One shared HQ for our prototype.
    // const means these values cannot change while the program runs.
    public const double HeadquartersLatitude = 59.03;
    public const double HeadquartersLongitude = 9.72;

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
    public void AddSituationReport(int incidentId, int unitId, string message, bool backupRequested = false)
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
            if (assignment.IncidentId == incidentId && assignment.UnitId == unitId && assignment.EndedAt is null)
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

        // Store the report, clearly identifying an explicit backup request.
        string reportMessage = $"{unit.Name} reports: {message.Trim()}";

        if (backupRequested)
        {
            reportMessage = "[BACKUP REQUESTED] " + reportMessage;
        }

        RecordLogEntry(incident, unit, DateTimeOffset.UtcNow, reportMessage);

        // An ordinary update must not clear and outstanding request.
        if (backupRequested)
        {
            incident.BackupRequested = true;
        }
        
    }

    // Concludes a case after the operator confirms the response is complete.
    public Incident ConcludeIncident(int incidentId, string conclusion, bool responseAndHandoversComplete)
    {
        var incident = _incidentService.GetIncidentById(incidentId);


        if (incident is null)
        {
            throw new ArgumentException("Incident not found.");
        }

        if (incident.BackupRequested)
        {
            throw new InvalidOperationException(
                "Please resolve the outstanding backup request before concluding the incident.");
        }

        if (incident.Status == IncidentStatus.Concluded)
        {
            throw new InvalidOperationException("This incident is already concluded.");
        }

        if (string.IsNullOrWhiteSpace(conclusion))
        {
            throw new ArgumentException("Please provide a conclusion.");
        }

        if (!responseAndHandoversComplete)
        {
            throw new InvalidOperationException("Please confirm that assistance and necessary handovers are complete.");
        }

        // Gather and check everything before changing any operational state.
        var activeAssignments = new List<Assignment>();
        var returningUnits = new List<RescueUnit>();

        foreach (var assignment in _assignments)
        {
            if (assignment.IncidentId == incidentId && assignment.EndedAt is null)
            {
                var unit = _rescueUnitService.GetUnitById(assignment.UnitId);

                if (unit is null)
                {
                    throw new InvalidOperationException("An assigned rescue unit could not be found.");
                }

                if (unit.Status != UnitStatus.Assigned)
                {
                    throw new InvalidOperationException("An assigned unit has an inconsistent status.");
                }

            activeAssignments.Add(assignment);
            returningUnits.Add(unit);
        }
    }


    // All checks passed? Use one timestamp for the conclusion.
    var concludedAt = DateTimeOffset.UtcNow;

    incident.Status = IncidentStatus.Concluded;
    incident.ConcludedAt = concludedAt;

    // Preserve assignment records, but mark them as ended.
    foreach (var assignment in activeAssignments)
    {
        assignment.EndedAt = concludedAt;
    }

    // Returning does not mean ready for another dispatch.
    foreach (var unit in returningUnits)
    {
        unit.Status = UnitStatus.Returning;

        RecordLogEntry(incident, unit, concludedAt, $"{unit.Name} released from incident; returning to HQ.");
    }

    // No specific unit: use the incident's coordinates.
    RecordLogEntry(incident, null, concludedAt, $"Incident concluded: {conclusion.Trim()} Awaiting detailed report.");

        return incident;
    }

    // Completes the journey home without making the unit dispatch-ready.
    public RescueUnit RecordReturnToHeadquarters(int unitId)
    {
        var unit = _rescueUnitService.GetUnitById(unitId);

        if (unit is null)
        {
            throw new ArgumentException("Rescue unit not found.");
        }

        if (unit.Status != UnitStatus.Returning)
        {
            throw new InvalidOperationException(
                "The unit must be returning before recording arrival at HQ.");
        }

        // A unit cannot complete its return while still actively assigned.
        foreach (var assignment in _assignments)
        {
            if (assignment.UnitId == unitId &&
                assignment.EndedAt is null)
            {
                throw new InvalidOperationException(
                    "The unit still has an active assignment.");
            }
        }

        // Arrival changes its position, but servicing is still required.
        unit.Latitude = HeadquartersLatitude;
        unit.Longitude = HeadquartersLongitude;
        unit.Status = UnitStatus.Unavailable;

        return unit;
    }

    // Records the operator's assessment of a unit's readiness.
    public RescueUnit AssessUnitReadiness(int unitId, bool readyForDispatch)
    {
        var unit = _rescueUnitService.GetUnitById(unitId);

        if (unit is null)
        {
            throw new ArgumentException("Rescue unit not found");
        }

        // A readiness assessment must not interrupt an active assignment.
        foreach (var assignment in _assignments)
        {
            if (assignment.UnitId == unitId && assignment.EndedAt is null)
            {
                throw new InvalidOperationException("Cannot change readiness while the unit has an active assignment.");
            }
        }

        // Also guard against an inconsistent Assigned status.
        if (unit.Status == UnitStatus.Assigned)
        {
            throw new InvalidOperationException("The unit is still marked as assigned.");
        }

        // Returning units must reach HQ before their readiness is assessed.
        if (unit.Status == UnitStatus.Returning)
        {
            throw new InvalidOperationException("The unit must arrive at HQ before readiness can be assessed.");
        }

        if (readyForDispatch)
        {
            unit.Status = UnitStatus.Available;
        }

        else
        {
            unit.Status = UnitStatus.Unavailable;
        }

        return unit;
    }

    // Redirects an actively assigned unit to another open incident.
    public Assignment ReassignUnit(int unitId, int targetIncidentId, bool readinessConfirmed)
    {
        var unit = _rescueUnitService.GetUnitById(unitId);

        if (unit is null)
        {
            throw new ArgumentException("Rescue unit not found.");
        }

        var targetIncident = _incidentService.GetIncidentById(targetIncidentId);

        if (targetIncident is null)
        {
            throw new ArgumentException("Target incident not found.");
        }

        if (targetIncident.Status == IncidentStatus.Concluded)
        {
            throw new InvalidOperationException("Cannot reassign to a concluded incident.");
        }

        if (!readinessConfirmed)
        {
            throw new InvalidOperationException("Confirm the unit is ready for another incident.");
        }

        if (unit.Status != UnitStatus.Assigned)
        {
            throw new InvalidOperationException("The unit must be assigned before direct reassignment");
        }

        Assignment? currentAssignment = null;

        foreach (var assignment in _assignments)
        {
            if (assignment.UnitId == unitId && assignment.EndedAt is null)
            {
                // Guard against inconsistent assignment records.
                if (currentAssignment is not null)
                {
                    throw new InvalidOperationException("The unit has multiple active assignments.");
                }

                currentAssignment = assignment;
            }
        }

        if (currentAssignment is null)
        {
            throw new InvalidOperationException("The unit has no active assignment.");
        }

        if (currentAssignment.IncidentId == targetIncidentId)
        {
            throw new InvalidOperationException("The unit is already assigned to this incident.");
        }

        var previousIncident = _incidentService.GetIncidentById(currentAssignment.IncidentId);

        if (previousIncident is null || previousIncident.Status != IncidentStatus.Ongoing)
        {
            throw new InvalidOperationException("The current assignment must belong to an ongoing incident.");
        }

        // All checks passed? End the old assignment and start the new one.
        var reassignedAt = DateTimeOffset.UtcNow;

        var newAssignment = new Assignment
        {
            Id = _nextAssignmentId++,
            IncidentId = targetIncident.Id,
            UnitId = unit.Id,
            AssignedAt = reassignedAt
        };

        currentAssignment.EndedAt = reassignedAt;
        _assignments.Add(newAssignment);

        targetIncident.Status = IncidentStatus.Ongoing;

        // The unit remains Assigned, at its current position.
        RecordLogEntry(
            previousIncident,
            unit,
            reassignedAt,
            $"{unit.Name} redirected to incident #{targetIncident.Id}.");

        RecordLogEntry(
            targetIncident,
            unit,
            reassignedAt,
            $"{unit.Name} dispatched from incident #{previousIncident.Id}.");

        return newAssignment;
    }

    public IReadOnlyList<Assignment> GetAssignments()
    {
        return _assignments.AsReadOnly();
    }

    // Restores assignment history after incidents and units have been restored.
    public void RestoreAssignments(List<Assignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var restoredAssignments = new List<Assignment>(assignments);
        var seenIds = new HashSet<int>();
        var activelyAssignedUnitIds = new HashSet<int>();

        int highestId = 0;

        foreach (var assignment in restoredAssignments)
        {
            if (assignment is null)
            {
                throw new InvalidDataException("The save contains a missing assignment record.");
            }

            if (assignment.Id < 1 || assignment.Id == int.MaxValue || !seenIds.Add(assignment.Id))
            {
                throw new InvalidDataException("The save contains an invalid or duplicate assignment ID.");
            }

            var incident = _incidentService.GetIncidentById(assignment.IncidentId);

            var unit = _rescueUnitService.GetUnitById(assignment.UnitId);

            if (incident is null || unit is null)
            {
                throw new InvalidDataException($"Assignment #{assignment.Id} references a missing incident or unit.");
            }

            // Arrival and ending cannot happen before dispatch.
            if (assignment.AssignedAt == default ||
                (assignment.ArrivedAt is DateTimeOffset arrivedAt &&
                arrivedAt < assignment.AssignedAt) ||
                (assignment.EndedAt is DateTimeOffset endedAt &&
                endedAt < assignment.AssignedAt))
            {
                throw new InvalidDataException($"Assignment #{assignment.Id} has invalid timestamps.");
            }

            // An arrival cannot happen after the assignment has ended.
            if (assignment.ArrivedAt is DateTimeOffset arrival &&
                assignment.EndedAt is DateTimeOffset ending &&
                arrival > ending)
            {
                throw new InvalidDataException($"Assignment #{assignment.Id} records arrival after ending.");
            }

            if (assignment.EndedAt is null)
            {
                // Active assignments must agree with operational statuses.
                if (incident.Status != IncidentStatus.Ongoing || unit.Status != UnitStatus.Assigned)
                {
                    throw new InvalidDataException($"Active assignment #{assignment.Id} has inconsistent statuses.");
                }

                if (!activelyAssignedUnitIds.Add(unit.Id))
                {
                    throw new InvalidDataException($"Unit #{unit.Id} has multiple active assignments.");
                }
            }

            highestId = Math.Max(highestId, assignment.Id);
        }

        // Also check the opposite direction:
        // every Assigned unit must have an active assignment.
        foreach (var unit in _rescueUnitService.GetUnits())
        {
            if (unit.Status == UnitStatus.Assigned && !activelyAssignedUnitIds.Contains(unit.Id))
            {
                throw new InvalidDataException($"Unit #{unit.Id} is Assigned but has no active assignment.");
            }
        }

        // All checks passed: replace assignment history and resume numbering.
        _assignments.Clear();
        _assignments.AddRange(restoredAssignments);

        _nextAssignmentId = highestId + 1;
    }

    // Provides the full operational history for saving.
    public IReadOnlyList<OperationalLogEntry> GetLogEntries()
    {
        return _logEntries.AsReadOnly();
    }

    // Restores operational history after incidents and units are restored.
    public void RestoreLogEntries(List<OperationalLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var restoredEntries = new List<OperationalLogEntry>(entries);
        var seenIds = new HashSet<int>();

        int highestId = 0;

        foreach (var entry in restoredEntries)
        {
            if (entry is null)
            {
                throw new InvalidDataException(
                    "The save contains a missing operational log entry.");
            }

            if (entry.Id < 1 ||
                entry.Id == int.MaxValue ||
                !seenIds.Add(entry.Id))
            {
                throw new InvalidDataException(
                    "The save contains an invalid or duplicate log entry ID.");
            }

            if (_incidentService.GetIncidentById(entry.IncidentId) is null)
            {
                throw new InvalidDataException(
                    $"Log entry #{entry.Id} references a missing incident.");
            }

            // Some entries describe the case itself, without a specific unit.
            if (entry.UnitId is int unitId &&
                _rescueUnitService.GetUnitById(unitId) is null)
            {
                throw new InvalidDataException(
                    $"Log entry #{entry.Id} references a missing rescue unit.");
            }

            if (entry.Timestamp == default ||
                string.IsNullOrWhiteSpace(entry.Message))
            {
                throw new InvalidDataException(
                    $"Log entry #{entry.Id} is missing its timestamp or message.");
            }

            if (!double.IsFinite(entry.Latitude) ||
                entry.Latitude < -90 ||
                entry.Latitude > 90 ||
                !double.IsFinite(entry.Longitude) ||
                entry.Longitude < -180 ||
                entry.Longitude > 180)
            {
                throw new InvalidDataException(
                    $"Log entry #{entry.Id} has invalid coordinates.");
            }

            highestId = Math.Max(highestId, entry.Id);
        }

        // Preserve the recorded sequence and original timestamps.
        _logEntries.Clear();
        _logEntries.AddRange(restoredEntries);

        _nextLogEntryId = highestId + 1;
    }

    // Records and event, optionally connected to specific unit.
    private void RecordLogEntry(Incident incident, RescueUnit? rescueUnit, DateTimeOffset timestamp, string message)
    {
        var entry = new OperationalLogEntry
        {
            Id = _nextLogEntryId++,
            IncidentId = incident.Id,
            UnitId = rescueUnit?.Id,
            Timestamp = timestamp,
            Latitude = rescueUnit?.Latitude ?? incident.Latitude,
            Longitude = rescueUnit?.Longitude ?? incident.Longitude,
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

    // Records the operator's decision about an outstanding backup request.
    public void ResolveBackupRequest(int incidentId, string resolution)
    {
        var incident = _incidentService.GetIncidentById(incidentId);

        if (incident is null)
        {
            throw new ArgumentException("Incident not found.");
        }

        if (incident.Status == IncidentStatus.Concluded)
        {
            throw new InvalidOperationException(
                "Cannot resolve a backup request on a concluded incident.");
        }

        if (!incident.BackupRequested)
        {
            throw new InvalidOperationException(
                "This incident has no outstanding backup request.");
        }

        if (string.IsNullOrWhiteSpace(resolution))
        {
            throw new ArgumentException(
                "Please explain how the backup request was resolved.");
        }

        RecordLogEntry(
            incident,
            null,
            DateTimeOffset.UtcNow,
            $"Backup request resolved: {resolution.Trim()}");

        incident.BackupRequested = false;
    }
}
