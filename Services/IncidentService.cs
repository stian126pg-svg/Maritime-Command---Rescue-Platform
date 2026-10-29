using System.Reflection.Metadata.Ecma335;
using MaritimeCommand.Models;

namespace MaritimeCommand.Services;

// Handles the rules for registering incidents.
public class IncidentService
{
    // Holds registered incidents while this service instance exists.
    private readonly List<Incident> _incidents = new();
    
    // The next ID available for a successful registration.
    private int _nextIncidentId = 1;

    public Incident RegisterIncident(
        IncidentType type,
        string description,
        double latitude,
        double longitude)
    {
        // Reject the values that aren't one of our supported incident types.
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException("Please select a supported incident type.");
        }

        // A description must contain something besides just whitespace.
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Please provide an incident description.");
        }

        // Coordinates must be real, finite numbers within these limits.
        if (!double.IsFinite(latitude) || latitude < -90 || latitude > 90)
        {
            throw new ArgumentException("Latitude is out of range.");
        }

        if (!double.IsFinite(longitude) || longitude < -180 || longitude > 180)
        {
            throw new ArgumentException("Longitude is out of range.");
        }

        // Finally, reaching this point means every validation check has passed.
        // We then make a incident.
        var incident = new Incident
        {
            Id = _nextIncidentId++,
            Type = type,
            Status = IncidentStatus.Reported,
            Description = description.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            ReportedAt = DateTimeOffset.UtcNow
        };

        // Keep the successfully registered incident in our collection.
        _incidents.Add(incident);

        // Give the caller the incident we just registered.
        return incident;
    }
    
    // Provides a read-only view of the registered incident collection.
    public IReadOnlyList<Incident> GetIncidents()
    {
        return _incidents.AsReadOnly();
    }

    // Restores saved incidents and resumes numbering after the highest saved ID.
    public void RestoreIncidents(List<Incident> incidents)
    {
        ArgumentNullException.ThrowIfNull(incidents);

        // Copy the collection before checking or changing anything.
        var restoredIncidents = new List<Incident>(incidents);

        var seenIds = new HashSet<int>();
        int highestId = 0;

        foreach (var incident in restoredIncidents)
        {
            if (incident is null)
            {
                throw new InvalidDataException("The save contains a missing incident record.");
            }

            // IDs must be positive, unique and leave room for the next id.
            if (incident.Id < 1 || incident.Id == int.MaxValue || !seenIds.Add(incident.Id))
            {
                throw new InvalidDataException("The save contains an invalid or duplicate incident ID.");
            }

            if (!Enum.IsDefined(incident.Type) || !Enum.IsDefined(incident.Status))
            {
                throw new InvalidDataException($"Incident #{incident.Id} has an unsupported type or status.");
            }

            if (string.IsNullOrWhiteSpace(incident.Description))
            {
                throw new InvalidDataException($"Incident #{incident.Id} is missing its description.");
            }

            if (!double.IsFinite(incident.Latitude) ||
                incident.Latitude < -90 ||
                incident.Latitude > 90 ||
                !double.IsFinite(incident.Longitude) ||
                incident.Longitude < -180 ||
                incident.Longitude > 180)
            {
                throw new InvalidDataException(
                    $"Incident #{incident.Id} has invalid coordinates.");
            }

            if (incident.ReportedAt == default)
            {
                throw new InvalidDataException($"Incident #{incident.Id} is missing its reported timestamp.");
            }

            highestId = Math.Max(highestId, incident.Id);
        }

        // Only replace the current collection after every record passes.
        _incidents.Clear();
        _incidents.AddRange(restoredIncidents);

        _nextIncidentId = highestId + 1;
    }

    // Returns the matching incident, or null if the ID doesn't exist.
    public Incident? GetIncidentById(int id)
    {
        foreach (var incident in _incidents)
        {
            if (incident.Id == id)
            {
                return incident;
            }
        }
    
    // We checked every incident without finding a match.
    return null;
    }
}