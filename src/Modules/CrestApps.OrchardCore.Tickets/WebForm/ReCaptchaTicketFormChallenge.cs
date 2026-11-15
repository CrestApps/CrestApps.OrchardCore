using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.ReCaptcha.Configuration;
using OrchardCore.ReCaptcha.Services;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// Orchard Core's ReCaptcha as the ticket web form's challenge, once its keys are set.
/// </summary>
internal sealed class ReCaptchaTicketFormChallenge : ITicketFormChallenge
{
    private readonly ReCaptchaService _reCaptcha;
    private readonly IOptionsMonitor<ReCaptchaSettings> _settings;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReCaptchaTicketFormChallenge(ReCaptchaService reCaptcha, IOptionsMonitor<ReCaptchaSettings> settings, IHttpContextAccessor httpContextAccessor)
    {
        _reCaptcha = reCaptcha;
        _settings = settings;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsConfigured
        => _settings.CurrentValue.ConfigurationExists();

    public Task<bool> VerifyAsync()
    {
        var form = _httpContextAccessor.HttpContext?.Request;
        var token = form?.HasFormContentType == true ? form.Form["g-recaptcha-response"].ToString() : null;

        return _reCaptcha.VerifyCaptchaResponseAsync(token);
    }
}
