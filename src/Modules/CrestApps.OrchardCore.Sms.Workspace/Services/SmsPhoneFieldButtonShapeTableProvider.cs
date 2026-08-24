using System.Text.Json;
using CrestApps.OrchardCore.Sms.Workspace.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Sms.Workspace.Services;

/// <summary>
/// Adds a "Send SMS" button next to phone-number fields on admin pages, mirroring the Telephony soft-phone dial
/// button (and its <c>PhoneFieldDialerShapeTableProvider</c>). The button reuses the phone field's
/// <c>[data-phone-dial]</c> placeholder and opens the SMS Workspace on the field's number, so an operator can start
/// (or resume) a conversation straight from the number they are viewing.
/// <para>
/// The enhancement is registered from the phone field's own rendering rather than a global MVC filter, so only pages
/// that actually render a phone field pay for it, and only for users allowed to use the workspace. The script is
/// registered once per request even when the page holds several phone fields.
/// </para>
/// </summary>
internal sealed class SmsPhoneFieldButtonShapeTableProvider : IShapeTableProvider
{
    private const string EvaluatedRequestKey = "CrestApps.SmsWorkspace.PhoneFieldButtonEvaluated";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public SmsPhoneFieldButtonShapeTableProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("PhoneField").OnDisplaying(_ => RegisterButton());
        builder.Describe("PhoneField_Edit").OnDisplaying(_ => RegisterButton());

        return ValueTask.CompletedTask;
    }

    private void RegisterButton()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null ||
            httpContext.User.Identity?.IsAuthenticated != true ||
            !AdminAttribute.IsApplied(httpContext) ||
            httpContext.Items.ContainsKey(EvaluatedRequestKey))
        {
            return;
        }

        // Evaluate at most once per request, even when the page holds several phone fields or the user is not
        // authorized.
        httpContext.Items[EvaluatedRequestKey] = true;

        var services = httpContext.RequestServices;

        // OnDisplaying is synchronous. The SMS Workspace permission is evaluated by the standard permission handler,
        // which reads the already-loaded principal and completes synchronously (no I/O), so this does not block.
        var authorizeTask = services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(httpContext.User, SmsWorkspacePermissions.UseSmsPortal);

        var authorized = authorizeTask.IsCompletedSuccessfully
            ? authorizeTask.Result
            : authorizeTask.GetAwaiter().GetResult();

        if (!authorized)
        {
            return;
        }

        var startUrl = services.GetRequiredService<LinkGenerator>()
            .GetPathByName(httpContext, "SmsPortalStart", values: null);

        if (string.IsNullOrEmpty(startUrl))
        {
            return;
        }

        services.GetRequiredService<IResourceManager>()
            .RegisterFootScript(new HtmlString(BuildScript(startUrl)));
    }

    private static string BuildScript(string startUrl)
    {
        var startUrlLiteral = JsonSerializer.Serialize(startUrl);

        return $$"""
            <script>
            (function () {
                'use strict';
                var startUrl = {{startUrlLiteral}};

                function getNumber(placeholder) {
                    if (placeholder.hasAttribute('data-phone-number')) {
                        return (placeholder.getAttribute('data-phone-number') || '').trim();
                    }
                    var field = placeholder.closest('[data-phone-field]');
                    if (field) {
                        var e164 = field.querySelector('[data-phone-e164]');
                        if (e164 && e164.value) { return e164.value.trim(); }
                        var tel = field.querySelector('input[type="tel"]');
                        if (tel && tel.value) { return tel.value.trim(); }
                    }
                    return '';
                }

                function enhance(placeholder) {
                    if (placeholder.__smsWorkspaceEnhanced) { return; }
                    placeholder.__smsWorkspaceEnhanced = true;

                    var button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'btn btn-sm btn-outline-primary sms-workspace-sms-btn ms-1';
                    button.title = 'Send SMS';
                    button.innerHTML = '<i class="fa-solid fa-comment-sms"></i>';

                    button.addEventListener('click', function () {
                        var number = getNumber(placeholder);
                        if (!number) { return; }
                        var sep = startUrl.indexOf('?') >= 0 ? '&' : '?';
                        window.open(startUrl + sep + 'number=' + encodeURIComponent(number), '_blank', 'noopener');
                    });

                    placeholder.appendChild(button);
                }

                function enhanceAll() {
                    Array.prototype.forEach.call(document.querySelectorAll('[data-phone-dial]'), enhance);
                }

                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', enhanceAll);
                } else {
                    enhanceAll();
                }
            })();
            </script>
            """;
    }
}
