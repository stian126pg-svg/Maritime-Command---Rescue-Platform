using MaritimeCommand.Models;
using MaritimeCommand.Services;


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
    STOP;*/

/*string description = "Smoke?";

if (string.IsNullOrWhiteSpace(description))
    {
        Console.WriteLine("Please provide an incident description");
        return;
    }


double latitude = 90;

if (latitude < -90 || latitude > 90)
{
    Console.WriteLine("Latitude is out of range.");
    return;
}


double longitude = 180;

if (longitude < -180 || longitude > 180)
{
    Console.WriteLine("Longitude is out of range.");
    return;
}

Console.WriteLine("Validation passed.");


var registeredIncident = new Incident
{
    Type = IncidentType.Fire,
    Status = IncidentStatus.Reported,
    Description = description,
    Latitude = latitude,
    Longitude = longitude,
    ReportedAt = DateTimeOffset.UtcNow
};
*/

// Create the service that knows our registration rules.
var incidentService = new IncidentService();

// And declared here, so the list-building code below can use it.
Incident registeredIncident;

try
{
    registeredIncident = incidentService.RegisterIncident(
        IncidentType.Fire,
        "Smoke?",
        90,
        180);

    Console.WriteLine("Validation passed.");
}

catch (ArgumentException error)
{
    Console.WriteLine(error.Message);
    return;
}




/*
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
    Status = IncidentStatus.Concluded,
    Description = "Smoke reported aboard a fishing vessel.",
    Latitude = 59.03,
    Longitude = 9.72,
    ReportedAt = DateTimeOffset.UtcNow
};


var secondIncident = new Incident
{
    Type = IncidentType.MedicalEmergency,
    Status = IncidentStatus.Reported,
    Description = "Medical Emergency aboard a civilian vessel.",
    Latitude = 31.89,
    Longitude = 99.01,
    ReportedAt = DateTimeOffset.UtcNow
};

var incidents = new List<Incident>();
incidents.Add(incident);
incidents.Add(secondIncident);
incidents.Add(registeredIncident);


foreach (var currentIncident in incidents)
{   
    Console.WriteLine($"{currentIncident.Type} [{currentIncident.Status}]: {currentIncident.Description}");
}


/*Console.WriteLine(incident.Type);
Console.WriteLine(incident.Status);
Console.WriteLine(incident.Description);
Console.WriteLine(incident.ReportedAt);
Console.WriteLine(secondIncident.Type);
Console.WriteLine(secondIncident.Status);
Console.WriteLine(secondIncident.Description);
Console.WriteLine(secondIncident.ReportedAt);*/