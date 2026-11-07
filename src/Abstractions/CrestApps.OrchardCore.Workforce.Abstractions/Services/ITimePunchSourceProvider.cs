namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Describes the punch sources a module adds beside the well-known ones in
/// <see cref="Models.TimePunchSource"/>, so time clock policies can allow them and screens can name them.
/// </summary>
public interface ITimePunchSourceProvider
{
    /// <summary>
    /// Returns the sources the module adds.
    /// </summary>
    /// <returns>The sources.</returns>
    IEnumerable<TimePunchSourceDescriptor> GetSources();
}
