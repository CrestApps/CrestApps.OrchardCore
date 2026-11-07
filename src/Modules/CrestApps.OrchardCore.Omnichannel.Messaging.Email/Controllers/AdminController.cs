using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Controllers;

/// <summary>
/// The email address editor's connection test: checks the SMTP or IMAP settings as typed, before they are saved.
/// </summary>
public sealed class AdminController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IEmailConnectionTester _connectionTester;
    private readonly IEmailSecretProtector _secretProtector;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="endpointManager">The manager of the business's addresses, for the stored password.</param>
    /// <param name="connectionTester">The connection tester.</param>
    /// <param name="secretProtector">The protector a typed password is protected with for the test.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminController(
        IAuthorizationService authorizationService,
        IOmnichannelChannelEndpointManager endpointManager,
        IEmailConnectionTester connectionTester,
        IEmailSecretProtector secretProtector,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _endpointManager = endpointManager;
        _connectionTester = connectionTester;
        _secretProtector = secretProtector;
        S = stringLocalizer;
    }

    /// <summary>
    /// Tests the SMTP or IMAP settings posted from the address editor.
    /// </summary>
    /// <param name="kind">Which connection to test: <c>smtp</c> or <c>imap</c>.</param>
    /// <param name="addressId">The address being edited, whose stored password is used when none was typed.</param>
    /// <param name="host">The server host.</param>
    /// <param name="port">The server port; zero for the default.</param>
    /// <param name="security">How the connection is secured.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password as typed; empty to use the stored one.</param>
    /// <param name="folder">The mailbox folder, for an IMAP test.</param>
    /// <returns>Whether the test succeeded, and a message to show.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("omnichannel/email/test-connection", "EmailAddressTestConnection")]
    public async Task<IActionResult> TestConnection(
        string kind,
        string addressId,
        string host,
        int port,
        EmailConnectionSecurity security,
        string userName,
        string password,
        string folder)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbid();
        }

        var isImap = string.Equals(kind, "imap", StringComparison.OrdinalIgnoreCase);

        var server = new EmailServerSettings
        {
            Host = host?.Trim(),
            Port = Math.Clamp(port, 0, 65535),
            Security = security,
            UserName = userName?.Trim(),
            Password = string.IsNullOrEmpty(password)
                ? await GetStoredPasswordAsync(addressId, isImap)
                : _secretProtector.Protect(password),
        };

        var error = isImap
            ? await _connectionTester.TestImapAsync(new EmailMailboxSettings { Server = server, Folder = folder }, HttpContext.RequestAborted)
            : await _connectionTester.TestSmtpAsync(server, HttpContext.RequestAborted);

        return Json(new
        {
            succeeded = error is null,
            message = error ?? (isImap ? S["The mailbox opened successfully."].Value : S["The SMTP server accepted the sign-in."].Value),
        });
    }

    private async Task<string> GetStoredPasswordAsync(string addressId, bool isImap)
    {
        if (string.IsNullOrEmpty(addressId))
        {
            return null;
        }

        var address = await _endpointManager.FindByIdAsync(addressId);

        if (address is null || !address.TryGet<EmailAddressSettings>(out var settings))
        {
            return null;
        }

        return isImap
            ? settings.Mailbox?.Server?.Password
            : settings.Smtp?.Password;
    }
}
