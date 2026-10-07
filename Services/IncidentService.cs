using MaritimeCommand.Models;

namespace MaritimeCommand.Services;

// Handles the rules for registering incidents.
public class IncidentService
{
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
        return new Incident
        {
            Type = type,
            Status = IncidentStatus.Reported,
            Description = description.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            ReportedAt = DateTimeOffset.UtcNow
        };

    }
}