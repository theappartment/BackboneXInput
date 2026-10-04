namespace BackboneXInput.Core;

public sealed record RawState(int[] Axes, bool[] Buttons, int[] Pov);
public sealed record XboxReport(HashSet<Control> Buttons, short LeftX, short LeftY, short RightX, short RightY, byte LT, byte RT)
{
    public static XboxReport Neutral => new(new(), 0, 0, 0, 0, 0, 0);
}

public static class Mapping
{
    public static double Stick(int raw, Binding b)
    {
        var delta = raw - b.Center;
        var v = Math.Clamp(delta / (delta >= 0 ? b.Max - b.Center : b.Center - b.Min), -1, 1);
        return b.Invert ? -v : v;
    }

    public static double Activity(RawState state, Binding b)
    {
        return b.Kind switch
        {
            InputKind.Button => b.Index < state.Buttons.Length && state.Buttons[b.Index] ? 1 : 0,
            InputKind.Pov => b.Index < state.Pov.Length && PovMatches(state.Pov[b.Index], b.PovAngle) ? 1 : 0,
            InputKind.Axis => b.Index < state.Axes.Length ? Trigger(state.Axes[b.Index], b) : 0,
            _ => 0
        };
    }

    public static double Trigger(int raw, Binding b)
    {
        var value = Math.Clamp((raw - b.Rest) / (b.Full - b.Rest), 0, 1);
        if (b.Invert) value = 1 - value;
        return value <= b.Deadzone ? 0 : (value - b.Deadzone) / (1 - b.Deadzone);
    }

    public static bool PovMatches(int value, int angle)
    {
        if (value < 0 || (value & 0xffff) == 0xffff || value >= 36000) return false;
        var distance = Math.Abs(value - angle);
        return Math.Min(distance, 36000 - distance) <= 4500;
    }

    public static (double X, double Y) Radial(double x, double y, double deadzone)
    {
        var magnitude = Math.Sqrt(x * x + y * y);
        if (magnitude <= deadzone) return (0, 0);
        var scale = (Math.Min(magnitude, 1) - deadzone) / (1 - deadzone) / magnitude;
        return (x * scale, y * scale);
    }

    public static short Signed(double v) => (short)Math.Round(Math.Clamp(v, -1, 1) * (v < 0 ? 32768 : 32767));
    public static byte Unsigned(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);

    public static XboxReport Convert(RawState state, AppConfig config)
    {
        double Axis(Control c) => config.Mappings.TryGetValue(c, out var b) && b.Index < state.Axes.Length ? Stick(state.Axes[b.Index], b) : 0;
        double Level(Control c) => config.Mappings.TryGetValue(c, out var b) ? Activity(state, b) : 0;
        var left = Radial(Axis(Control.LeftX), Axis(Control.LeftY), config.LeftStickDeadzone);
        var right = Radial(Axis(Control.RightX), Axis(Control.RightY), config.RightStickDeadzone);
        var pressed = new HashSet<Control>();
        foreach (var (c, b) in config.Mappings)
            if (c < Control.LeftX && c is not (Control.LT or Control.RT) && Activity(state, b) >= b.Threshold) pressed.Add(c);
        // Opposite directions cannot coexist in one physical D-pad.
        if (pressed.Contains(Control.Up) && pressed.Contains(Control.Down)) { pressed.Remove(Control.Up); pressed.Remove(Control.Down); }
        if (pressed.Contains(Control.Left) && pressed.Contains(Control.Right)) { pressed.Remove(Control.Left); pressed.Remove(Control.Right); }
        return new(pressed, Signed(left.X), Signed(left.Y), Signed(right.X), Signed(right.Y), Unsigned(Level(Control.LT)), Unsigned(Level(Control.RT)));
    }
}
