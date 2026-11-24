using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Zones;

namespace CrestApps.OrchardCore.KnowledgeBase.Services;

/// <summary>
/// Reads the help center's zones of the page's layout, which the help center's page and an article's page show
/// themselves, so their widgets show on any theme.
/// </summary>
public static class KnowledgeBaseZones
{
    /// <summary>
    /// Gets a zone of the layout when widgets were put in it.
    /// </summary>
    /// <param name="layout">The page's layout.</param>
    /// <param name="name">The zone.</param>
    /// <returns>The zone, or <see langword="null"/> when it is empty.</returns>
    public static IShape Find(object layout, string name)
    {
        if (layout is not IZoneHolding holding)
        {
            return null;
        }

        var zone = holding.Zones[name];

        return zone is Shape shape && shape.Items.Count > 0 ? zone : null;
    }
}
