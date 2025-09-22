namespace CrestApps.OrchardCore.Services;

public interface ICloneable<T> : ICloneable
{
    new T Clone();

    object ICloneable.Clone()
        => Clone();
}
