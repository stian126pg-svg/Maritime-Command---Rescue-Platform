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