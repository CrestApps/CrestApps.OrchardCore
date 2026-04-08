namespace CrestApps.Core.Services;

/// <summary>
/// Provides a strongly-typed clone operation, creating a deep copy of the current instance.
/// Extends <see cref="ICloneable"/> with a typed <see cref="Clone"/> method.
/// </summary>
/// <typeparam name="T">The type of the object being cloned.</typeparam>
public interface ICloneable<T> : ICloneable
{
    /// <summary>
    /// Creates a deep copy of the current instance.
    /// </summary>
    /// <returns>A new instance of <typeparamref name="T"/> that is a copy of this object.</returns>
    new T Clone();

    /// <inheritdoc />
    object ICloneable.Clone()
        => Clone();
}
