using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Registers the soft-phone dialer script (the "call" button next to phone-number fields) on a page only when a
/// <c>PhoneField</c> actually renders there.
/// <para>
/// Whether the soft phone should be present at all - and the injection of the floating soft phone widget itself -
/// stays with <see cref="Filters.SoftPhoneWidgetFilter"/>, which must run on every admin page (calls arrive with or
/// without a phone field on screen). That filter sets <see cref="SoftPhonePresentRequestKey"/> when it registers the
/// widget; this provider then adds the small dialer enhancement only where a phone field is shown. The result: the
/// dialer never loads on pages with no phone field, and the call button never appears where the widget is not present
/// to place the call.
/// </para>
/// </summary>
internal sealed class PhoneFieldDialerShapeTableProvider : IShapeTableProvider
{
    /// <summary>
    /// Per-request flag set by <see cref="Filters.SoftPhoneWidgetFilter"/> once the soft phone widget and its
    /// resources have been registered for the current request.
    /// </summary>
    public const string SoftPhonePresentRequestKey = "CrestApps.Telephony.SoftPhonePresent";

    private const string DialerRegisteredRequestKey = "CrestApps.Telephony.PhoneFieldDialerRegistered";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public PhoneFieldDialerShapeTableProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("PhoneField").OnDisplaying(_ => RegisterDialer());
        builder.Describe("PhoneField_Edit").OnDisplaying(_ => RegisterDialer());

        return ValueTask.CompletedTask;
    }

    private void RegisterDialer()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        // Only add the dialer when the soft phone widget is present on this request.
        if (httpContext is null || httpContext.Items[SoftPhonePresentRequestKey] is not true)
        {
            return;
        }

        // The page may hold several phone fields; register the dialer script once.
        if (httpContext.Items.ContainsKey(DialerRegisteredRequestKey))
        {
            return;
        }

        httpContext.Items[DialerRegisteredRequestKey] = true;

        httpContext.RequestServices
            .GetRequiredService<IResourceManager>()
            .RegisterResource("script", "telephony-phone-field")
            .AtFoot();
    }
}
