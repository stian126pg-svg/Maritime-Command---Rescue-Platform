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


TO DO:
- Interactive console — choose actions and enter incident/unit IDs.
- Core checks — verify reassignment, conclusion, and readiness rules, including rejected actions.
- Persistence — save incidents, units, assignments, and logs across restarts.
- Web API — expose our existing service methods through controllers and DTOs.
- Operator dashboard — display cases, fleet availability, assignments, and logs; connect action buttons.
- Operator login(?) — identify operators and protect access.
- V1(?) finish — check the complete workflow, tidy the interface, and finish README + Setup.

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

// Connect dispatch to the services we've already populated.
var dispatchService = new DispatchService(incidentService, rescueUnitService);

try
{
    // Send unit #1 to incident #1
    var assignment = dispatchService.AssignUnit(1, 1);

    Console.WriteLine();
    Console.WriteLine($"Dispatch #{assignment.Id}: unit #{assignment.UnitId} " + $"assigned to incident #{assignment.IncidentId}");

    // Look up the objects again to see their updated states.
    var dispatchedIncident = incidentService.GetIncidentById(assignment.IncidentId);

    var dispatchedUnit = rescueUnitService.GetUnitById(assignment.UnitId);

    if (dispatchedIncident is not null && dispatchedUnit is not null)
    {
        Console.WriteLine($"Incident status: {dispatchedIncident.Status}");
        Console.WriteLine($"Unit status: {dispatchedUnit.Status}");
    }

    // Record Arrival for the assignment we just created.
    var arrivedAssignment = dispatchService.RecordArrival(assignment.Id);

    Console.WriteLine($"Unit #{arrivedAssignment.UnitId} arrived at incident " + 
    $"#{arrivedAssignment.IncidentId} at " + $"{arrivedAssignment.ArrivedAt:HH:mm:ss} UTC.");

    // Record a situation update before displaying the incident's log.
    dispatchService.AddSituationReport(
    assignment.IncidentId,
    assignment.UnitId,
    "Fire confirmed aboard vessel. Requesting firefighting backup.");

    // Demonstration: the response has finished and handover is complete.
    var concludedIncident = dispatchService.ConcludeIncident(
        assignment.IncidentId, 
        "Fire extinguished. Personnel handed over to shore emergency services.", 
        responseAndHandoversComplete: true);

    Console.WriteLine($"Final incident status: {concludedIncident.Status}");

    var returningUnit = rescueUnitService.GetUnitById(assignment.UnitId);

    if (returningUnit is not null)
    {
        Console.WriteLine($"Unit status after conclusion: {returningUnit.Status}");
    }

    Console.WriteLine($"Assignment ended: {assignment.EndedAt is not null}");

    Console.WriteLine();
    Console.WriteLine($"OPERATIONAL LOG — INCIDENT #{assignment.IncidentId}");

    foreach (var entry in dispatchService.GetIncidentLog(assignment.IncidentId))
    {
        Console.WriteLine(
            $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss} UTC] " +
            $"{entry.Message} " +
            $"Position: {entry.Latitude}, {entry.Longitude}");
    }

    // Demonstration: the unit still needs servicing.
    var assessedUnit = dispatchService.AssessUnitReadiness(assignment.UnitId, readyForDispatch: false);

    Console.WriteLine($"Readiness assessment: {assessedUnit.Status}");

    // Demonstration: servicing is now complete and readiness is confirmed.
    assessedUnit = dispatchService.AssessUnitReadiness(assignment.UnitId, readyForDispatch: true);

    Console.WriteLine($"After servicing: {assessedUnit.Status}");
}

catch (ArgumentException error)
{
    Console.WriteLine(error.Message);
}
catch (InvalidOperationException error)
{
    Console.WriteLine(error.Message);
}

try
{
    // Create another case for our reassignment demonstration.
    var searchIncident = incidentService.RegisterIncident(
        IncidentType.SearchAndRescue,
        "Person reported missing near the harbour.",
        59.05,
        9.75);

    // Send the helicopter to the existing medical emergency incident first.
    var originalAssignment = dispatchService.AssignUnit(
        anotherRegisteredIncident.Id,
        2);

    // The operator confirms it can be redirected to the search.
    var redirectedAssignment = dispatchService.ReassignUnit(
        originalAssignment.UnitId,
        searchIncident.Id,
        readinessConfirmed: true);

    Console.WriteLine();
    Console.WriteLine(
        $"Previous assignment ended: " + 
        $"{originalAssignment.EndedAt is not null}");

    Console.WriteLine($"Previous incident status: {anotherRegisteredIncident.Status}");

    Console.WriteLine(
        $"New assignment #{redirectedAssignment.Id} " + 
        $"unit #{redirectedAssignment.UnitId} to " + 
        $"incident #{redirectedAssignment.IncidentId}");

    Console.WriteLine($"New incident status: {searchIncident.Status}");
}

catch (ArgumentException error)
{
    Console.WriteLine(error.Message);
}

catch (InvalidOperationException error)
{
    Console.WriteLine(error.Message);
}