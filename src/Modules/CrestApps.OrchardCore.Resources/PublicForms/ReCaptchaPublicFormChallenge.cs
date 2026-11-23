using CrestApps.OrchardCore.PublicForms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.ReCaptcha.Configuration;
using OrchardCore.ReCaptcha.Services;

namespace CrestApps.OrchardCore.Resources.PublicForms;

/// <summary>
/// Orchard Core's ReCaptcha as the challenge of every public form, once its keys are set. The form shows Orchard Core's
/// <c>ReCaptcha</c> shape and the post carries the answer in the <c>g-recaptcha-response</c> field.
/// </summary>
internal sealed class ReCaptchaPublicFormChallenge : IPublicFormChallenge, IPublicFormChallengeShape
{
    /// <summary>
    /// The form field the ReCaptcha widget posts its answer in.
    /// </summary>
    public const string ResponseFieldName = "g-recaptcha-response";

    private readonly ReCaptchaService _reCaptcha;
    private readonly IOptionsMonitor<ReCaptchaSettings> _settings;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReCaptchaPublicFormChallenge(
        ReCaptchaService reCaptcha,
        IOptionsMonitor<ReCaptchaSettings> settings,
        IHttpContextAccessor httpContextAccessor)
    {
        _reCaptcha = reCaptcha;
        _settings = settings;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public bool IsConfigured
        => _settings.CurrentValue.ConfigurationExists();

    /// <inheritdoc/>
    public string ShapeType
        => "ReCaptcha";

    /// <inheritdoc/>
    public Task<bool> VerifyAsync()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var token = request?.HasFormContentType == true ? request.Form[ResponseFieldName].ToString() : null;

        return _reCaptcha.VerifyCaptchaResponseAsync(token);
    }
}
