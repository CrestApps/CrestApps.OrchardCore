using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Wizard.Services;

/// <summary>
/// Manages the data-protected cookie that maps a wizard key to a resumable wizard session identifier, so a
/// returning visitor resumes the same pending session instead of orphaning a new one on every visit. Unlike
/// a raw JSON cookie, the payload is encrypted and integrity-protected so a visitor cannot forge a session
/// id for another wizard.
/// </summary>
public sealed class WizardResumeCookieManager
{
    private const string CookieName = "wizard_sessions";
    private const string ProtectorPurpose = "CrestApps.OrchardCore.Wizard.Resume.v1";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtector _dataProtector;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardResumeCookieManager"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read and write the cookie.</param>
    /// <param name="dataProtectionProvider">The provider used to encrypt and integrity-protect the cookie.</param>
    public WizardResumeCookieManager(
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _dataProtector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    /// <summary>
    /// Adds or updates the stored session identifier for a wizard key.
    /// </summary>
    /// <param name="wizardKey">The wizard key (the wizard type, optionally qualified by definition id).</param>
    /// <param name="sessionId">The wizard session identifier to store.</param>
    public void Append(string wizardKey, string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(wizardKey);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var values = GetValues();

        values[wizardKey] = sessionId;

        SetValue(values);
    }

    /// <summary>
    /// Attempts to get the stored session identifier for a wizard key.
    /// </summary>
    /// <param name="wizardKey">The wizard key.</param>
    /// <param name="sessionId">When this method returns, contains the stored session identifier when found.</param>
    /// <returns><see langword="true"/> when a stored session identifier exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(string wizardKey, out string sessionId)
    {
        if (wizardKey != null)
        {
            return GetValues().TryGetValue(wizardKey, out sessionId);
        }

        sessionId = null;

        return false;
    }

    /// <summary>
    /// Removes the stored session identifier for a wizard key.
    /// </summary>
    /// <param name="wizardKey">The wizard key to remove.</param>
    public void Remove(string wizardKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(wizardKey);

        var values = GetValues();

        if (values.Remove(wizardKey))
        {
            SetValue(values);
        }
    }

    private void SetValue(Dictionary<string, string> values)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        httpContext.Response.Cookies.Delete(CookieName);

        if (values.Count > 0)
        {
            var protectedValue = _dataProtector.Protect(JsonSerializer.Serialize(values));

            httpContext.Response.Cookies.Append(CookieName, protectedValue, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
            });
        }
    }

    private Dictionary<string, string> GetValues()
    {
        if (_httpContextAccessor.HttpContext.Request.Cookies.TryGetValue(CookieName, out var value))
        {
            try
            {
                var unprotected = _dataProtector.Unprotect(value);

                return JsonSerializer.Deserialize<Dictionary<string, string>>(unprotected) ?? [];
            }
            catch
            {
                // A tampered, expired, or key-rotated cookie is treated as absent.
            }
        }

        return [];
    }
}
