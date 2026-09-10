using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Builds the soft phone widget model from the active telephony provider and the site-wide soft phone
/// settings, and registers the styles and scripts the widget needs. It is shared by the admin auto-inject
/// filter and the placeable front-end widget so both render an identical soft phone.
/// </summary>
public interface ISoftPhoneWidgetPresenter
{
    /// <summary>
    /// Builds the soft phone widget model. The provider capabilities and audio mode are resolved from the
    /// active telephony provider; the accent color, recent-calls count, and default country come from the
    /// site-wide soft phone settings.
    /// </summary>
    /// <returns>The populated soft phone widget model.</returns>
    Task<SoftPhoneWidget> CreateWidgetAsync();

    /// <summary>
    /// Registers the soft phone styles and scripts (including the provider-specific browser media library)
    /// on the current request. This must run before the response head is rendered, so it lives on the
    /// request pipeline rather than in the widget shape, which renders after the head.
    /// </summary>
    /// <param name="widget">The widget model whose audio mode selects the browser media library.</param>
    void RegisterResources(SoftPhoneWidget widget);
}
