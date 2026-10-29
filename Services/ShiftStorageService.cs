using System.Text.Json;
using System.Text.Json.Serialization;
using MaritimeCommand.Models;

namespace MaritimeCommand.Services;

// Handles writing shift snapshots to disk.
public class ShiftStorageService
{
    // Resolve the location once, relative to the startup working directory.
    public string SavePath { get; } =
        Path.GetFullPath(Path.Combine("Data", "shift.json"));

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        // Format the file so we can comfortably read it ourselves.
        WriteIndented = true,

        // Store enum names such as "Returning" instead of numbers.
        Converters = { new JsonStringEnumConverter() }
    };

    public void Save(ShiftState state)
    {   
        ArgumentNullException.ThrowIfNull(state);

        // Make sure the save folder exists before writing.
        string directory = Path.GetDirectoryName(SavePath)!;
        Directory.CreateDirectory(directory);

        state.SavedAt = DateTimeOffset.UtcNow;

        // Convert our snapshot object into JSON text.
        string json = JsonSerializer.Serialize(state, _jsonOptions);

        // Finish writing a temporary file before replacing the previous save.
        string temporaryPath = SavePath + ".tmp";

        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, SavePath, overwrite: true);
    }

    // Reads a saved shift without changing the running services.
    public ShiftState? Load()
    {
        string json;

        try
        {
            json = File.ReadAllText(SavePath);
        }

        catch (FileNotFoundException)
        {
            // No save yet: the application can start a fresh shift.
            return null;
        }

        catch (DirectoryNotFoundException)
        {
            // The save folder might not exist on the first run.
            return null;
        }

        // Convert the JSON text back into our C# snapshot object.
        var state = JsonSerializer.Deserialize<ShiftState>(json, _jsonOptions);

        if (state is null)
        {
            throw new InvalidDataException("The save file does not contain a shift.");
        }

        // Only accept a save format we currently understand.
        if (state.SaveVersion != 1)
        {
            throw new InvalidDataException($"Unsuported save version: {state.SaveVersion}.");
        }

        // JSON can contain explicit nulls, even for non-nullable properties.
        if (state.SavedAt == default ||
            state.Incidents is null ||
            state.Units is null ||
            state.Assignments is null ||
            state.LogEntries is null ||
            state.Simulation is null)
        {
            throw new InvalidDataException("The save file is missing shift data.");
        }

        var simulation = state.Simulation;

        if (simulation.RemainingTravel is null ||
            simulation.RemainingReturnTravel is null ||
            simulation.RemainingAssessment is null ||
            simulation.ReportedAssignments is null)
        {
            throw new InvalidDataException("The save file is missing simulation collections.");
        }
        return state;
    }
}