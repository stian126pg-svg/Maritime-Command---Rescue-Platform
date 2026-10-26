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
- Interactive console — choose actions and see overview of incidents/units, conclude cases/incidents and more. (Currently Ongoing. NEEDS CLEANUP)
- Core checks — verify reassignment, conclusion, and readiness rules, including rejected actions.
- Persistence — save incidents, units, assignments, and logs across restarts.
- Web API — expose our existing service methods through controllers and DTOs.
- Operator dashboard — display cases, fleet availability, assignments, and logs; connect action buttons.
- Operator login(?) — identify operators and protect access.
- V1(?) finish — check the complete workflow, tidy the interface, and finish README + Setup.

 */


// Create each service once.
// All menu actions use these same instances and their stored collections.
var incidentService = new IncidentService();

var incidentGeneratorService = new IncidentGeneratorService(incidentService);

var rescueUnitService = new RescueUnitService();

var dispatchService = new DispatchService(incidentService, rescueUnitService);

var unitTravelSimulationService = new UnitTravelSimulationService(incidentService, rescueUnitService, dispatchService);

var unitReportSimulationService = new UnitReportSimulationService(incidentService, rescueUnitService, dispatchService);

// Temporary starting fleet / example loadouts.
// No dispatch, arrival or conclusion happens automatically.

rescueUnitService.RegisterUnit(
    "Patrol One",
    UnitType.SmallPatrolBoat,
    59.03,
    9.72,
    crewCount: 3,
    capabilities: new List<Capability>
    {
        Capability.Reconnaissance,
        Capability.SearchAndRescue,
        Capability.Transport
    });
    
rescueUnitService.RegisterUnit(
    "Rescue Helicopter One",
    UnitType.Helicopter,
    59.10,
    9.80,
    crewCount: 4,
    capabilities: new List<Capability>
    {
        Capability.Reconnaissance,
        Capability.SearchAndRescue,
        Capability.Medical,
        Capability.Transport
    });

// A specialist vessel that can provide firefighting support.
rescueUnitService.RegisterUnit(
    "Rescue Vessel One",
    UnitType.EmergencyVessel,
    59.05,
    9.75,
    crewCount: 6,
    capabilities: new List<Capability>
    {
        Capability.Firefighting,
        Capability.SearchAndRescue,
        Capability.Medical,
        Capability.Transport
    });

// Checks for a possible incoming report every ten seconds.
TimeSpan incidentInterval = TimeSpan.FromSeconds(10);
int maxActiveIncidents = 3;

// Percentages: start at 10%, increases after quiet checks, stops at 60%.
int initialIncidentChance = 10;
int incidentChanceIncrease = 5;
int maximumIncidentChance = 60;

int currentIncidentChance = initialIncidentChance;

bool automaticIncidentsEnabled = true;

DateTimeOffset nextIncidentAt = DateTimeOffset.UtcNow.Add(incidentInterval);

// Measure elapsed time independently of changes to the computer's clock.
var simulationClock = System.Diagnostics.Stopwatch.StartNew();

TimeSpan previousSimulationCheck = simulationClock.Elapsed;

// Keep showing the menu until the operator chooses to exit.
bool running = true;

while (running)
{
    Console.WriteLine();
    Console.WriteLine("MARITIME COMMAND & RESCUE");
    Console.WriteLine("1. Incident board");
    Console.WriteLine("2. Fleet overview");
    Console.WriteLine("3. Control module");
    Console.WriteLine("4. Case review and reports");
    Console.WriteLine("5. Case history");

    Console.WriteLine(
        $"6. Pause/resume " +
        $"[{(automaticIncidentsEnabled ? "Running" : "Paused")}]");

    Console.WriteLine("0. Exit");

    string choice = ReadOperatorInput(
        "Choose an option: ",
        digitsOnly: true);

    // Handle service errors in one place for all menu actions.
    try
    {
        switch (choice.Trim())
        {
            case "1":
                ShowIncidentBoard();
                break;

            case "2":
                ShowFleetOverview();
                break;

            case "3":
                OpenControlModule();
                break;

            case "4":
                ReviewCase();
                break;

            case "5":
                ShowCaseHistory();
                break;

            case "6":
                automaticIncidentsEnabled = !automaticIncidentsEnabled;
                previousSimulationCheck = simulationClock.Elapsed;

                nextIncidentAt =
                    DateTimeOffset.UtcNow.Add(incidentInterval);

                Console.WriteLine(
                    automaticIncidentsEnabled
                        ? "Automatic incidents resumed."
                        : "Automatic incidents paused.");

                break;

            case "0":
                running = false;
                Console.WriteLine("Command console closed.");
                break;

            default:
                Console.WriteLine("Please select a listed option.");
                break;
        }
    }
    catch (ArgumentException error)
    {
        Console.WriteLine(error.Message);
    }
    catch (InvalidOperationException error)
    {
        Console.WriteLine(error.Message);
    }
}

