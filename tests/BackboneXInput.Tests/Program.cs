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
RawState Rest() => new(Enumerable.Repeat(32768, 8).ToArray(), new bool[128], [-1, -1, -1, -1]);
void Feed(MappingCapture capture, RawState state, double from, double duration)
{
    for (var i = 0; i <= 10; i++) capture.Push(state, from + duration * i / 10);
}
MappingCapture Ready(Control control)
{
    var c = new MappingCapture(control); c.Start(0); Feed(c, Rest(), 0, 1); return c;
}
var gui = Ready(Control.A);
Assert(gui.Stage == CaptureStage.Positive, "guided neutral phase advances");
var aState = Rest(); aState.Buttons[3] = true; Feed(gui, aState, 1, 4);
Assert(gui.Stage == CaptureStage.Review && gui.Result?.Index == 3 && gui.Result.Kind == InputKind.Button, "guided button detection");
gui.Push(Rest(), 100); Assert(gui.Result?.Index == 3, "review preserves detected binding");
gui.Start(200); Assert(gui.Stage == CaptureStage.Neutral && gui.Result is null, "retry clears candidate");
var multi = Ready(Control.A); var multiState = Rest(); multiState.Buttons[3] = true; multiState.Buttons[4] = true; Feed(multi, multiState, 1, 4);
Assert(multi.Stage == CaptureStage.Failed && multi.Result is null, "ambiguous simultaneous buttons rejected");
var held = new MappingCapture(Control.A); held.Start(0); Feed(held, aState, 0, 1);
Assert(held.Stage == CaptureStage.Failed, "held button at neutral rejected");
var hat = Ready(Control.Up); var hatState = Rest(); hatState.Pov[0] = 4500; Feed(hat, hatState, 1, 4);
Assert(hat.Stage == CaptureStage.Failed, "guided diagonal rejected");
var cardinal = Ready(Control.Left); hatState = Rest(); hatState.Pov[0] = 27000; Feed(cardinal, hatState, 1, 4);
Assert(cardinal.Result?.PovAngle == 27000, "guided cardinal POV detected");
var analogTrigger = Ready(Control.LT); var triggerState = Rest(); triggerState.Axes[5] = 0; triggerState.Buttons[7] = true; Feed(analogTrigger, triggerState, 1, 4);
Assert(analogTrigger.Result?.Kind == InputKind.Axis && analogTrigger.Result.Index == 5 && analogTrigger.Result.Full == 0, "analog trigger preferred over digital event");
var digitalTrigger = Ready(Control.RT); Feed(digitalTrigger, aState, 1, 4);
Assert(digitalTrigger.Result?.Kind == InputKind.Button, "guided digital trigger fallback");
var stickCapture = Ready(Control.LeftX); var right = Rest(); right.Axes[2] = 65535; Feed(stickCapture, right, 1, 4);
Assert(stickCapture.Stage == CaptureStage.Opposite, "stick asks opposite direction separately");
var left = Rest(); left.Axes[2] = 0; Feed(stickCapture, left, 5, 4);
Assert(stickCapture.Result?.Index == 2 && stickCapture.Result.Center == 32768 && !stickCapture.Result.Invert, "guided stick calibration");
var inverted = Ready(Control.LeftY); Feed(inverted, left, 1, 4); Feed(inverted, right, 5, 4);
Assert(inverted.Result?.Invert == true && Mapping.Stick(0, inverted.Result) == 1, "up direction inferred for inverted Y axis");
var wrong = Ready(Control.RightX); Feed(wrong, right, 1, 4); Feed(wrong, right, 5, 4);
Assert(wrong.Stage == CaptureStage.Failed, "same stick direction twice rejected");
var empty = Ready(Control.A); Feed(empty, Rest(), 1, 4);
Assert(empty.Stage == CaptureStage.Failed, "no input detected prompts retry");
foreach (var control in new[] { Control.LeftX, Control.LeftY, Control.RightX, Control.RightY })
{
    var calibration = new MappingCapture(control); calibration.Start(0);
    var neutral = Rest(); neutral.Axes[0] = 32767; Feed(calibration, neutral, 0, 1);
    var first = Rest(); first.Axes[0] = control is Control.LeftY or Control.RightY ? 0 : 65535;
    var second = Rest(); second.Axes[0] = control is Control.LeftY or Control.RightY ? 60000 : 1000;
    Feed(calibration, first, 1, 4);
    for (var i = 0; i <= 10; i++) calibration.Push(i < 4 ? first : second, 5 + 4.0 * i / 10);
    Assert(calibration.Stage == CaptureStage.Review && calibration.Result?.Index == 0
        && Mapping.Stick(second.Axes[0], calibration.Result) == -1,
        $"{control}: opposite endpoint detected despite stronger previous endpoint and reaction delay");
}
var tied = Ready(Control.RightX); var tiedFirst = Rest(); tiedFirst.Axes[1] = 60000;
var tiedSecond = Rest(); tiedSecond.Axes[1] = 5536; Feed(tied, tiedFirst, 1, 4);
for (var i = 0; i <= 10; i++) tied.Push(i < 3 ? tiedFirst : tiedSecond, 5 + 4.0 * i / 10);
Assert(tied.Stage == CaptureStage.Review && tied.Result?.Min == 5536, "equal excursions do not select previous direction on tie");
var spike = Ready(Control.LeftX); Feed(spike, right, 1, 4);
for (var i = 0; i <= 10; i++) spike.Push(i == 5 ? left : right, 5 + 4.0 * i / 10);
Assert(spike.Stage == CaptureStage.Failed, "single opposite glitch cannot confirm calibration");
AppConfig AllMapped()
{
    var c = new AppConfig();
    foreach (var control in Enum.GetValues<Control>())
        c.SetMapping(control, new Binding { Kind = control >= Control.LeftX ? InputKind.Axis : InputKind.Button, Index = 0 });
    return c;
}
var optional = AllMapped(); optional.SkipControl(Control.Menu); optional.SkipControl(Control.View); optional.Validate(true);
Assert(optional.ResolvedCount == 20 && optional.Mappings.Count == 18, "explicit skip permits complete profile without Menu/View");
var physical = Rest(); physical.Buttons[0] = true;
var optionalReport = Mapping.Convert(physical, optional);
Assert(optionalReport.Buttons.Contains(Control.A) && !optionalReport.Buttons.Contains(Control.Menu) && !optionalReport.Buttons.Contains(Control.View), "skipped Menu/View neutral while mapped controls work");
optional.SkipControl(Control.LT); optional.SkipControl(Control.LeftX); optional.SkipControl(Control.LeftY); optional.Validate(true);
optionalReport = Mapping.Convert(physical, optional);
Assert(optionalReport.LT == 0 && optionalReport.LeftX == 0 && optionalReport.LeftY == 0 && optionalReport.RT == 255, "skipped trigger/stick neutral without disabling other controls");
var hole = AllMapped(); hole.Mappings.Remove(Control.Menu); Reject(() => hole.Validate(true), "unreviewed missing mapping still rejected");
var overlap = AllMapped(); overlap.SkippedControls.Add(Control.View); Reject(() => overlap.Validate(), "mapped and skipped simultaneously rejected");
var unknownSkip = new AppConfig(); unknownSkip.SkippedControls.Add((Control)999); Reject(() => unknownSkip.Validate(), "unknown skipped control rejected");
var noneMapped = new AppConfig { SkippedControls = Enum.GetValues<Control>().ToHashSet() }; Reject(() => noneMapped.Validate(true), "all skipped profile cannot create useless Xbox target");
try
{
    ConfigStore.Save(path, optional); var restored = ConfigStore.Load(path); restored.Validate(true);
    Assert(restored.SkippedControls.SetEquals(optional.SkippedControls) && !restored.Mappings.ContainsKey(Control.View), "JSON retains explicit skips and absent bindings");
}
finally { File.Delete(path); }
var oldJson = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(AllMapped(), ConfigStore.Options))!.AsObject();
oldJson.Remove("SkippedControls");
var oldConfig = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(oldJson.ToJsonString(), ConfigStore.Options)!;
oldConfig.Validate(true); Assert(oldConfig.SkippedControls.Count == 0, "older full profiles remain compatible");
optional.SetMapping(Control.Menu, new Binding { Kind = InputKind.Button, Index = 0 }); optional.Validate(true);
Assert(!optional.SkippedControls.Contains(Control.Menu) && Mapping.Convert(physical, optional).Buttons.Contains(Control.Menu), "remapping skipped control reactivates it");
Console.WriteLine($"{count} tests passed.");

sealed class FakeOutput(Action dispose) : IVirtualOutput<XboxReport>
{
    public void Send(XboxReport report) { }
    public void Dispose() => dispose();
}
