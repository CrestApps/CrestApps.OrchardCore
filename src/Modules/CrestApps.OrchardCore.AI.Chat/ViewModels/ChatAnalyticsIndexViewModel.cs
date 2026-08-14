using CrestApps.OrchardCore.AI.Chat.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Main view model for the analytics index page.
/// </summary>
public class ChatAnalyticsIndexViewModel
{
    /// <summary>
    /// The filter shape rendered by display drivers.
    /// </summary>
    public IShape FilterShape { get; set; }

    /// <summary>
    /// The report shape rendered by display drivers.
    /// </summary>
    public IShape ReportShape { get; set; }

    /// <summary>
    /// The filter model with accumulated conditions.
    /// </summary>
    public AIChatAnalyticsFilter Filter { get; set; }

    /// <summary>
    /// Whether to show the report (true after form submission).
    /// </summary>
    public bool ShowReport { get; set; }
}
