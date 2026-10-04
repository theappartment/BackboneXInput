using System.Diagnostics;
using BackboneXInput.Core;

namespace BackboneXInput;

internal static class Wizard
{
    public static void Run(DirectInputSource source, AppConfig config, string path, CancellationToken token)
    {
        Console.WriteLine("Mapping: indici JSON da 0; pulsanti monitor anche da 1 come joy.cpl. Ctrl+C annulla senza salvare.");
        config.DeviceInstanceGuid = source.Selected!.InstanceGuid;
        foreach (var control in Enum.GetValues<Control>())
        {
            while (true)
            {
                Console.WriteLine($"\n{control}: rilascia tutti i comandi, centra gli stick, poi premi Invio.");
                ReadLine(token);
                var baseline = Capture(source, 0.5, token).Last();
                Binding binding;
                if (control >= Control.LeftX)
                {
                    var horizontal = control is Control.LeftX or Control.RightX;
                    Console.WriteLine($"Per 4 secondi porta SOLO lo stick {((control is Control.LeftX or Control.LeftY) ? "sinistro" : "destro")} completamente {(horizontal ? "a DESTRA" : "in ALTO")} e tienilo fermo.");
                    var positive = Capture(source, 4, token);
                    var index = Enumerable.Range(0, 8).OrderByDescending(i => positive.Max(s => Math.Abs(s.Axes[i] - baseline.Axes[i]))).First();
                    var full = positive.Select(s => s.Axes[index]).MaxBy(v => Math.Abs(v - baseline.Axes[index]));
                    if (Math.Abs(full - baseline.Axes[index]) < 2048) { Console.WriteLine("Movimento insufficiente. Riprova."); continue; }
                    Console.WriteLine("Ora per 4 secondi porta lo stesso stick completamente nella direzione OPPOSTA.");
                    var negative = Capture(source, 4, token);
                    var opposite = negative.Select(s => s.Axes[index]).MaxBy(v => Math.Abs(v - baseline.Axes[index]));
                    var center = baseline.Axes[index];
                    if ((full - (double)center) * (opposite - (double)center) >= 0)
                    { Console.WriteLine("Non rilevate le due estremita opposte. Riprova."); continue; }
                    binding = new() { Kind = InputKind.Axis, Index = index, Min = Math.Min(full, opposite), Max = Math.Max(full, opposite), Center = center, Invert = full < center };
                }
                else
                {
                    Console.WriteLine($"Per 4 secondi premi e tieni SOLO {control}. Per LT/RT premi a fondo.");
                    var samples = Capture(source, 4, token);
                    binding = Detect(control, baseline, samples)!;
                    if (binding is null) { Console.WriteLine("Nessun input rilevato. Riprova."); continue; }
                }
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(binding, ConfigStore.Options));
                Console.WriteLine("Confermi? Invio = si; r = ripeti.");
                if (ReadLine(token).Trim().Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
                config.Mappings[control] = binding;
                break;
            }
        }
        config.Validate(true);
        if (config.Mappings[Control.LT].Kind == InputKind.Axis && config.Mappings[Control.RT].Kind == InputKind.Axis
            && config.Mappings[Control.LT].Index == config.Mappings[Control.RT].Index)
            Console.WriteLine("LT e RT condividono un asse: DirectInput potrebbe non rappresentarli entrambi premuti. Verifica nel monitor.");
        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        ConfigStore.Save(path, config);
        Console.WriteLine("Configurazione salvata: " + Path.GetFullPath(path));
    }

    private static Binding? Detect(Control control, RawState baseline, List<RawState> samples)
    {
        var axis = Enumerable.Range(0, 8).OrderByDescending(i => samples.Max(s => Math.Abs(s.Axes[i] - baseline.Axes[i]))).First();
        var full = samples.Select(s => s.Axes[axis]).MaxBy(v => Math.Abs(v - baseline.Axes[axis]));
        Binding? analog = Math.Abs(full - baseline.Axes[axis]) >= 2048
            ? new() { Kind = InputKind.Axis, Index = axis, Rest = baseline.Axes[axis], Full = full } : null;
        if (control is Control.LT or Control.RT && analog is not null) return analog;
        for (var i = 0; i < baseline.Buttons.Length; i++)
            if (!baseline.Buttons[i] && samples.Any(s => s.Buttons[i])) return new() { Kind = InputKind.Button, Index = i };
        for (var i = 0; i < baseline.Pov.Length; i++)
        {
            var angles = samples.Select(s => s.Pov[i]).Where(p => p >= 0 && p < 36000 && p != baseline.Pov[i]).ToArray();
            if (angles.Length > 0)
            {
                var angle = angles.GroupBy(v => v).MaxBy(g => g.Count())!.Key;
                if (angle % 9000 != 0) { Console.WriteLine("POV diagonale: premi una sola direzione."); return null; }
                return new() { Kind = InputKind.Pov, Index = i, PovAngle = angle };
            }
        }
        return analog;
    }

    private static List<RawState> Capture(DirectInputSource source, double seconds, CancellationToken token)
    {
        var samples = new List<RawState>();
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < seconds)
        {
            token.ThrowIfCancellationRequested();
            samples.Add(source.Read());
            if (token.WaitHandle.WaitOne(5)) token.ThrowIfCancellationRequested();
        }
        return samples;
    }

    private static string ReadLine(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var line = Console.ReadLine() ?? throw new OperationCanceledException("Input console chiuso.");
        token.ThrowIfCancellationRequested();
        return line;
    }
}