// Local functions below run only when called.
// They reuse the service instances and settings declared above.

// Checks whether an automatic incident is due.
// Returns true when a notification was printed.
bool UpdateSimulation()
{
    TimeSpan now = simulationClock.Elapsed;
    TimeSpan elapsed = now - previousSimulationCheck;

    previousSimulationCheck = now;

    // Keep measuring time while paused, but don't advance the simulation.
    if (!automaticIncidentsEnabled)
    {
        return false;
    }

    bool notificationPrinted = false;

    // Travel updates run independently of the incident-generation interval.
    var travelNotifications = unitTravelSimulationService.Update(elapsed);

    foreach (var notification in travelNotifications)
    {
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("*** UNIT UPDATE ***");
        Console.WriteLine(notification);

        notificationPrinted = true;
    }

    // On-Scene assessments update independently of incident generation.
    var reportNotifications = unitReportSimulationService.Update(elapsed);

    foreach (var notification in reportNotifications)
    {
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("*** SITUATION REPORT RECEIVED ***");
        Console.WriteLine(notification);

        notificationPrinted = true;
    }
    
    if (DateTimeOffset.UtcNow < nextIncidentAt)
    {
        return notificationPrinted;
    }

    // Schedule the next check. Also don't generate a backlog of missed checks.
    nextIncidentAt = DateTimeOffset.UtcNow.Add(incidentInterval);

    int activeIncidentCount = 0;

    foreach (var incident in incidentService.GetIncidents())
    {
        if (incident.Status != IncidentStatus.Concluded)
        {
            activeIncidentCount++;
        }
    }

    // At capacity, hold the current chance without rolling or increasing it.
    if (activeIncidentCount >= maxActiveIncidents)
    {
        return notificationPrinted;
    }

    // Next (100) produces a whole number from 0 through 99.
    int roll = Random.Shared.Next(100);

    if (roll >= currentIncidentChance)
    {
        // Quiet check: increase the chance without exceeding the maximum.
        currentIncidentChance = Math.Min(currentIncidentChance + incidentChanceIncrease, maximumIncidentChance);

        return notificationPrinted;
    }

    // Successful Roll: register the report through our existing service.
    var incidentReport = incidentGeneratorService.GenerateIncident();

    currentIncidentChance = initialIncidentChance;

    Console.WriteLine();
    Console.WriteLine();    
    Console.WriteLine("*** NEW INCIDENT RECEIVED ***");

    Console.WriteLine($"#{incidentReport.Id} - {incidentReport.Type}");

    Console.WriteLine(incidentReport.Description);

    Console.WriteLine($"Position: ({incidentReport.Latitude}; " + $"{incidentReport.Longitude})");

    return true;
}


// Reads a line while allowing the simulation to keep updating.
// Requires an interactive terminal, such as your PowerShell terminal.
string ReadOperatorInput(string prompt, bool digitsOnly = false)
{
    string typedText = "";

    Console.Write(prompt);

    while (true)
    {
        if (UpdateSimulation())
        {
            // Restore the prompt and anything entered before the notification.
            Console.Write(prompt + typedText);
        }

        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return typedText;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typedText.Length > 0)
                {
                    typedText = typedText.Remove(typedText.Length - 1);

                    // Move back, erase the character, then move back again.
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar) &&
                     (!digitsOnly || char.IsDigit(key.KeyChar)))
            {
                typedText += key.KeyChar;
                Console.Write(key.KeyChar);
            }
        }

        // A short wait prevents the polling loop from consuming a full CPU core.
        Thread.Sleep(50);
    }
}

