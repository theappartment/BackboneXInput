using System.Runtime.InteropServices;
using BackboneXInput.Core;
using Vortice.DirectInput;

namespace BackboneXInput;

internal sealed class DirectInputSource : IDisposable
{
    private readonly IDirectInput8 input = DInput.DirectInput8Create();
    private IDirectInputDevice8? device;
    public DeviceInstance? Selected { get; private set; }
    public string[] Objects { get; private set; } = [];
    public static readonly string[] AxisNames = ["X", "Y", "Z", "RotationX", "RotationY", "RotationZ", "Slider0", "Slider1"];

    public IList<DeviceInstance> Enumerate() => input.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

    public bool Connect(AppConfig config)
    {
        Disconnect();
        var devices = Enumerate();
        var candidates = config.DeviceInstanceGuid is { } guid
            ? devices.Where(d => d.InstanceGuid == guid).ToArray()
            : devices.Where(d => d.ProductName.Contains(config.DeviceNameContains, StringComparison.OrdinalIgnoreCase)
                || d.InstanceName.Contains(config.DeviceNameContains, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length == 0) return false;
        if (candidates.Length > 1) throw new InvalidOperationException("Piu dispositivi corrispondono: usa select per scegliere un GUID.");
        var selected = candidates[0];
        var opened = input.CreateDevice(selected.InstanceGuid);
        try
        {
            opened.SetDataFormat<RawJoystickState>().CheckError();
            opened.SetCooperativeLevel(GetDesktopWindow(), CooperativeLevel.Background | CooperativeLevel.NonExclusive).CheckError();
            foreach (var obj in opened.GetObjects(DeviceObjectTypeFlags.Axis))
            {
                var properties = opened.GetObjectPropertiesById(obj.ObjectId);
                properties.Range = new InputRange { Minimum = 0, Maximum = 65535 };
                try { properties.DeadZone = 0; properties.Saturation = 10000; }
                catch (Exception ex) { Log.Write("Il driver non consente deadzone/saturazione DirectInput: " + obj.Name, ex); }
            }
            Objects = opened.GetObjects().Select(o => $"{o.Name} | {o.ObjectId}").ToArray();
            opened.Acquire().CheckError();
            device = opened;
            Selected = selected;
            return true;
        }
        catch { opened.Dispose(); throw; }
    }

    public RawState Read()
    {
        if (device is null) throw new InvalidOperationException("Controller scollegato.");
        // Check the native HRESULT explicitly: the convenience state getter may ignore it.
        device.Poll().CheckError();
        unsafe
        {
            RawJoystickState raw = default;
            // IDirectInputDevice8::GetDeviceState is COM slot 9 (IUnknown slots included).
            // Vortice's public helper discards its HRESULT; check it to avoid stale input.
            var pointer = device.NativePointer;
            var getState = (delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int>)(*(IntPtr**)pointer)[9];
            Marshal.ThrowExceptionForHR(getState(pointer, sizeof(RawJoystickState), (IntPtr)(&raw)));
            var state = new JoystickState();
            state.MarshalFrom(ref raw);
            return new([state.X, state.Y, state.Z, state.RotationX, state.RotationY, state.RotationZ, .. state.Sliders], state.Buttons, state.PointOfViewControllers);
        }
    }

    public void Disconnect()
    {
        if (device is not null) { device.Unacquire(); device.Dispose(); device = null; }
        Selected = null;
    }
    public void Dispose() { Disconnect(); input.Dispose(); }
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
}
