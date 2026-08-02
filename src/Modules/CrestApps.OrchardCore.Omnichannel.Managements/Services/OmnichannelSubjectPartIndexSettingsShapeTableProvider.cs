using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Hides the Azure AI Search and Elasticsearch content index part settings that Orchard Core injects globally
/// into every content type part editor, but only while editing the <c>OmnichannelSubjectPart</c> settings. Those
/// index settings are managed automatically for omnichannel subjects, so they should not be configurable by hand.
/// </summary>
internal sealed class OmnichannelSubjectPartIndexSettingsShapeTableProvider : IShapeTableProvider
{
    private static readonly PlacementInfo _hidden = PlacementInfo.FromLocation(PlacementInfo.HiddenLocation);

    private static readonly string[] _shapeTypes =
    [
        "AzureAISearchContentIndexSettings_Edit",
        "ElasticContentIndexSettings_Edit",
    ];

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectPartIndexSettingsShapeTableProvider"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor used to inspect the current request route values.</param>
    public OmnichannelSubjectPartIndexSettingsShapeTableProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        foreach (var shapeType in _shapeTypes)
        {
            builder.Describe(shapeType)
                .Placement(IsSubjectPartSettingsEditor, _hidden);
        }

        return ValueTask.CompletedTask;
    }

    private bool IsSubjectPartSettingsEditor(ShapePlacementContext context)
    {
        var routeValues = _httpContextAccessor.HttpContext?.Request.RouteValues;

        if (routeValues is null)
        {
            return false;
        }

        return string.Equals(GetRouteValue(routeValues, "area"), "OrchardCore.ContentTypes", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetRouteValue(routeValues, "name"), OmnichannelConstants.ContentParts.OmnichannelSubject, StringComparison.Ordinal);
    }

    private static string GetRouteValue(IReadOnlyDictionary<string, object> routeValues, string key)
        => routeValues.TryGetValue(key, out var value) ? value as string : null;
}