// Reads a positive ID. Zero cancels; invalid input returns no ID.
int? ReadId(string prompt)
{
    string input = ReadOperatorInput(prompt);

    if (input.Trim() == "0")
    {
        return null;
    }

    if (!int.TryParse(input, out int id) || id < 1)
    {
        Console.WriteLine("Please enter a positive numeric ID.");
        return null;
    }

    return id;
}


// Shows cases that still need attention.
void ShowIncidentBoard()
{
    Console.WriteLine();
    Console.WriteLine("INCIDENT BOARD");

    int activeCount = 0;

    foreach (var incident in incidentService.GetIncidents())
    {
        if (incident.Status == IncidentStatus.Concluded)
        {
            continue;
        }

        activeCount++;

        Console.WriteLine();
        Console.WriteLine(
            $"#{incident.Id} — {incident.Type} [{incident.Status}]");

        Console.WriteLine(incident.Description);

        if (incident.BackupRequested)
        {
            Console.WriteLine("*** BACKUP REQUEST OUTSTANDING ***");
        }

        Console.WriteLine(
            $"Position: ({incident.Latitude}; {incident.Longitude})");

        int assignedCount = 0;

        foreach (var assignment in dispatchService.GetAssignments())
        {
            if (assignment.IncidentId == incident.Id &&
                assignment.EndedAt is null)
            {
                assignedCount++;

                var unit = rescueUnitService.GetUnitById(
                    assignment.UnitId);

                string unitName = unit?.Name ?? "Unknown unit";

                string progress = assignment.ArrivedAt is null
                    ? "En route"
                    : "On scene";

                Console.WriteLine(
                    $"  Assignment #{assignment.Id}: " +
                    $"{unitName} [{progress}]");
            }
        }

        if (assignedCount == 0)
        {
            Console.WriteLine("No units assigned.");
        }
    }

    if (activeCount == 0)
    {
        Console.WriteLine("No active incidents.");
    }
}


