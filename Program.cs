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


// Create each service once, so its collections survive between menu choices.
var incidentService = new IncidentService();
var rescueUnitService = new RescueUnitService();
var dispatchService = new DispatchService(incidentService, rescueUnitService);

// Temporary starting data so we have something to view.
// No dispatch, arrival or conclusion happens automatically.
incidentService.RegisterIncident(
    IncidentType.Fire,
    "Smoke reported aboard a fishing vessel.",
    59.03,
    9.72);

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

// Keep showing the menu until the operator chooses to exit.
bool running = true;

while (running)
{
    Console.WriteLine();
    Console.WriteLine("MARITIME COMMAND & RESCUE");
    Console.WriteLine("1. List incidents");
    Console.WriteLine("2. List rescue units");
    Console.WriteLine("3. Register incident");
    Console.WriteLine("0. Exit");
    Console.Write("Choose an option: ");

    string? choice = Console.ReadLine();

    // End cleanly if the input stream closes.
    if (choice is null)
    {
        break;
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