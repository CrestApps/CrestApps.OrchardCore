namespace CrestApps.Services;

public interface ICloneable<T> : ICloneable
{
    new T Clone();

    object ICloneable.Clone()
        => Clone();
}
