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

// Ask the service for the incidents it has registered.
var incidents = incidentService.GetIncidents();

foreach (var currentIncident in incidents)
{   
    Console.WriteLine(
        $"#{currentIncident.Id} - {currentIncident.Type} " +
        $"[{currentIncident.Status}]: {currentIncident.Description}");
}

// Look up one particular case/incident once.
var selectedIncident = incidentService.GetIncidentById(2);

if (selectedIncident is null)
{
    Console.WriteLine("Incident not found");
}
else
{
    Console.WriteLine($"Selected case #{selectedIncident.Id}: " + selectedIncident.Description);
}

// One service instance holds our fleet and its ID counter.
var rescueUnitService = new RescueUnitService();

try
{
    rescueUnitService.RegisterUnit(
        "Patrol One",
        UnitType.SmallPatrolBoat,
        59.03,
        9.72);

    rescueUnitService.RegisterUnit(
        "Rescue Helicopter One",
        UnitType.Helicopter,
        59.10,
        9.80);
}

catch (ArgumentException error)
{
    Console.WriteLine(error.Message);
    return;
}

// Display the units stored by the service.
Console.WriteLine();
Console.WriteLine("RESCUE UNITS");

foreach (var unit in rescueUnitService.GetUnits())
{
    Console.WriteLine($"{unit.Id} - {unit.Name}: {unit.Type} [{unit.Status}]");
}

