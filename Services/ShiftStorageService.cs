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
}