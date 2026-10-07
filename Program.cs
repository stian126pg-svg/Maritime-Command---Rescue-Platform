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


// Create the service that knows our registration rules.
var incidentService = new IncidentService();

// Declared outside 'try' so we can use both incidents afterwards.
Incident registeredIncident;
Incident anotherRegisteredIncident;

try
{
    // Already assigned to the variable we declared above.
    // First registration uses ID 1.
    registeredIncident = incidentService.RegisterIncident(
        IncidentType.Fire,
        "Smoke?",
        90,
        180);

    // The same service remembers the counter and uses ID 2.
    anotherRegisteredIncident = incidentService.RegisterIncident(
        IncidentType.MedicalEmergency,
        "Passenger requires medical assistance.",
        59.03,
        9.72);

    Console.WriteLine("Validation passed.");

    Console.WriteLine($"Generated IDs: {registeredIncident.Id}, {anotherRegisteredIncident.Id}");
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

var incidents = new List<Incident>();

incidents.Add(registeredIncident);
incidents.Add(anotherRegisteredIncident);

foreach (var currentIncident in incidents)
{   
    Console.WriteLine(
        $"#{currentIncident.Id} - {currentIncident.Type} " +
        $"[{currentIncident.Status}]: {currentIncident.Description}");
}