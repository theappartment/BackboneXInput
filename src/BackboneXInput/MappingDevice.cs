using BackboneXInput.Core;

namespace BackboneXInput;

public sealed record MappingDeviceInfo(Guid InstanceGuid, string Name)
{
    public override string ToString() => Name;
}

public sealed class MappingDevice : IDisposable
{
    private readonly DirectInputSource source = new();
    public MappingDeviceInfo[] Enumerate() => source.Enumerate().Select(d => new MappingDeviceInfo(d.InstanceGuid, d.ProductName + " | " + d.InstanceName)).ToArray();
    public bool Connect(Guid guid) => source.Connect(new AppConfig { DeviceInstanceGuid = guid });
    public RawState Read() => source.Read();
    public void Disconnect() => source.Disconnect();
    public void Dispose() => source.Dispose();
}
