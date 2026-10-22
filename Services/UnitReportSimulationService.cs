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

            string message = CreateAssessment(incident.Type);

            // Reuse validation and operational logging.
            _dispatchService.AddSituationReport(
                incident.Id,
                unit.Id,
                message);

            _reportedAssignments.Add(assignment.Id);
            _remainingAssessment.Remove(assignment.Id);

            notifications.Add($"{unit.Name}, incident #{incident.Id}: {message}");
        }

        return notifications;
    }

    private string CreateAssessment(IncidentType type)
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

        // Temporary probability until threat and capabilities are modelled.
        bool backupRequested = Random.Shared.Next(100) < 25;

        if (backupRequested)
        {
            return assessment +
                " Requesting an additional suitable unit to assist.";
        }

        return assessment +
            " No additional units requested at present.";
    }
}