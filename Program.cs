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


// Create each service once.
// All menu actions use these same instances and their stored collections.
var incidentService = new IncidentService();

var incidentGeneratorService = new IncidentGeneratorService(incidentService);

var rescueUnitService = new RescueUnitService();

var dispatchService = new DispatchService(incidentService, rescueUnitService);


// Temporary starting fleet.
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

DateTimeOffset nextIncidentAt = DateTimeOffset.UtcNow.Add(incidentInterval);


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

    Console.WriteLine(
        $"10. Pause/Resume automatic incidents. " +
        $"[{(automaticIncidentsEnabled ? "Running" : "Paused")}]");

    Console.WriteLine("11. Conclude incident");
    Console.WriteLine("12. Assess unit readiness");
    Console.WriteLine("0. Exit");

    // This function handles keyboard input and simulation updates.
    string choice = ReadOperatorInput(
        "Choose an option: ",
        digitsOnly: true);

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
                Console.WriteLine(
                    $"{index + 1}. {incidentTypes[index]}");
            }

            // Read text, then check whether it represents a valid choice.
            if (!int.TryParse(
                    ReadOperatorInput("Select incident type: "),
                    out int typeChoice) ||
                typeChoice < 1 ||
                typeChoice > incidentTypes.Length)
            {
                Console.WriteLine(
                    "Please select one of the listed incident types.");

                break;
            }

            // Menu numbers start at 1; array positions start at 0.
            IncidentType selectedType = incidentTypes[typeChoice - 1];

            string description = ReadOperatorInput("Description: ");

            if (!double.TryParse(
                ReadOperatorInput(
                    "Latitude (use your local decimal separator): "),
                out double latitude))
            {
                Console.WriteLine("Please enter a numerical latitude.");
                break;
            }

            if (!double.TryParse(
                ReadOperatorInput(
                    "Longitude (use your local decimal separator): "),
                out double longitude))
            {
                Console.WriteLine("Please enter a numerical longitude.");
                break;
            }

            try
            {
                // The service enforces our registration rules.
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

            if (!int.TryParse(
                ReadOperatorInput("Incident ID: "),
                out int incidentId))
            {
                Console.WriteLine("Please enter a numeric incident ID.");
                break;
            }

            if (!int.TryParse(
                ReadOperatorInput("Rescue unit ID: "),
                out int unitId))
            {
                Console.WriteLine("Please enter a numeric rescue unit ID.");
                break;
            }

            try
            {
                // The service checks existence, availability and assignments.
                var assignment = dispatchService.AssignUnit(
                    incidentId,
                    unitId);

                Console.WriteLine(
                    $"Dispatch #{assignment.Id}: " +
                    $"unit #{assignment.UnitId} assigned to " +
                    $"incident #{assignment.IncidentId}.");
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
                // Check Ended first because an ended assignment
                // may also have an arrival timestamp.
                string progress;

                if (assignment.EndedAt is not null)
                {
                    progress = "Ended";
                }
                else if (assignment.ArrivedAt is not null)
                {
                    progress = "On scene";
                }
                else
                {
                    progress = "En route";
                }

                Console.WriteLine(
                    $"Assignment #{assignment.Id}: " +
                    $"unit #{assignment.UnitId} → " +
                    $"incident #{assignment.IncidentId} [{progress}]");
            }

            break;
        }

        case "6":
        {
            Console.WriteLine();
            Console.WriteLine("RECORD ARRIVAL");

            if (!int.TryParse(
                ReadOperatorInput("Assignment ID: "),
                out int assignmentId))
            {
                Console.WriteLine("Please enter a numeric assignment ID.");
                break;
            }

            try
            {
                // The service validates the assignment and records arrival.
                var assignment =
                    dispatchService.RecordArrival(assignmentId);

                Console.WriteLine(
                    $"Unit #{assignment.UnitId} arrived at " +
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

            if (!int.TryParse(
                ReadOperatorInput("Incident ID: "),
                out int incidentId))
            {
                Console.WriteLine("Please enter a numeric incident ID.");
                break;
            }

            if (!int.TryParse(
                ReadOperatorInput("Reporting unit ID: "),
                out int unitId))
            {
                Console.WriteLine("Please enter a numeric unit ID.");
                break;
            }

            string message = ReadOperatorInput("Report message: ");

            try
            {
                // The service checks the message, case and active assignment.
                dispatchService.AddSituationReport(
                    incidentId,
                    unitId,
                    message);

                Console.WriteLine("Situation report recorded.");
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

            if (!int.TryParse(
                ReadOperatorInput("Incident ID: "),
                out int incidentId))
            {
                Console.WriteLine("Please enter a numeric incident ID.");
                break;
            }

            try
            {
                // Retrieve only the entries belonging to this case.
                var entries = dispatchService.GetIncidentLog(incidentId);

                Console.WriteLine();
                Console.WriteLine(
                    $"OPERATIONAL LOG - INCIDENT #{incidentId}");

                if (entries.Count == 0)
                {
                    Console.WriteLine(
                        "No operational log entries recorded.");
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
            // Manual generation intentionally bypasses the automatic cap.
            var incident = incidentGeneratorService.GenerateIncident();

            Console.WriteLine();
            Console.WriteLine("*** NEW INCIDENT RECEIVED ***");

            Console.WriteLine(
                $"#{incident.Id} - {incident.Type} [{incident.Status}]");

            Console.WriteLine(incident.Description);

            Console.WriteLine(
                $"Position: ({incident.Latitude}; {incident.Longitude})");

            Console.WriteLine(
                $"Reported: {incident.ReportedAt:yyyy-MM-dd HH:mm:ss} UTC");

            break;
        }

        case "10":
        {
            automaticIncidentsEnabled = !automaticIncidentsEnabled;

            // Resume with a fresh interval.
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

            if (!int.TryParse(
                ReadOperatorInput("Incident ID: "),
                out int incidentId))
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

            Console.WriteLine(
                $"#{incident.Id} — {incident.Type}: {incident.Description}");

            string conclusion = ReadOperatorInput("Conclusion: ");

            Console.WriteLine(
                "Confirm assistance is complete and necessary handovers " +
                "to other emergency services are finished.");

            Console.WriteLine(
                "For an investigated false alarm, " +
                "confirm no assistance is required.");

            string confirmation = ReadOperatorInput(
                "Type YES to confirm, or anything else to cancel: ");

            bool responseAndHandoversComplete = string.Equals(
                confirmation.Trim(),
                "YES",
                StringComparison.OrdinalIgnoreCase);

            if (!responseAndHandoversComplete)
            {
                Console.WriteLine(
                    "Conclusion cancelled. Incident unchanged.");

                break;
            }

            try
            {
                // The service concludes the case and ends active assignments.
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

        case "12":
        {
            Console.WriteLine();
            Console.WriteLine("ASSESS UNIT READINESS");

            if (!int.TryParse(
                ReadOperatorInput("Unit ID: "),
                out int unitId))
            {
                Console.WriteLine("Please enter a numeric unit ID.");
                break;
            }

            var unit = rescueUnitService.GetUnitById(unitId);

            if (unit is null)
            {
                Console.WriteLine("Unit not found.");
                break;
            }

            Console.WriteLine(
                $"#{unit.Id} - {unit.Name} [{unit.Status}]");

            Console.WriteLine(
                "Consider fuel, equipment, crew readiness " +
                "and any required debrief.");

            Console.WriteLine("1. Ready for dispatch - Available");
            Console.WriteLine("2. Not ready for dispatch - Unavailable");
            Console.WriteLine("0. Cancel");

            string assessment = ReadOperatorInput(
                "Select assessment: ").Trim();

            if (assessment == "0")
            {
                Console.WriteLine(
                    "Assessment cancelled. Unit unchanged.");

                break;
            }

            if (assessment != "1" && assessment != "2")
            {
                Console.WriteLine(
                    "Please select one of the listed options.");

                break;
            }

            bool readyForDispatch = assessment == "1";

            try
            {
                // Active assignments prevent readiness changes.
                var assessedUnit = dispatchService.AssessUnitReadiness(
                    unitId,
                    readyForDispatch);

                Console.WriteLine(
                    $"{assessedUnit.Name} is now {assessedUnit.Status}.");
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


// Local functions below run only when called.
// They reuse the service instances and settings declared above.

// Checks whether an automatic incident is due.
// Returns true when a notification was printed.
bool UpdateSimulation()
{
    if (!automaticIncidentsEnabled ||
        DateTimeOffset.UtcNow < nextIncidentAt)
    {
        return false;
    }

    // Schedule the next attempt even when the active-case limit is reached.
    nextIncidentAt = DateTimeOffset.UtcNow.Add(incidentInterval);

    int activeIncidentCount = 0;

    foreach (var incident in incidentService.GetIncidents())
    {
        if (incident.Status != IncidentStatus.Concluded)
        {
            activeIncidentCount++;
        }
    }

    if (activeIncidentCount >= maxActiveIncidents)
    {
        return false;
    }

    var incidentReport = incidentGeneratorService.GenerateIncident();

    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine("*** NEW INCIDENT RECEIVED ***");

    Console.WriteLine(
        $"#{incidentReport.Id} - {incidentReport.Type}");

    Console.WriteLine(incidentReport.Description);

    Console.WriteLine(
        $"Position: ({incidentReport.Latitude}; " +
        $"{incidentReport.Longitude})");

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