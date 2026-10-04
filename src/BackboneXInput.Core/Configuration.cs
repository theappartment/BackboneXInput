using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackboneXInput.Core;

public enum InputKind { Button, Axis, Pov }
public enum Control { A, B, X, Y, LB, RB, LT, RT, L3, R3, Up, Down, Left, Right, Menu, View, LeftX, LeftY, RightX, RightY }

public sealed class Binding
{
    public InputKind Kind { get; set; }
    public int Index { get; set; }
    public double Min { get; set; } = 0;
    public double Center { get; set; } = 32767.5;
    public double Max { get; set; } = 65535;
    public double Rest { get; set; }
    public double Full { get; set; } = 65535;
    public bool Invert { get; set; }
    public double Deadzone { get; set; } = 0.05;
    public double Threshold { get; set; } = 0.5;
    public int PovAngle { get; set; }
}

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = 1;
    public string DeviceNameContains { get; set; } = "Backbone One";
    public Guid? DeviceInstanceGuid { get; set; }
    public int PollingHz { get; set; } = 200;
    public double LeftStickDeadzone { get; set; } = 0.12;
    public double RightStickDeadzone { get; set; } = 0.12;
    public Dictionary<Control, Binding> Mappings { get; set; } = new();

    public void Validate(bool complete = false)
    {
        if (SchemaVersion != 1) throw new InvalidDataException("SchemaVersion non supportata.");
        if (PollingHz is < 125 or > 250) throw new InvalidDataException("PollingHz deve essere 125..250.");
        if (string.IsNullOrWhiteSpace(DeviceNameContains) && DeviceInstanceGuid is null)
            throw new InvalidDataException("Specificare nome dispositivo o GUID.");
        CheckUnit(LeftStickDeadzone, nameof(LeftStickDeadzone), false);
        CheckUnit(RightStickDeadzone, nameof(RightStickDeadzone), false);
        if (Mappings is null) throw new InvalidDataException("Mappings mancante.");
        foreach (var (control, b) in Mappings)
        {
            if (!Enum.IsDefined(control) || b is null || !Enum.IsDefined(b.Kind)) throw new InvalidDataException("Mapping sconosciuto.");
            var limit = b.Kind switch { InputKind.Button => 128, InputKind.Axis => 8, _ => 4 };
            if (b.Index < 0 || b.Index >= limit) throw new InvalidDataException($"Indice non valido: {control}.");
            CheckUnit(b.Deadzone, "Deadzone", false);
            CheckUnit(b.Threshold, "Threshold", true);
            if (b.Threshold <= 0) throw new InvalidDataException("Threshold deve essere > 0.");
            if (b.Kind == InputKind.Pov && b.PovAngle is not (0 or 9000 or 18000 or 27000))
                throw new InvalidDataException("PovAngle deve essere cardinale: 0/9000/18000/27000.");
            if (control >= Control.LeftX && b.Kind != InputKind.Axis) throw new InvalidDataException("Uno stick richiede un asse.");
            if (b.Kind == InputKind.Axis)
            {
                if (!new[] { b.Min, b.Center, b.Max, b.Rest, b.Full }.All(double.IsFinite)) throw new InvalidDataException("Valori asse non finiti.");
                if (control >= Control.LeftX && !(b.Min < b.Center && b.Center < b.Max)) throw new InvalidDataException($"Range stick non valido: {control}.");
                if (control < Control.LeftX && Math.Abs(b.Full - b.Rest) < 1) throw new InvalidDataException($"Range input non valido: {control}.");
            }
        }
        if (complete)
        {
            var missing = Enum.GetValues<Control>().Where(c => !Mappings.ContainsKey(c)).ToArray();
            if (missing.Length > 0) throw new InvalidDataException("Mapping incompleto: " + string.Join(", ", missing));
        }
    }

    private static void CheckUnit(double value, string name, bool includeOne)
    {
        if (!double.IsFinite(value) || value < 0 || (includeOne ? value > 1 : value >= 1))
            throw new InvalidDataException($"{name} fuori intervallo.");
    }
}

public static class ConfigStore
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppConfig Load(string path)
    {
        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("JSON vuoto.");
        config.Validate();
        return config;
    }

    public static void Save(string path, AppConfig config)
    {
        config.Validate();
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, Options));
        File.Move(temp, path, true);
    }
}
