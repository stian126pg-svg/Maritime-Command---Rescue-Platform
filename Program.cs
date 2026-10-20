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
- Interactive console — choose actions and enter incident/unit IDs. (Currently Ongoing. NEEDS CLEANUP)
- Core checks — verify reassignment, conclusion, and readiness rules, including rejected actions.
- Persistence — save incidents, units, assignments, and logs across restarts.
- Web API — expose our existing service methods through controllers and DTOs.
- Operator dashboard — display cases, fleet availability, assignments, and logs; connect action buttons.
- Operator login(?) — identify operators and protect access.
- V1(?) finish — check the complete workflow, tidy the interface, and finish README + Setup.

 */


// Create each service once, so its collections survive between menu choices.
var incidentService = new IncidentService();
var incidentGeneratorService = new IncidentGeneratorService(incidentService);
var rescueUnitService = new RescueUnitService();
var dispatchService = new DispatchService(incidentService, rescueUnitService);

// Temporary starting data so we have something to view.
// No dispatch, arrival or conclusion happens automatically.

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

// Settings for automatic incoming reports.
TimeSpan incidentInterval = TimeSpan.FromSeconds(30);
int maxActiveIncidents = 3;

bool automaticIncidentsEnabled = true;

DateTimeOffset nextIncidentAt =
    DateTimeOffset.UtcNow.Add(incidentInterval);

// Keep showing the menu until the operator chooses to exit.
bool running = true;

