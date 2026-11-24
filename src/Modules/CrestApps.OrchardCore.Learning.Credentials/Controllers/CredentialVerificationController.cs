using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Credentials.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Learning.Credentials.Controllers;

/// <summary>
/// The public page that proves a credential from its code: it checks the signature, the expiry and the revocation
/// status on the server and shows the result, the achievement and the issuer, and the recipient's name only when the
/// recipient chose to show it.
/// </summary>
public sealed class CredentialVerificationController : Controller
{
    private readonly ICredentialAwardService _awards;
    private readonly CredentialVerifier _verifier;
    private readonly CredentialRecipientResolver _recipients;
    private readonly CredentialIssuerContext _issuerContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialVerificationController"/> class.
    /// </summary>
    /// <param name="awards">The award service.</param>
    /// <param name="verifier">The verifier.</param>
    /// <param name="recipients">The recipients, for the name the recipient chose to show.</param>
    /// <param name="issuerContext">The issuer.</param>
    public CredentialVerificationController(
        ICredentialAwardService awards,
        CredentialVerifier verifier,
        CredentialRecipientResolver recipients,
        CredentialIssuerContext issuerContext)
    {
        _awards = awards;
        _verifier = verifier;
        _recipients = recipients;
        _issuerContext = issuerContext;
    }

    /// <summary>
    /// Shows the verification of a credential.
    /// </summary>
    /// <param name="publicId">The award's public code.</param>
    /// <returns>The page.</returns>
    [HttpGet("learning/credentials/verify/{publicId}")]
    [RateLimitGroup(CredentialsConstants.RateLimitGroup)]
    public async Task<IActionResult> Index(string publicId)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers.XContentTypeOptions = "nosniff";

        var award = await _awards.FindByPublicIdAsync(publicId, HttpContext.RequestAborted);
        var result = await _verifier.VerifyAsync(award, HttpContext.RequestAborted);
        var model = new CredentialVerificationViewModel { Outcome = result.Outcome };

        if (award is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;

            return View(model);
        }

        var issued = string.IsNullOrEmpty(award.Credential) ? null : JsonNode.Parse(award.Credential);
        var issuer = await _issuerContext.GetAsync();

        model.SignatureValid = result.SignatureValid;
        model.Erased = result.Erased;
        model.AchievementName = award.Achievement?.Name;
        model.AchievementDescription = award.Achievement?.Description;
        model.Criteria = award.Achievement?.Criteria;
        model.ImageUrl = award.Achievement?.ImageUrl;
        model.AchievementType = award.Achievement?.AchievementType;
        model.IssuerName = issued?["issuer"]?["name"]?.GetValue<string>() ?? issuer?.Name;
        model.IssuedUtc = DateTime.SpecifyKind(award.IssuedUtc, DateTimeKind.Utc);
        model.ValidUntilUtc = result.ValidUntilUtc ?? (award.ExpiresUtc is { } expires ? DateTime.SpecifyKind(expires, DateTimeKind.Utc) : null);
        model.RevokedUtc = result.Revoked && award.RevokedUtc is { } revoked ? DateTime.SpecifyKind(revoked, DateTimeKind.Utc) : null;
        model.RecipientName = award.ShowName && !result.Erased ? await _recipients.GetDisplayNameAsync(award.RecipientUserId) : null;

        if (!result.Erased && issuer is not null)
        {
            model.CredentialUrl = CredentialUrls.Credential(issuer.BaseUrl, award.PublicId);
            model.JwtUrl = CredentialUrls.CredentialJwt(issuer.BaseUrl, award.PublicId);
            model.KeySetUrl = CredentialUrls.KeySet(issuer.BaseUrl);
        }

        return View(model);
    }
}
