using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Learning.Credentials.Controllers;

/// <summary>
/// The public, anonymous endpoints a wallet or a verifier reads: the credential in JSON-LD and as a signed JSON Web
/// Token, the achievement, the issuer profile, the key set and each key, and the revocation status lists. Each is rate
/// limited when Orchard Core Rate Limits is on; a credential is found only by its unguessable code.
/// </summary>
public sealed class CredentialEndpointsController : Controller
{
    private const string _jsonLd = CredentialsConstants.OpenBadges.JsonLdMediaType + "; charset=utf-8";
    private const string _json = "application/json; charset=utf-8";

    private readonly ICredentialAwardService _awards;
    private readonly ICredentialDefinitionCatalog _definitions;
    private readonly CredentialIssuerContext _issuerContext;
    private readonly CredentialSigningKeys _keys;
    private readonly CredentialStatusLists _statusLists;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialEndpointsController"/> class.
    /// </summary>
    /// <param name="awards">The award service.</param>
    /// <param name="definitions">The definitions.</param>
    /// <param name="issuerContext">The issuer.</param>
    /// <param name="keys">The credentials key set.</param>
    /// <param name="statusLists">The status lists.</param>
    public CredentialEndpointsController(
        ICredentialAwardService awards,
        ICredentialDefinitionCatalog definitions,
        CredentialIssuerContext issuerContext,
        CredentialSigningKeys keys,
        CredentialStatusLists statusLists)
    {
        _awards = awards;
        _definitions = definitions;
        _issuerContext = issuerContext;
        _keys = keys;
        _statusLists = statusLists;
    }

    /// <summary>
    /// The credential in JSON-LD, as it was issued. The address is the credential's <c>id</c>.
    /// </summary>
    /// <param name="publicId">The award's public code.</param>
    /// <returns>The credential.</returns>
    [HttpGet("learning/credentials/awards/{publicId}")]
    [RateLimitGroup(CredentialsConstants.RateLimitGroup)]
    public async Task<IActionResult> Credential(string publicId)
    {
        Secure();

        var award = await _awards.FindByPublicIdAsync(publicId, HttpContext.RequestAborted);

        return string.IsNullOrEmpty(award?.Credential) ? NotFound() : Content(award.Credential, _jsonLd);
    }

    /// <summary>
    /// The credential signed as a JSON Web Token (the Open Badges 3.0 JWT proof format), as it was issued.
    /// </summary>
    /// <param name="publicId">The award's public code.</param>
    /// <returns>The compact JWS.</returns>
    [HttpGet("learning/credentials/awards/{publicId}/jwt")]
    [RateLimitGroup(CredentialsConstants.RateLimitGroup)]
    public async Task<IActionResult> Jwt(string publicId)
    {
        Secure();

        var award = await _awards.FindByPublicIdAsync(publicId, HttpContext.RequestAborted);

        return string.IsNullOrEmpty(award?.Jwt) ? NotFound() : Content(award.Jwt, CredentialsConstants.OpenBadges.JwtMediaType);
    }

    /// <summary>
    /// The achievement of a published credential definition, as it is now. The address is the achievement's <c>id</c>.
    /// </summary>
    /// <param name="definitionId">The content item id of the definition.</param>
    /// <returns>The achievement.</returns>
    [HttpGet("learning/credentials/achievements/{definitionId}")]
    [RateLimitGroup(CredentialsConstants.RateLimitGroup)]
    public async Task<IActionResult> Achievement(string definitionId)
    {
        Secure();

        var issuer = await _issuerContext.GetAsync();
        var definition = issuer is null ? null : await _definitions.FindAsync(definitionId, HttpContext.RequestAborted);

        if (definition?.Achievement is null)
        {
            return NotFound();
        }

        var achievement = new CredentialAchievement
        {
            Id = CredentialUrls.Achievement(issuer.BaseUrl, definition.DefinitionId),
            Name = definition.Achievement.Name,
            Description = definition.Achievement.Description,
            Criteria = definition.Achievement.Criteria,
            ImageUrl = definition.Achievement.ImageUrl,
            AchievementType = definition.Achievement.AchievementType,
            Tags = definition.Achievement.Tags,
            Alignments = definition.Achievement.Alignments,
        };

        return Content(WithContext(OpenBadgeCredentialBuilder.Achievement(achievement)).ToJsonString(), _jsonLd);
    }

