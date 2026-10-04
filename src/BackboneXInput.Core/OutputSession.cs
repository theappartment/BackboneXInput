namespace BackboneXInput.Core;

public interface IVirtualOutput<in T> : IDisposable
{
    void Send(T report);
}

// A target exists only between the first valid physical report and input loss.
public sealed class OutputSession<T>(Func<IVirtualOutput<T>> factory) : IDisposable
{
    private IVirtualOutput<T>? active;
    public void Send(T report)
    {
        active ??= factory();
        active.Send(report);
    }
    public void Disconnect()
    {
        var previous = active;
        active = null;
        previous?.Dispose();
    }
    public void Dispose() => Disconnect();
}
