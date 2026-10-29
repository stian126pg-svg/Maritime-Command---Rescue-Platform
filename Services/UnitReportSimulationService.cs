using MaritimeCommand.Models;

namespace MaritimeCommand.Services;

// Generates one initial situation report after a unit arrives.
public class UnitReportSimulationService
{
    private readonly IncidentService _incidentService;
    private readonly RescueUnitService _rescueUnitService;
    private readonly DispatchService _dispatchService;

    private readonly Dictionary<int, TimeSpan> _remainingAssessment = new();

    // Remembers which assignments have already sent their initial report.
    private readonly HashSet<int> _reportedAssignments = new();

    public UnitReportSimulationService(IncidentService incidentService, RescueUnitService rescueUnitService,
        DispatchService dispatchService)
    {
        _incidentService = incidentService;
        _rescueUnitService = rescueUnitService;
        _dispatchService = dispatchService;
    }

    public List<string> Update(TimeSpan elapsed)
    {
        var notifications = new List<string>();

        foreach (var assignment in _dispatchService.GetAssignments())
        {
            // Ended assignments must stop producing reports.
            if (assignment.EndedAt is not null)
            {
                _remainingAssessment.Remove(assignment.Id);
                continue;
            }

            // Assessment starts only after arrival, and happens once.
            if (assignment.ArrivedAt is null || _reportedAssignments.Contains(assignment.Id))
            {
                continue;
            }

            var incident = _incidentService.GetIncidentById(assignment.IncidentId);

            var unit = _rescueUnitService.GetUnitById(assignment.UnitId);

            if (incident is null || unit is null || incident.Status == IncidentStatus.Concluded)
            {
                continue;
            }

            if (!_remainingAssessment.ContainsKey(assignment.Id))
            {
                _remainingAssessment[assignment.Id] = TimeSpan.FromSeconds(30);

                continue;
            }

            _remainingAssessment[assignment.Id] -= elapsed;

            if (_remainingAssessment[assignment.Id] > TimeSpan.Zero)
            {
                continue;
            }

            // Look up the main capability needed at this incident.
            Capability? requiredCapability = GetRequiredCapability(incident.Type);

            // Consider the combined capabilities of units already at the scene.
            bool specialistSupportNeeded = requiredCapability.HasValue && !HasCapabilityOnScene(
                incident.Id,
                requiredCapability.Value);

            // Missing specialist equipment requires backup.
            // Otherwise, unexpected complications still have a 25% chance to occur.
            bool backupRequested = specialistSupportNeeded || Random.Shared.Next(100) < 25;

            string message = CreateAssessment(incident.Type, backupRequested);

            // Reuse validation and operational logging.
            _dispatchService.AddSituationReport(
                incident.Id,
                unit.Id,
                message,
                backupRequested);

            _reportedAssignments.Add(assignment.Id);
            _remainingAssessment.Remove(assignment.Id);

            notifications.Add($"{unit.Name}, incident #{incident.Id}: {message}");
        }

        return notifications;
    }

    private string CreateAssessment(IncidentType type, bool backupRequested)
    {
        string assessment = type switch
        {
            IncidentType.Fire =>
                "Smoke and fire confirmed. Assessing personnel and access.",

            IncidentType.MedicalEmergency =>
                "Patient located. Assessing condition and transport needs.",

            IncidentType.TechnicalFailure =>
                "Disabled vessel located. Assessing damage and towing needs.",

            IncidentType.EmergencyEvacuation =>
                "Crew located. Assessing evacuation and transport needs.",

            IncidentType.SearchAndRescue =>
                "Search underway. No confirmed contact with the missing person.",

            _ => throw new ArgumentException(
                "Unsupported incident type.")
        };

        if (backupRequested)
        {
            return assessment +
                " Requesting an additional suitable unit to assist.";
        }

        return assessment +
            " No additional units requested at present.";
    }

    // Copies assessment timers and remembers which reports already happened.
    public void CaptureState(SimulationState state)
    {
        state.RemainingAssessment =
            new Dictionary<int, TimeSpan>(_remainingAssessment);

        state.ReportedAssignments =
            new HashSet<int>(_reportedAssignments);
    }

    // Restores report progress after assignments have been restored.
    public void RestoreState(SimulationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.RemainingAssessment is null ||
            state.ReportedAssignments is null)
        {
            throw new InvalidDataException(
                "The save is missing report simulation collections.");
        }

        // Validate separate copies before changing the running simulation.
        var assessmentTimers =
            new Dictionary<int, TimeSpan>(state.RemainingAssessment);

        var reportedAssignments =
            new HashSet<int>(state.ReportedAssignments);

        var assignments = _dispatchService.GetAssignments()
            .ToDictionary(assignment => assignment.Id);

        foreach (var timer in assessmentTimers)
        {
            if (timer.Value < TimeSpan.Zero ||
                !assignments.TryGetValue(timer.Key, out var assignment))
            {
                throw new InvalidDataException(
                    "The save contains an invalid assessment timer.");
            }

            // Assessments belong to active assignments already on scene.
            if (assignment.EndedAt is not null ||
                assignment.ArrivedAt is null)
            {
                throw new InvalidDataException(
                    $"Assignment #{assignment.Id} should not have an assessment timer.");
            }

            // A completed initial report must not also be waiting to happen.
            if (reportedAssignments.Contains(assignment.Id))
            {
                throw new InvalidDataException(
                    $"Assignment #{assignment.Id} has both a pending and completed report.");
            }
        }

        foreach (int assignmentId in reportedAssignments)
        {
            if (!assignments.TryGetValue(assignmentId, out var assignment))
            {
                throw new InvalidDataException(
                    $"A completed report references missing assignment #{assignmentId}.");
            }

            if (assignment.ArrivedAt is null)
            {
                throw new InvalidDataException(
                    $"Assignment #{assignmentId} has a completed assessment without arrival.");
            }
        }

        // All checks passed: restore pending assessments.
        _remainingAssessment.Clear();

        foreach (var timer in assessmentTimers)
        {
            _remainingAssessment.Add(timer.Key, timer.Value);
        }

        // Restore the memory of reports already produced.
        _reportedAssignments.Clear();
        _reportedAssignments.UnionWith(reportedAssignments);
    }

    // Identifies the main capability needed for this kind of incident.
    private static Capability? GetRequiredCapability(IncidentType incidentType)
    {
        return incidentType switch
        {
            IncidentType.Fire => Capability.Firefighting,
            IncidentType.MedicalEmergency => Capability.Medical,
            IncidentType.EmergencyEvacuation => Capability.Transport,
            IncidentType.SearchAndRescue => Capability.SearchAndRescue,

            // We haven't modelled technical repair or towing equipment yet :(
            _ => null
        };
    }

    // Checks whether an active unit at the scene provides this capability.
    private bool HasCapabilityOnScene(
        int incidentId,
        Capability requiredCapability)
    {
        foreach (var assignment in _dispatchService.GetAssignments())
        {
            // Only consider active assignments at this incident
            // where the unit has already arrived.
            if (assignment.IncidentId != incidentId ||
                assignment.EndedAt is not null ||
                assignment.ArrivedAt is null)
            {
                continue;
            }

            var unit = _rescueUnitService.GetUnitById(assignment.UnitId);

            if (unit is not null &&
                unit.Capabilities.Contains(requiredCapability))
            {
                return true;
            }
        }

        return false;
    }
}