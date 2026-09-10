using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Telephony.Filters;

/// <summary>
/// Signals that a soft phone is available to place calls when the browser-extension soft phone is the active
/// surface (the in-page widget feature is off). It sets the same per-request flag the widget filter sets, so the
/// <see cref="PhoneFieldDialerShapeTableProvider"/> renders the phone-field "call" button on admin pages even
/// without the floating widget. The extension is present on every admin page, so no widget needs to render here;
/// authorization and the admin-page check mirror the widget filter's gating.
/// </summary>
public sealed class SoftPhoneExtensionDialerFilter : IAsyncResultFilter
{
    private readonly IAuthorizationService _authorizationService;
    private readonly AdminOptions _adminOptions;

    public SoftPhoneExtensionDialerFilter(
        IAuthorizationService authorizationService,
        IOptions<AdminOptions> adminOptions)
    {
        _authorizationService = authorizationService;
        _adminOptions = adminOptions.Value;
    }

    /// <inheritdoc/>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is (ViewResult or PageResult) &&
            context.HttpContext.User.Identity?.IsAuthenticated == true &&
            IsAdminPage(context) &&
            await _authorizationService.AuthorizeAsync(context.HttpContext.User, TelephonyPermissions.UseSoftPhone))
        {
            context.HttpContext.Items[PhoneFieldDialerShapeTableProvider.SoftPhonePresentRequestKey] = true;
        }

        await next();
    }

    private bool IsAdminPage(ResultExecutingContext context)
        => context.HttpContext.Request.Path.StartsWithSegments('/' + _adminOptions.AdminUrlPrefix, StringComparison.OrdinalIgnoreCase);
}
