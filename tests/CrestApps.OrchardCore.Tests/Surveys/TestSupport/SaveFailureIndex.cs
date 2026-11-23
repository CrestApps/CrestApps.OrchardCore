using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// The index row of <see cref="SaveFailureIndexProvider{T}"/>, which never maps one: it only makes a save fail.
/// </summary>
internal sealed class SaveFailureIndex : MapIndex
{
    public string Marker { get; set; }
}
