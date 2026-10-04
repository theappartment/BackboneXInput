namespace BackboneXInput.Core;

public enum CaptureStage { Idle, Neutral, Positive, Opposite, Review, Failed }

public sealed class MappingCapture(Control control)
{
    private readonly List<RawState> samples = [];
    private RawState? baseline;
    private double started;
    private int axis;
    private int positive;
    public Control Control { get; } = control;
    public CaptureStage Stage { get; private set; }
    public Binding? Result { get; private set; }
    public string? Error { get; private set; }
    public double Duration => Stage == CaptureStage.Neutral ? 1 : 4;
    public double Remaining(double now) => Math.Max(0, Duration - (now - started));

    public void Start(double now)
    {
        Result = null;
        Error = null;
        baseline = null;
        Enter(CaptureStage.Neutral, now);
    }

    public void Push(RawState state, double now)
    {
        if (Stage is not (CaptureStage.Neutral or CaptureStage.Positive or CaptureStage.Opposite)) return;
        samples.Add(state);
        if (Remaining(now) > 0) return;
        if (samples.Count < 5) { Fail("Lettura troppo lenta. Riprova."); return; }
        if (Stage == CaptureStage.Neutral)
        {
            baseline = new(
                Enumerable.Range(0, 8).Select(i => samples.Select(s => s.Axes[i]).Order().ElementAt(samples.Count / 2)).ToArray(),
                samples.Last().Buttons,
                samples.Last().Pov);
            if (samples.TakeLast(5).Any(s => s.Buttons.Any(v => v) || s.Pov.Any(p => p >= 0 && p < 36000)))
            { Fail("Rilascia tutti i tasti e il D-pad prima di iniziare."); return; }
            Enter(CaptureStage.Positive, now);
            return;
        }
        if (Stage == CaptureStage.Positive)
        {
            axis = Enumerable.Range(0, 8).OrderByDescending(i => samples.Max(s => Math.Abs(s.Axes[i] - baseline!.Axes[i]))).First();
            positive = samples.Select(s => s.Axes[axis]).MaxBy(v => Math.Abs(v - baseline!.Axes[axis]));
            if (Control >= Control.LeftX)
            {
                if (Math.Abs(positive - baseline!.Axes[axis]) < 2048) { Fail("Movimento non rilevato. Porta la levetta fino in fondo."); return; }
                Enter(CaptureStage.Opposite, now);
                return;
            }
            Result = Detect();
            if (Result is null) { if (Stage != CaptureStage.Failed) Fail("Nessun comando rilevato. Premi e tieni il comando indicato."); }
            else Stage = CaptureStage.Review;
            return;
        }
        var opposite = samples.Select(s => s.Axes[axis]).MaxBy(v => Math.Abs(v - baseline!.Axes[axis]));
        var center = baseline!.Axes[axis];
        if ((positive - (double)center) * (opposite - (double)center) >= 0 || Math.Abs(opposite - center) < 2048)
        { Fail("Direzione opposta non rilevata. Riprova muovendo la stessa levetta nei due versi."); return; }
        Result = new() { Kind = InputKind.Axis, Index = axis, Min = Math.Min(positive, opposite), Max = Math.Max(positive, opposite), Center = center, Invert = positive < center };
        Stage = CaptureStage.Review;
    }

    private Binding? Detect()
    {
        var sustained = Math.Max(2, samples.Count / 5);
        Binding? analog = Math.Abs(positive - baseline!.Axes[axis]) >= 2048
            ? new() { Kind = InputKind.Axis, Index = axis, Rest = baseline.Axes[axis], Full = positive } : null;
        if (Control is Control.LT or Control.RT && analog is not null) return analog;
        var buttons = Enumerable.Range(0, baseline.Buttons.Length)
            .Where(i => !baseline.Buttons[i] && samples.Count(s => s.Buttons[i]) >= sustained).ToArray();
        if (buttons.Length > 1) { Fail("Sono stati premuti piu tasti. Riprova premendone soltanto uno."); return null; }
        if (buttons.Length == 1) return new() { Kind = InputKind.Button, Index = buttons[0] };
        var hats = Enumerable.Range(0, baseline.Pov.Length)
            .Where(i => samples.Count(s => s.Pov[i] >= 0 && s.Pov[i] < 36000) >= sustained).ToArray();
        if (hats.Length > 1) { Fail("Piu D-pad rilevati. Premi soltanto il comando richiesto."); return null; }
        if (hats.Length == 1)
        {
            var index = hats[0];
            var angle = samples.Select(s => s.Pov[index]).Where(v => v >= 0 && v < 36000).GroupBy(v => v).MaxBy(g => g.Count())!.Key;
            if (angle % 9000 != 0) { Fail("Hai premuto una diagonale. Premi una sola direzione del D-pad."); return null; }
            return new() { Kind = InputKind.Pov, Index = index, PovAngle = angle };
        }
        return analog;
    }

    private void Enter(CaptureStage stage, double now) { Stage = stage; started = now; samples.Clear(); }
    private void Fail(string error) { Error = error; Stage = CaptureStage.Failed; Result = null; }
}
