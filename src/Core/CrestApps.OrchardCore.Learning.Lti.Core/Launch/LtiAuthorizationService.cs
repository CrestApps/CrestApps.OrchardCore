using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// The platform's authorization endpoint of the third-party initiated login (Security Framework 5.1.1.2 and 5.1.1.3):
/// checks the tool's authentication request and answers with an id token carrying the LTI claims, signed with the
/// platform key, for the signed-in person the message hint was made for.
/// </summary>
/// <remarks>
/// The order of the checks decides where an error may go: until the client and its redirect URI are known, nothing is
/// posted anywhere (the error is shown to the person); after that, errors are posted to the redirect URI with the state.
/// </remarks>
public sealed class LtiAuthorizationService
{
    private readonly ILtiToolDirectory _tools;
    private readonly LtiMessageHints _hints;
    private readonly LtiNonceStore _nonces;
    private readonly ILtiLaunchContextFactory _contexts;
    private readonly ILtiPlatform _platform;
    private readonly LtiPlatformKeys _keys;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiAuthorizationService"/> class.
    /// </summary>
    /// <param name="tools">The tool directory.</param>
    /// <param name="hints">The message hints.</param>
    /// <param name="nonces">The nonce store.</param>
    /// <param name="contexts">The launch context factory.</param>
    /// <param name="platform">The platform.</param>
    /// <param name="keys">The platform keys.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public LtiAuthorizationService(
        ILtiToolDirectory tools,
        LtiMessageHints hints,
        LtiNonceStore nonces,
        ILtiLaunchContextFactory contexts,
        ILtiPlatform platform,
        LtiPlatformKeys keys,
        IClock clock,
        ILogger<LtiAuthorizationService> logger)
    {
        _tools = tools;
        _hints = hints;
        _nonces = nonces;
        _contexts = contexts;
        _platform = platform;
        _keys = keys;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Answers an authentication request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="userId">The user id of the signed-in person, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The answer.</returns>
    public async Task<LtiAuthorizationResult> AuthorizeAsync(LtiAuthorizationRequest request, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tool = await _tools.FindByClientIdAsync(request.ClientId, cancellationToken);

        if (tool is null || !tool.Enabled)
        {
            return Refuse(null, "unauthorized_client", "The client is not a registered external tool, or the tool is turned off.");
        }

        if (!await _tools.IsRedirectUriAllowedAsync(tool.ClientId, request.RedirectUri, cancellationToken))
        {
            return Refuse(null, "invalid_request", "The redirect URI is not one the tool registered.");
        }

        var redirect = new LtiAuthorizationResult { RedirectUri = request.RedirectUri, State = request.State };

        if (!string.Equals(request.Scope, "openid", StringComparison.Ordinal) ||
            !string.Equals(request.ResponseType, "id_token", StringComparison.Ordinal) ||
            !string.Equals(request.ResponseMode, "form_post", StringComparison.Ordinal) ||
            !string.Equals(request.Prompt, "none", StringComparison.Ordinal))
        {
            return Refuse(redirect, "invalid_request", "An LTI login asks for scope openid, response_type id_token, response_mode form_post and prompt none.");
        }

        if (string.IsNullOrEmpty(request.Nonce))
        {
            return Refuse(redirect, "invalid_request", "The nonce is required.");
        }

        if (string.IsNullOrEmpty(userId))
        {
            return Refuse(redirect, "login_required", "Nobody is signed in to the platform.");
        }

        var hint = _hints.Read(request.LtiMessageHint);

        if (hint is null || !string.Equals(hint.ToolId, tool.ItemId, StringComparison.Ordinal))
        {
            return Refuse(redirect, "invalid_request", "The message hint is not valid for this tool, or it expired.");
        }

        if (!string.Equals(hint.UserId, userId, StringComparison.Ordinal))
        {
            return Refuse(redirect, "login_required", "The launch was started by someone other than the signed-in person.");
        }

        if (!string.Equals(request.LoginHint, LtiSubjects.For(tool, userId), StringComparison.Ordinal))
        {
            return Refuse(redirect, "invalid_request", "The login hint is not the signed-in person's.");
        }

        if (!await _nonces.TryUseAsync("authorize:" + tool.ClientId, request.Nonce, cancellationToken))
        {
            return Refuse(redirect, "invalid_request", "The nonce was used before.");
        }

        // The key first: making the platform's first key saves a secret in a scope of its own, which must not wait behind
        // this request's own writes (the line item the context may make).
        var credentials = await _keys.GetSigningCredentialsAsync(cancellationToken);
        var context = await _contexts.CreateAsync(hint, tool, cancellationToken);

        if (context is null)
        {
            return Refuse(redirect, "invalid_request", "The course item or the course of the launch is no longer available.");
        }

        var platform = await _platform.GetAsync();
        var claims = LtiClaims.Build(context, tool, platform, request.Nonce, _clock.UtcNow);

        redirect.IdToken = LtiTokens.Sign(claims, credentials);

        return redirect;
    }

    private LtiAuthorizationResult Refuse(LtiAuthorizationResult redirect, string error, string description)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("An LTI login was refused: {Error}. {Description}", error, description);
        }

        var result = redirect ?? new LtiAuthorizationResult();

        result.Error = error;
        result.ErrorDescription = description;
        result.IdToken = null;

        return result;
    }
}