// Shows the whole fleet, including units currently occupied.
void ShowFleetOverview()
{
    Console.WriteLine();
    Console.WriteLine("FLEET OVERVIEW");

    foreach (var unit in rescueUnitService.GetUnits())
    {
        Console.WriteLine();
        Console.WriteLine(
            $"#{unit.Id} — {unit.Name}: {unit.Type} [{unit.Status}]");

        Console.WriteLine(
            $"Position: ({unit.Latitude}; {unit.Longitude})");

        Console.WriteLine($"Crew/operators: {unit.CrewCount}");

        string capabilitySummary = unit.Capabilities.Count == 0 
            ? "None recorded"
            : string.Join(", ", unit.Capabilities);

        Console.WriteLine($"Capabilities: {capabilitySummary}");

        foreach (var assignment in dispatchService.GetAssignments())
        {
            if (assignment.UnitId == unit.Id &&
                assignment.EndedAt is null)
            {
                string progress = assignment.ArrivedAt is null
                    ? "En route"
                    : "On scene";

                Console.WriteLine(
                    $"Current task: incident #{assignment.IncidentId} " +
                    $"[{progress}]");
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine("1. Assess unit readiness");
    Console.WriteLine("0. Back");

    string choice = ReadOperatorInput("Choose: ", digitsOnly: true);

    if (choice == "0")
    {
        return;
    }

    if (choice != "1")
    {
        Console.WriteLine("Please select a listed option.");
        return;
    }

    int? unitId = ReadId("Unit ID (0 to cancel): ");

    if (unitId is null)
    {
        return;
    }

    var selectedUnit = rescueUnitService.GetUnitById(unitId.Value);

    if (selectedUnit is null)
    {
        Console.WriteLine("Rescue unit not found.");
        return;
    }

    Console.WriteLine(
        $"{selectedUnit.Name} [{selectedUnit.Status}]");

    Console.WriteLine(
        "Consider fuel, equipment, crew readiness and required debrief.");

    Console.WriteLine("1. Ready — Available");
    Console.WriteLine("2. Not ready — Unavailable");
    Console.WriteLine("0. Cancel");

    string assessment = ReadOperatorInput(
        "Assessment: ",
        digitsOnly: true);

    if (assessment == "0")
    {
        return;
    }

    if (assessment != "1" && assessment != "2")
    {
        Console.WriteLine("Please select a listed option.");
        return;
    }

    var assessedUnit = dispatchService.AssessUnitReadiness(
        unitId.Value,
        readyForDispatch: assessment == "1");

    Console.WriteLine(
        $"{assessedUnit.Name} is now {assessedUnit.Status}.");
}


// Coordinates new dispatches and direct transfers between cases.
void OpenControlModule()
{
    Console.WriteLine();
    Console.WriteLine("CONTROL MODULE");
    Console.WriteLine("1. Dispatch available unit");
    Console.WriteLine("2. Reassign unit to another incident");
    Console.WriteLine("3. Resolve backup request");
    Console.WriteLine("0. Back");

    string action = ReadOperatorInput("Choose: ", digitsOnly: true);

    if (action == "3")
    {
        int? caseId = ReadId("Incident ID (0 to cancel): ");

        if (caseId is null)
        {
            return;
        }

        string resolution = ReadOperatorInput(
            "How was the request resolved? ");

        string confirmation = ReadOperatorInput(
            "Type YES to confirm the backup need has been addressed: ");

        if (!string.Equals(
            confirmation.Trim(),
            "YES",
            StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Resolution cancelled.");
            return;
        }

        dispatchService.ResolveBackupRequest(
            caseId.Value,
            resolution);

        Console.WriteLine("Backup request resolved and logged.");
        return;
    }

    if (action == "0")
    {
        return;
    }

    if (action != "1" && action != "2")
    {
        Console.WriteLine("Please select a listed option.");
        return;
    }

    int? incidentId = ReadId("Target incident ID (0 to cancel): ");

    if (incidentId is null)
    {
        return;
    }

    int? unitId = ReadId("Unit ID (0 to cancel): ");

    if (unitId is null)
    {
        return;
    }

    Assignment assignment;

    if (action == "1")
    {
        assignment = dispatchService.AssignUnit(
            incidentId.Value,
            unitId.Value);
    }
    else
    {
        Console.WriteLine(
            "Confirm the unit can leave its current task " +
            "and is ready for the new response.");

        string confirmation = ReadOperatorInput(
            "Type YES to reassign: ");

        if (!string.Equals(
            confirmation.Trim(),
            "YES",
            StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Reassignment cancelled.");
            return;
        }

        assignment = dispatchService.ReassignUnit(
            unitId.Value,
            incidentId.Value,
            readinessConfirmed: true);
    }

    Console.WriteLine(
        $"Assignment #{assignment.Id}: " +
        $"unit #{assignment.UnitId} → incident #{assignment.IncidentId}.");
}


// Combines case information, reports and operational actions.
void ReviewCase()
{
    Console.WriteLine();
    Console.WriteLine("CASE REVIEW");

    int? incidentId = ReadId("Incident ID (0 to cancel): ");

    if (incidentId is null)
    {
        return;
    }

    var incident = incidentService.GetIncidentById(incidentId.Value);

    if (incident is null)
    {
        Console.WriteLine("Incident not found.");
        return;
    }

    Console.WriteLine(
        $"#{incident.Id} — {incident.Type} [{incident.Status}]");

    Console.WriteLine(incident.Description);

    if (incident.BackupRequested)
    {
        Console.WriteLine("*** BACKUP REQUEST OUTSTANDING ***");
    }

    PrintIncidentLog(incident.Id);

    // Concluded cases remain readable, but cannot receive these actions.
    if (incident.Status == IncidentStatus.Concluded)
    {
        return;
    }

    Console.WriteLine();
    Console.WriteLine("1. Add situation report");
    Console.WriteLine("2. Conclude incident");
    Console.WriteLine("3. Record arrival — manual control");
    Console.WriteLine("0. Back");

    string action = ReadOperatorInput("Choose: ", digitsOnly: true);

    switch (action)
    {
        case "1":
        {
            int? unitId = ReadId("Reporting unit ID (0 to cancel): ");

            if (unitId is null)
            {
                return;
            }

            string message = ReadOperatorInput("Report: ");

            dispatchService.AddSituationReport(
                incident.Id,
                unitId.Value,
                message);

            Console.WriteLine("Situation report recorded.");
            break;
        }

        case "2":
        {
            string conclusion = ReadOperatorInput("Conclusion: ");

            Console.WriteLine(
                "Confirm assistance and necessary handovers are complete.");

            Console.WriteLine(
                "For an investigated false alarm, " +
                "confirm no assistance is required.");

            string confirmation = ReadOperatorInput(
                "Type YES to conclude: ");

            if (!string.Equals(
                confirmation.Trim(),
                "YES",
                StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Conclusion cancelled.");
                return;
            }

            var concludedIncident = dispatchService.ConcludeIncident(
                incident.Id,
                conclusion,
                responseAndHandoversComplete: true);

            Console.WriteLine(
                $"Case #{concludedIncident.Id} concluded. " +
                "Released units are Returning.");

            break;
        }

        case "3":
        {
            // Temporary manual control until simulated travel is added.
            Console.WriteLine("ACTIVE ASSIGNMENTS FOR THIS CASE");

            foreach (var assignment in dispatchService.GetAssignments())
            {
                if (assignment.IncidentId == incident.Id &&
                    assignment.EndedAt is null)
                {
                    Console.WriteLine(
                        $"Assignment #{assignment.Id}: " +
                        $"unit #{assignment.UnitId}");
                }
            }

            int? assignmentId = ReadId(
                "Assignment ID (0 to cancel): ");

            if (assignmentId is null)
            {
                return;
            }

            // Prevent this case screen from updating another case.
            bool belongsToCase = false;

            foreach (var assignment in dispatchService.GetAssignments())
            {
                if (assignment.Id == assignmentId.Value &&
                    assignment.IncidentId == incident.Id)
                {
                    belongsToCase = true;
                    break;
                }
            }

            if (!belongsToCase)
            {
                Console.WriteLine(
                    "That assignment does not belong to this case.");

                return;
            }

            var arrivedAssignment = dispatchService.RecordArrival(
                assignmentId.Value);

            Console.WriteLine(
                $"Unit #{arrivedAssignment.UnitId} arrived at " +
                $"{arrivedAssignment.ArrivedAt:HH:mm:ss} UTC.");

            break;
        }

        case "0":
            return;

        default:
            Console.WriteLine("Please select a listed option.");
            break;
    }
}


// Lists handled cases and allows their reports to be read.
void ShowCaseHistory()
{
    Console.WriteLine();
    Console.WriteLine("CASE HISTORY");

    int concludedCount = 0;

    foreach (var incident in incidentService.GetIncidents())
    {
        if (incident.Status == IncidentStatus.Concluded)
        {
            concludedCount++;

            Console.WriteLine(
                $"#{incident.Id} — {incident.Type}: " +
                $"{incident.Description} " +
                $"[Concluded {incident.ConcludedAt:yyyy-MM-dd HH:mm:ss} UTC]");
        }
    }

    if (concludedCount == 0)
    {
        Console.WriteLine("No concluded cases.");
        return;
    }

    int? incidentId = ReadId(
        "Case ID to view reports (0 to return): ");

    if (incidentId is null)
    {
        return;
    }

    var selectedIncident = incidentService.GetIncidentById(
        incidentId.Value);

    if (selectedIncident is null ||
        selectedIncident.Status != IncidentStatus.Concluded)
    {
        Console.WriteLine("Please select a concluded case.");
        return;
    }

    PrintIncidentLog(selectedIncident.Id);
}


// Shared by active case review and concluded case history.
void PrintIncidentLog(int incidentId)
{
    var entries = dispatchService.GetIncidentLog(incidentId);

    Console.WriteLine();
    Console.WriteLine($"OPERATIONAL LOG — INCIDENT #{incidentId}");

    if (entries.Count == 0)
    {
        Console.WriteLine("No operational log entries recorded.");
    }

    foreach (var entry in entries)
    {
        Console.WriteLine(
            $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss} UTC] " +
            $"{entry.Message} " +
            $"Position: ({entry.Latitude}; {entry.Longitude})");
    }
}