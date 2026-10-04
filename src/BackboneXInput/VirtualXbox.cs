using BackboneXInput.Core;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace BackboneXInput;

internal sealed class VirtualXbox : IVirtualOutput<XboxReport>
{
    private readonly ViGEmClient client;
    private readonly IXbox360Controller controller;
    private static readonly Dictionary<Control, Xbox360Button> buttons = new()
    {
        [Control.A] = Xbox360Button.A, [Control.B] = Xbox360Button.B, [Control.X] = Xbox360Button.X, [Control.Y] = Xbox360Button.Y,
        [Control.LB] = Xbox360Button.LeftShoulder, [Control.RB] = Xbox360Button.RightShoulder,
        [Control.L3] = Xbox360Button.LeftThumb, [Control.R3] = Xbox360Button.RightThumb,
        [Control.Up] = Xbox360Button.Up, [Control.Down] = Xbox360Button.Down,
        [Control.Left] = Xbox360Button.Left, [Control.Right] = Xbox360Button.Right,
        [Control.Menu] = Xbox360Button.Start, [Control.View] = Xbox360Button.Back
    };

    public VirtualXbox()
    {
        client = new ViGEmClient();
        try { controller = client.CreateXbox360Controller(); controller.AutoSubmitReport = false; controller.Connect(); }
        catch { client.Dispose(); throw; }
    }

    public void Send(XboxReport report)
    {
        foreach (var (c, b) in buttons) controller.SetButtonState(b, report.Buttons.Contains(c));
        controller.SetAxisValue(Xbox360Axis.LeftThumbX, report.LeftX);
        controller.SetAxisValue(Xbox360Axis.LeftThumbY, report.LeftY);
        controller.SetAxisValue(Xbox360Axis.RightThumbX, report.RightX);
        controller.SetAxisValue(Xbox360Axis.RightThumbY, report.RightY);
        controller.SetSliderValue(Xbox360Slider.LeftTrigger, report.LT);
        controller.SetSliderValue(Xbox360Slider.RightTrigger, report.RT);
        controller.SubmitReport();
    }

    public void Dispose()
    {
        try { Send(XboxReport.Neutral); }
        finally { try { controller.Disconnect(); } finally { client.Dispose(); } }
    }
}
