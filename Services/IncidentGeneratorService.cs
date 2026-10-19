using MaritimeCommand.Models;

namespace MaritimeCommand.Services;

// Simulates incoming reports within a small operating area.
public class IncidentGeneratorService
{
    private readonly IncidentService _incidentService;

    public IncidentGeneratorService(IncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    public Incident GenerateIncident()
    {
        var types = Enum.GetValues<IncidentType>();

        // Random.Next picks an array position, excluding the upper limit.
        IncidentType type = types[Random.Shared.Next(types.Length)];

        // Match the reported description to the selected category.
        string description;

        switch (type)
        {
            case IncidentType.Fire:
                description =
                    "Smoke and flames reported aboard a fishing vessel.";
                break;

            case IncidentType.MedicalEmergency:
                description =
                    "Passenger aboard a civilian vessel requires medical assistance.";
                break;

            case IncidentType.TechnicalFailure:
                description =
                    "Vessel reports engine failure and is drifting.";
                break;

            case IncidentType.EmergencyEvacuation:
                description =
                    "Vessel taking on water. Crew requests emergency evacuation.";
                break;

            case IncidentType.SearchAndRescue:
                description =
                    "Person reported overboard. Search and rescue assistance requested.";
                break;

            default:
                throw new InvalidOperationException(
                    "No report template exists for this incident type.");
        }

        // Generate coordinates near our sample units.
        // Latitude: 59.00 up to 59.10.
        // Longitude: 9.65 up to 9.85.
        double latitude = Math.Round(
            59.00 + Random.Shared.NextDouble() * 0.10,
            4);

        double longitude = Math.Round(
            9.65 + Random.Shared.NextDouble() * 0.20,
            4);

        // Reuse registration rules, ID generation and storage.
        return _incidentService.RegisterIncident(
            type,
            description,
            latitude,
            longitude);
    }
}