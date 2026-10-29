using MaritimeCommand.Models;

namespace MaritimeCommand.Services;

// Simulates travel while leaving arrival rules to DispatchService.
public class UnitTravelSimulationService
{
    private readonly IncidentService _incidentService;
    private readonly RescueUnitService _rescueUnitService;
    private readonly DispatchService _dispatchService;

    // Each assignment gets its own remaining travel time.
    private readonly Dictionary<int, TimeSpan> _remainingTravel = new();

    // Return journeys belong to units, because their assignments have ended.
    private readonly Dictionary<int, TimeSpan> _remainingReturnTravel = new();

    public UnitTravelSimulationService(
        IncidentService incidentService,
        RescueUnitService rescueUnitService,
        DispatchService dispatchService)
    {
        _incidentService = incidentService;
        _rescueUnitService = rescueUnitService;
        _dispatchService = dispatchService;
    }

    // Returns notifications for arrivals completed during this update.
    public List<string> Update(TimeSpan elapsed)
    {
        var notifications = new List<string>();

        foreach (var assignment in _dispatchService.GetAssignments())
        {
            // Ended or manually completed journeys must stop updating.
            if (assignment.EndedAt is not null ||
            assignment.ArrivedAt is not null)
            {
                _remainingTravel.Remove(assignment.Id);
                continue;
            }

            var incident = _incidentService.GetIncidentById(assignment.IncidentId);

            var unit = _rescueUnitService.GetUnitById(assignment.UnitId);

            if (incident is null || unit is null)
            {
                continue;
            }

            // Start tracking a newly discovered assignment.
            if (!_remainingTravel.ContainsKey(assignment.Id))
            {
                _remainingTravel[assignment.Id] = CalculateTravelTime(unit, incident.Latitude, incident.Longitude);
                continue;
            }

            _remainingTravel[assignment.Id] -= elapsed;

            if (_remainingTravel[assignment.Id] > TimeSpan.Zero)
            {
                continue;
            }

            // Reuse the existing arrival checks, coordinates and logging.
            _dispatchService.RecordArrival(assignment.Id);

            _remainingTravel.Remove(assignment.Id);

            notifications.Add($"{unit.Name} arrived at incident #{incident.Id}.");
        }

        // Update journeys home separately from incident assignments.
        foreach (var unit in _rescueUnitService.GetUnits())
        {
            if (unit.Status != UnitStatus.Returning)
            {
                // Remove any timer belonging to a cancelled return journey.
                _remainingReturnTravel.Remove(unit.Id);
                continue;
            }


            // Start tracking a unit newly released to return to HQ.
            if (!_remainingReturnTravel.ContainsKey(unit.Id))
            {
                _remainingReturnTravel[unit.Id] = CalculateTravelTime(
                    unit,
                    DispatchService.HeadquartersLatitude,
                    DispatchService.HeadquartersLongitude);

                continue;
            }

            _remainingReturnTravel[unit.Id] -= elapsed;

            if (_remainingReturnTravel[unit.Id] > TimeSpan.Zero)
            {
                continue;
            }

            // Let DispatchService validate and complete the arrival.
            _dispatchService.RecordReturnToHeadquarters(unit.Id);

            _remainingReturnTravel.Remove(unit.Id);

            notifications.Add(
                $"{unit.Name} arrived at HQ. " +
                "Awaiting servicing and readiness assessment.");
        }

        return notifications;
    }

    // Copies current journey timers into the snapshot.
    public void CaptureState(SimulationState state)
    {
        state.RemainingTravel =
            new Dictionary<int, TimeSpan>(_remainingTravel);

        state.RemainingReturnTravel =
            new Dictionary<int, TimeSpan>(_remainingReturnTravel);
    }

    // Restores journey timers after incidents, units and assignments are restored.
    public void RestoreState(SimulationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.RemainingTravel is null ||
            state.RemainingReturnTravel is null)
        {
            throw new InvalidDataException(
                "The save is missing travel timer collections.");
        }

        // Make separate copies and validate before changing current timers.
        var travelTimers =
            new Dictionary<int, TimeSpan>(state.RemainingTravel);

        var returnTimers =
            new Dictionary<int, TimeSpan>(state.RemainingReturnTravel);

        var assignments = _dispatchService.GetAssignments()
            .ToDictionary(assignment => assignment.Id);

        foreach (var timer in travelTimers)
        {
            // The timer's key is an assignment ID.
            if (timer.Value < TimeSpan.Zero ||
                !assignments.TryGetValue(timer.Key, out var assignment))
            {
                throw new InvalidDataException(
                    "The save contains an invalid outbound travel timer.");
            }

            if (assignment.EndedAt is not null ||
                assignment.ArrivedAt is not null)
            {
                throw new InvalidDataException(
                    $"Assignment #{assignment.Id} should not have a travel timer.");
            }
        }

        foreach (var timer in returnTimers)
        {
            // Return timer keys are unit IDs, rather than assignment IDs.
            var unit = _rescueUnitService.GetUnitById(timer.Key);

            if (timer.Value < TimeSpan.Zero ||
                unit is null ||
                unit.Status != UnitStatus.Returning)
            {
                throw new InvalidDataException(
                    "The save contains an invalid return journey timer.");
            }
        }

        // Every timer passed: replace the current simulation progress.
        _remainingTravel.Clear();

        foreach (var timer in travelTimers)
        {
            _remainingTravel.Add(timer.Key, timer.Value);
        }

        _remainingReturnTravel.Clear();

        foreach (var timer in returnTimers)
        {
            _remainingReturnTravel.Add(timer.Key, timer.Value);
        }
    }

    private TimeSpan CalculateTravelTime(RescueUnit unit, double destinationLatitude, double destinationLongitude)
    {
        // Approximate kilometers between nearby coordinates.
        double averageLatitudeRadians = ((unit.Latitude + destinationLatitude) / 2) * Math.PI / 180;

        double northSouthKm = (destinationLatitude - unit.Latitude) * 111.2;

        double eastWestKm = (destinationLongitude - unit.Longitude) * 111.2 * Math.Cos(averageLatitudeRadians);

        double distanceKm = Math.Sqrt(northSouthKm * northSouthKm + eastWestKm * eastWestKm);

        // Illustrative simulation speeds, NOT operational specifications.
        double speedKmh = unit.Type switch
        {
            UnitType.Helicopter => 220,
            UnitType.SmallPatrolBoat => 45,
            UnitType.MediumPatrolBoat => 35,
            UnitType.EmergencyVessel => 25,
            UnitType.ScoutDrone => 70,
            UnitType.Ambulance => 60,
            UnitType.FireEngine => 50,
            _ => 30
        };
    
        // Speed up travel for a playable demonstration.
        double realTravelSeconds = distanceKm / speedKmh * 3600;
        double simulatedTravelSeconds = realTravelSeconds / 20;

        // Even a nearby response takes at least fifteen seconds.
        return TimeSpan.FromSeconds(Math.Max(15, simulatedTravelSeconds));
    }
}