while (running)
{
    Console.WriteLine();
    Console.WriteLine("MARITIME COMMAND & RESCUE");
    Console.WriteLine("1. List incidents");
    Console.WriteLine("2. List rescue units");
    Console.WriteLine("3. Register incident");
    Console.WriteLine("4. Dispatch rescue unit");
    Console.WriteLine("5. List assignments");
    Console.WriteLine("6. Record unit arrival");
    Console.WriteLine("7. Add situation report");
    Console.WriteLine("8. View incident log");
    Console.WriteLine("9. Simulate incoming incident");
    Console.WriteLine($"10. Pause/Resume automatic incidents. " + $"[{(automaticIncidentsEnabled ? "Running" : "Paused")}]");
    Console.WriteLine("11. Conclude incident");
    Console.WriteLine("0. Exit");
    Console.Write("Choose an option: ");

    // Collect the menu choice while also checking for incoming reports.
    string typedChoice = "";
    string? choice = null;

    while (choice is null)
    {
        if (automaticIncidentsEnabled && DateTimeOffset.UtcNow >= nextIncidentAt)
        {
            int activeIncidentCount = 0;

            foreach (var incident in incidentService.GetIncidents())
            {
                if (incident.Status != IncidentStatus.Concluded)
                {
                    activeIncidentCount++;
                }
            }

            if (activeIncidentCount < maxActiveIncidents)
            {
                var incomingIncident = incidentGeneratorService.GenerateIncident();

                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("*** NEW INCIDENT RECEIVED ***");

                Console.WriteLine($"#{incomingIncident.Id} - {incomingIncident.Type}");

                Console.WriteLine(incomingIncident.Description);

                Console.WriteLine(
                    $"Position: ({incomingIncident.Latitude}; " +
                    $"{incomingIncident.Longitude})");
                
                // Restore the prompt and anything already typed.
                Console.Write("Choose an option: " + typedChoice);
            }

            // Schedule the next attempt, even when the queue is full.
            nextIncidentAt = DateTimeOffset.UtcNow.Add(incidentInterval);
        }

        if (Console.KeyAvailable)
        {
            // Read one key without automatically displaying it.
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                choice = typedChoice;
                Console.WriteLine();
            }

            else if (key.Key == ConsoleKey.Backspace)
            {
                if (typedChoice.Length > 0)
                {
                    typedChoice = typedChoice.Remove(typedChoice.Length - 1);

                    // Move back, erase the displayed character, move back again.
                    Console.Write("\b \b");
                }
            }

            else if (char.IsDigit(key.KeyChar))
            {
                typedChoice += key.KeyChar;
                Console.Write(key.KeyChar);
            }
        }

        // Avoid constantly checking at full processor speed.
        Thread.Sleep(50);
    }

    switch (choice.Trim())
    {
        case "1":
        {
            Console.WriteLine();
            Console.WriteLine("INCIDENTS");

            var incidents = incidentService.GetIncidents();

            if (incidents.Count == 0)
            {
                Console.WriteLine("No incidents registered.");
            }

            foreach (var incident in incidents)
            {
                Console.WriteLine(
                    $"#{incident.Id} - {incident.Type} " +
                    $"[{incident.Status}]: {incident.Description}");
            }

            break;
        }

        case "2":
        {
            Console.WriteLine();
            Console.WriteLine("RESCUE UNITS");

            var units = rescueUnitService.GetUnits();

            if (units.Count == 0)
            {
                Console.WriteLine("No rescue units registered.");
            }

            foreach (var unit in units)
            {
                Console.WriteLine(
                    $"#{unit.Id} - {unit.Name}: " +
                    $"{unit.Type} [{unit.Status}]");
            }

            break;
        }

        case "3":
        {
            Console.WriteLine();
            Console.WriteLine("REGISTER INCIDENT");

            // Display the categories the operator can choose from.
            var incidentTypes = Enum.GetValues<IncidentType>();

            for (int index = 0; index < incidentTypes.Length; index++)
                {
                    Console.WriteLine($"{index + 1}. {incidentTypes[index]}");
                }
            
            Console.Write("Select incident type: ");

            // Convert the entered text into a number and check the menu range.
            if (!int.TryParse(Console.ReadLine(), out int typeChoice) ||
                typeChoice < 1 ||
                typeChoice > incidentTypes.Length)
                {
                    Console.WriteLine("Please select one of the listed incident types.");
                    break;
                }
            
            // Menu numbers start at 1; array positions start at 0.
            IncidentType selectedType = incidentTypes[typeChoice - 1];

            Console.Write("Description: ");
            string description = Console.ReadLine() ?? "";

            Console.Write("Latitude (use your local decimal seperator): ");

            if (!double.TryParse(Console.ReadLine(), out double latitude))
                {
                    Console.WriteLine("Please enter a numerical latitude.");
                    break;
                }
            
            Console.Write("Longitude (use your local decimal seperator): ");
            
            if (!double.TryParse(Console.ReadLine(), out double longitude))
                {
                    Console.WriteLine("Please enter a numerical longitude.");
                    break;
                }

            try
            {
                // The service still enforces our registration values.
                var incident = incidentService.RegisterIncident(
                    selectedType,
                    description,
                    latitude,
                    longitude);

                Console.WriteLine(
                    $"Incident #{incident.Id} registered: " +
                    $"{incident.Type} [{incident.Status}].");
            }

            catch (ArgumentException error)
                {
                    Console.WriteLine(error.Message);
                }

            break;    
        }

        case "4":
        {
            Console.WriteLine();
            Console.WriteLine("DISPATCH RESCUE UNIT");

            Console.Write("Incident ID: ");

            if (!int.TryParse(Console.ReadLine(), out int incidentId))
            {
                Console.WriteLine("Please enter a numeric incident ID.");
                break;
            }

            Console.Write("Rescue unit ID: ");

            if (!int.TryParse(Console.ReadLine(), out int unitId))
            {
                Console.WriteLine("Please enter a numeric rescue unit ID.");
                break;
            }

            try
            {
                // The service checks existence, availability and assignment rules.
                var assignment = dispatchService.AssignUnit(incidentId, unitId);

                Console.WriteLine(
                    $"Dispatch #{assignment.Id}: unit #{assignment.UnitId} " +
                    $"assigned to incident #{assignment.IncidentId}.");
            }

            catch (ArgumentException error)
            {
                Console.WriteLine(error.Message);
            }
            catch (InvalidOperationException error)
            {
                Console.WriteLine(error.Message);
            }

            break;
        }

        case "5":
        {
            Console.WriteLine();
            Console.WriteLine("ASSIGNMENTS");

            var assignments = dispatchService.GetAssignments();

            if (assignments.Count == 0)
            {
                Console.WriteLine("No assignments recorded.");
            }

            foreach (var assignment in assignments)
            {
                // Work out which stage this assignment has reached.
                string progress;

                if (assignment.EndedAt is not null)
                {
                    progress = "Ended.";
                }

                else if (assignment.ArrivedAt is not null)
                {
                    progress = "On scene.";
                }

                else
                {
                    progress = "En route.";
                }

                Console.WriteLine($"Assignment #{assignment.Id}: " +
                    $"unit #{assignment.UnitId} → " +
                    $"incident #{assignment.IncidentId} [{progress}]");
            }

            break;
        }

        case "6":
        {
            Console.WriteLine();
            Console.WriteLine("RECORD ARRIVAL");

            Console.Write("Assignment ID: ");

            if (!int.TryParse(Console.ReadLine(), out int assignmentId))
                {
                    Console.WriteLine("Please enter a numeric assignment ID");
                    break;
                }

                try
                {   
                    // The service validates the assignment and records the arrival.
                    var assignment = dispatchService.RecordArrival(assignmentId);

                    Console.WriteLine($"Unit #{assignment.UnitId} arrived at " +
                    $"incident #{assignment.IncidentId} at " +
                    $"{assignment.ArrivedAt:HH:mm:ss} UTC.");
                }

                catch (ArgumentException error)
                {
                    Console.WriteLine(error.Message);
                }
                catch (InvalidOperationException error)
                {
                    Console.WriteLine(error.Message);
                }

            break;
        }

        case "7":
        {
            Console.WriteLine();
            Console.WriteLine("ADD SITUATION REPORT");

            Console.Write("Incident ID: ");

            if (!int.TryParse(Console.ReadLine(), out int incidentId))
                {
                    Console.WriteLine("Please enter a numeric incident ID.");
                    break;
                }

            Console.Write("Reporting unit ID: ");

            if (!int.TryParse(Console.ReadLine(), out int unitId))
                {
                    Console.WriteLine("Please enter a numeric unit ID.");
                    break;
                }

            Console.Write("Report message: ");
            string message = Console.ReadLine() ?? "";

            try
                {
                    // The service checks the message, case and active assignment.
                    dispatchService.AddSituationReport(
                        incidentId,
                        unitId,
                        message);

                    Console.WriteLine("Situation report recorded");
                }

                catch (ArgumentException error)
                {
                    Console.WriteLine(error.Message);
                }

                catch (InvalidOperationException error)
                {
                    Console.WriteLine(error.Message);
                }

            break;
        }

        case "8":
        {
            Console.WriteLine();
            Console.WriteLine("INCIDENT LOG");

            Console.Write("Incident ID: ");

            if (!int.TryParse(Console.ReadLine(), out int incidentId))
                {
                    Console.WriteLine("Please enter a numeric incident ID.");
                    break;
                }

            try
                {
                    // Retrieve only the entries belonging to this state.
                    var entries = dispatchService.GetIncidentLog(incidentId);

                    Console.WriteLine();
                    Console.WriteLine($"OPERATIONAL LOG - INCIDENT #{incidentId}");

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

                catch (ArgumentException error)
                {
                    Console.WriteLine(error.Message);
                }
            break;
        }

        case "9":
        {
            var incident = incidentGeneratorService.GenerateIncident();
            
            // A simple visual notification for the incoming report.
            Console.WriteLine();
            Console.WriteLine("*** NEW INCIDENT RECEIVED ***");

            Console.WriteLine($"#{incident.Id} - {incident.Type} [{incident.Status}]");

            Console.WriteLine(incident.Description);

            Console.WriteLine($"Position: ({incident.Latitude}; {incident.Longitude})");

            Console.WriteLine($"Reported: {incident.ReportedAt:yyyy-MM-dd HH:mm:ss} UTC");

            break;
        }

        case "10":
        {
            // Flip running to paused, or paused to running again.
            automaticIncidentsEnabled = !automaticIncidentsEnabled;

            // Resume with a fresh interval instead of an immediate report.
            nextIncidentAt = DateTimeOffset.UtcNow.Add(incidentInterval);

            if (automaticIncidentsEnabled)
                {
                    Console.WriteLine("Automatic incidents resumed.");
                }

                else
                {
                    Console.WriteLine("Automatic incidents paused.");
                }

            break;
        }

        case "11":
        {
            Console.WriteLine();
            Console.WriteLine("CONCLUDE INCIDENT");

            Console.Write("Incident ID: ");

            if (!int.TryParse(Console.ReadLine(), out int incidentId))
            {
                Console.WriteLine("Please enter a numeric incident ID.");
                break;
            }

            // Show the case before asking the operator to conclude it.
            var incident = incidentService.GetIncidentById(incidentId);

            if (incident is null)
            {
                Console.WriteLine("Incident not found.");
                break;
            }

            if (incident.Status == IncidentStatus.Concluded)
            {
                Console.WriteLine("This incident is already concluded.");
                break;
            }

            Console.WriteLine($"#{incident.Id} — {incident.Type}: {incident.Description}");

            Console.Write("Conclusion: ");
            string conclusion = Console.ReadLine() ?? "";

            Console.WriteLine(
                "Confirm assistance is complete and necessary handovers " +
                "to other emergency services are finished.");

            Console.WriteLine("For an investigated false alarm, confirm no assistance is required.");

            Console.Write("Type YES to confirm, or anything else to cancel: ");

            string confirmation = Console.ReadLine() ?? "";

            bool responseAndHandoversComplete = string.Equals(
                confirmation.Trim(),
                "YES",
                StringComparison.OrdinalIgnoreCase);

            if (!responseAndHandoversComplete)
            {
                Console.WriteLine("Conclusion cancelled. Incident unchanged.");
                break;
            }

            try
            {
                // The service records the conclusion and ends active assignments.
                var concludedIncident = dispatchService.ConcludeIncident(
                    incidentId,
                    conclusion,
                    responseAndHandoversComplete);

                Console.WriteLine(
                    $"Incident #{concludedIncident.Id} concluded at " +
                    $"{concludedIncident.ConcludedAt:yyyy-MM-dd HH:mm:ss} UTC.");

                Console.WriteLine(
                    "Active assignments ended. Released units are Returning.");
            }
            catch (ArgumentException error)
            {
                Console.WriteLine(error.Message);
            }
            catch (InvalidOperationException error)
            {
                Console.WriteLine(error.Message);
            }

            break;
        }

        case "0":
        {
            running = false;
            Console.WriteLine("Command console closed.");
            break;
        }

        default:
        {
            Console.WriteLine("Please select one of the menu options.");
            break;
        }
    }
}