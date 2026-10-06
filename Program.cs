// A maritime emergency coordination system that: 
// 1. Registers incidents
// 2. Tracks rescue units and their availability
// 3. Assigns suitable units to emergencies
// 4. Records a simple chronological operational log until the incident is concluded.



/* Maritime Command & Rescue Platform

Incident
 ├── ID (case number)
 ├── Type
 ├── Description
 ├── Coordinates
 ├── ReportedAt
 ├── Status
 ├── Assigned Rescue Units
 └── Operational Log Entries


Rescue Unit
 ├── Name
 ├── Type
 ├── Status
 └── Coordinates


Operational Log Entry
 ├── Incident ID
 ├── Timestamp
 ├── Coordinates
 ├── Rescue Unit
 └── Message
 */





/*REGISTER INCIDENT


Receive type, description, latitude and longitude

IF(incident type is not one of our five accepted categories) 
    Reject with "Select supported incident type"
    STOP;

IF(description missing, empty or contains only spaces)
    Reject with "Please provide an incident description"
    STOP;

IF(latitude is outside -90 to 90)
    Reject with "Latitude is out of range"
    STOP;

IF(longitude is outside -180 to 180)
    Reject with "Longitude is out of range"
    STOP;

Create incident with:
    [Id]: (Generate unique ID here)
    [ReportedAt]: (Record the current timestamp here)
    [Status]: (Reported)
    [type/Type]: (Example: Fire. Search and Rescue. Medical Emergency.)
    [Description]: (Example: "Smoke/Fire reported at Vessel.)
	[Location]: (Example: Latitude 56. Longitude 112)

Store incident
Return incident
*/



var incident = new Incident
{
    Type = IncidentType.Fire,
    Status = IncidentStatus.Reported,
    Description = "Smoke reported aboard a fishing vessel.",
    Latitude = 59.03,
    Longitude = 9.72,
    ReportedAt = DateTimeOffset.UtcNow
};


var secondIncident = new Incident
{
    Type = IncidentType.MedicalEmergency,
    Status = IncidentStatus.Concluded,
    Description = "Medical Emergency aboard a civilian vessel.",
    Latitude = 31.89,
    Longitude = 99.01,
    ReportedAt = DateTimeOffset.UtcNow
};

var incidents = new List<Incident>();
incidents.Add(incident);
incidents.Add(secondIncident);


foreach (var currentIncident in incidents)
{
    if (currentIncident.Status != IncidentStatus.Concluded)
    {
        Console.WriteLine($"{currentIncident.Type} [{currentIncident.Status}]: {currentIncident.Description}");
    }
}


/*Console.WriteLine(incident.Type);
Console.WriteLine(incident.Status);
Console.WriteLine(incident.Description);
Console.WriteLine(incident.ReportedAt);
Console.WriteLine(secondIncident.Type);
Console.WriteLine(secondIncident.Status);
Console.WriteLine(secondIncident.Description);
Console.WriteLine(secondIncident.ReportedAt);*/

public enum IncidentType
{
    Fire,
    MedicalEmergency,
    TechnicalFailure,
    EmergencyEvacuation,
    SearchAndRescue
}

public enum IncidentStatus
{
    Reported,
    Ongoing,
    Concluded
}



public class Incident
{
    public int Id { get; set; }
    public IncidentType Type { get; set; }
    public string Description { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
    public IncidentStatus Status { get; set; }
}

