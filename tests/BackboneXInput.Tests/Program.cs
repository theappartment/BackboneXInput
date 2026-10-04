using BackboneXInput.Core;

var count = 0;
void Assert(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
void Reject(Action action, string name) { try { action(); } catch (InvalidDataException) { Assert(true, name); return; } throw new Exception("FAIL: " + name); }
var axis = new Binding { Kind = InputKind.Axis, Min = 100, Center = 300, Max = 900 };
Assert(Mapping.Stick(100, axis) == -1 && Mapping.Stick(300, axis) == 0 && Mapping.Stick(900, axis) == 1, "asymmetric calibration endpoints");
Assert(Mapping.Stick(-500, axis) == -1 && Mapping.Stick(5000, axis) == 1, "clamped stick");
axis.Invert = true; Assert(Mapping.Stick(100, axis) == 1, "inverted stick");
var trigger = new Binding { Kind = InputKind.Axis, Rest = 40000, Full = 10000, Deadzone = .1 };
Assert(Mapping.Trigger(40000, trigger) == 0 && Mapping.Trigger(10000, trigger) == 1, "decreasing trigger");
Assert(Mapping.Trigger(39000, trigger) == 0, "trigger deadzone");
Assert(Mapping.PovMatches(4500, 0) && Mapping.PovMatches(4500, 9000) && !Mapping.PovMatches(4500, 18000), "diagonal hat");
Assert(Mapping.PovMatches(31500, 0) && Mapping.PovMatches(31500, 27000), "hat wraparound");
Assert(!Mapping.PovMatches(-1, 0) && !Mapping.PovMatches(65535, 0), "neutral POV representations");
Assert(Mapping.Radial(.05, .05, .12) == (0, 0), "radial deadzone");
var diagonal = Mapping.Radial(1, 1, .12);
Assert(Math.Abs(diagonal.X * diagonal.X + diagonal.Y * diagonal.Y - 1) < 1e-10, "radial diagonal clamp");
Assert(Mapping.Signed(-1) == short.MinValue && Mapping.Signed(1) == short.MaxValue && Mapping.Unsigned(1) == 255, "XInput endpoints");
var cfg = new AppConfig();
Reject(() => cfg.Validate(true), "incomplete mappings rejected");
cfg.PollingHz = 251; Reject(() => cfg.Validate(), "unsafe polling rate rejected"); cfg.PollingHz = 200;
cfg.Mappings[Control.A] = new() { Kind = InputKind.Button, Index = 0 };
cfg.Mappings[Control.LT] = new() { Kind = InputKind.Button, Index = 1 };
cfg.Mappings[Control.Up] = new() { Kind = InputKind.Pov, Index = 0, PovAngle = 0 };
cfg.Mappings[Control.Right] = new() { Kind = InputKind.Pov, Index = 0, PovAngle = 9000 };
var report = Mapping.Convert(new(new int[8], [true, true], [4500]), cfg);
Assert(report.Buttons.SetEquals([Control.A, Control.Up, Control.Right]) && report.LT == 255, "buttons, digital trigger and diagonal mapping");
cfg.Mappings[Control.Down] = new() { Kind = InputKind.Button, Index = 0 };
Assert(!Mapping.Convert(new(new int[8], [true], [0]), cfg).Buttons.Contains(Control.Up), "opposite dpad neutralized");
Assert(Mapping.Convert(new(new int[8], [false], [-1]), cfg).Buttons.Count == 0, "release clears buttons");
var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
try
{
    ConfigStore.Save(path, cfg); var loaded = ConfigStore.Load(path);
    Assert(loaded.Mappings[Control.Right].PovAngle == 9000 && loaded.PollingHz == 200, "JSON roundtrip");
    loaded.Mappings[Control.A].Index = -1; Reject(() => loaded.Validate(), "negative index rejected");
    loaded.Mappings[Control.A].Index = 0; loaded.LeftStickDeadzone = double.NaN; Reject(() => loaded.Validate(), "NaN rejected");
}
finally { File.Delete(path); }
var created = 0;
var disposed = 0;
using (var session = new OutputSession<XboxReport>(() => { created++; return new FakeOutput(() => disposed++); }))
{
    Assert(created == 0, "no Xbox target while waiting");
    session.Send(XboxReport.Neutral);
    session.Send(XboxReport.Neutral);
    Assert(created == 1, "one target for connected reports");
    session.Disconnect();
    Assert(disposed == 1, "disconnect removes virtual target");
    session.Disconnect();
    Assert(disposed == 1, "disconnect is idempotent");
    session.Send(XboxReport.Neutral);
    Assert(created == 2, "reconnect creates fresh target");
}
Assert(disposed == 2, "shutdown removes active target");
var attempts = 0;
using (var session = new OutputSession<XboxReport>(() => ++attempts == 1 ? throw new IOException("driver missing") : new FakeOutput(() => { })))
{
    try { session.Send(XboxReport.Neutral); } catch (IOException) { }
    session.Send(XboxReport.Neutral);
    Assert(attempts == 2, "failed target creation can retry");
}
Console.WriteLine($"{count} tests passed.");

sealed class FakeOutput(Action dispose) : IVirtualOutput<XboxReport>
{
    public void Send(XboxReport report) { }
    public void Dispose() => dispose();
}