    /// <summary>
    /// The issuer profile. The address is the issuer's <c>id</c>.
    /// </summary>
    /// <returns>The profile.</returns>
    [HttpGet("learning/credentials/issuer")]
    [RateLimitGroup(CredentialsConstants.RateLimitGroup)]
    public async Task<IActionResult> Issuer()
    {
        Secure();

        var issuer = await _issuerContext.GetAsync();

        return issuer is null
            ? NotFound()
            : Content(WithContext(OpenBadgeCredentialBuilder.Issuer(issuer.BaseUrl, issuer.Name)).ToJsonString(), _jsonLd);
    }

    /// <summary>
    /// The published public keys of the credentials key set, including retired keys that still verify old credentials.
    /// </summary>
    /// <returns>The JSON Web Key Set.</returns>
    [HttpGet("learning/credentials/jwks")]
    [RateLimitGroup(CredentialsConstants.RateLimitGroup)]
    public async Task<IActionResult> KeySet()
    {
        Secure();

        var keys = new JsonArray();

        foreach (var key in await _keys.GetPublishedKeysAsync(HttpContext.RequestAborted))
        {
            keys.Add(key.DeepClone());
        }

        return Content(new JsonObject { ["keys"] = keys }.ToJsonString(), _json);
    }

    /// <summary>
    /// One public key, the address a token's <c>kid</c> names.
    /// </summary>
    /// <param name="kid">The key id.</param>
    /// <returns>The JSON Web Key.</returns>
    [HttpGet("learning/credentials/keys/{kid}")]
    [RateLimitGroup(CredentialsConstants.RateLimitGroup)]
    public async Task<IActionResult> Key(string kid)
    {
        Secure();

        var key = await _keys.FindPublishedKeyAsync(kid, HttpContext.RequestAborted);

        return key is null ? NotFound() : Content(key.ToJsonString(), _json);
    }

    /// <summary>
    /// A revocation status list: a Bitstring Status List credential in JSON-LD, or signed as a JSON Web Token when the
    /// request accepts <c>application/vc+jwt</c> or <c>application/jwt</c>. It is computed from the awards each time.
    /// </summary>
    /// <param name="listNumber">The number of the list.</param>
    /// <returns>The status list credential.</returns>
    [HttpGet("learning/credentials/status/{listNumber:int}")]
    [RateLimitGroup(CredentialsConstants.RateLimitGroup)]
    public Task<IActionResult> StatusList(int listNumber)
    {
        var accept = Request.Headers.Accept.ToString();
        var wantsJwt = accept.Contains("application/vc+jwt", StringComparison.OrdinalIgnoreCase) ||
            accept.Contains(CredentialsConstants.OpenBadges.JwtMediaType, StringComparison.OrdinalIgnoreCase);

        return StatusListCoreAsync(listNumber, wantsJwt);
    }

    /// <summary>
    /// A revocation status list signed as a JSON Web Token.
    /// </summary>
    /// <param name="listNumber">The number of the list.</param>
    /// <returns>The compact JWS.</returns>
    [HttpGet("learning/credentials/status/{listNumber:int}/jwt")]
    [RateLimitGroup(CredentialsConstants.RateLimitGroup)]
    public Task<IActionResult> StatusListJwt(int listNumber)
        => StatusListCoreAsync(listNumber, jwt: true);

    private async Task<IActionResult> StatusListCoreAsync(int listNumber, bool jwt)
    {
        Secure();

        // A revocation shows at once: no cache keeps the list.
        Response.Headers.CacheControl = "no-store";

        var issuer = await _issuerContext.GetAsync();

        if (issuer is null || !await _statusLists.ExistsAsync(listNumber, HttpContext.RequestAborted))
        {
            return NotFound();
        }

        return jwt
            ? Content(await _statusLists.SignAsync(listNumber, issuer.BaseUrl, HttpContext.RequestAborted), CredentialsConstants.OpenBadges.JwtMediaType)
            : Content((await _statusLists.BuildAsync(listNumber, issuer.BaseUrl, HttpContext.RequestAborted)).ToJsonString(), _jsonLd);
    }

    private void Secure()
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers.AccessControlAllowOrigin = "*";
    }

    private static JsonObject WithContext(JsonObject value)
    {
        var result = new JsonObject
        {
            ["@context"] = new JsonArray(CredentialsConstants.OpenBadges.VerifiableCredentialsContext, CredentialsConstants.OpenBadges.OpenBadgesContext),
        };

        foreach (var (name, node) in value)
        {
            result[name] = node?.DeepClone();
        }

        return result;
    }
}
