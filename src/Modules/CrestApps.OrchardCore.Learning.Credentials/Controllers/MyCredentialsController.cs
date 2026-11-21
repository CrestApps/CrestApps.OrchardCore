using System.Security.Claims;
using System.Text;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Credentials.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Credentials.Controllers;

/// <summary>
/// My credentials: the learner's own awards, with downloads of the credential, the share link and the choice to show
/// their name on the public page.
/// </summary>
[Authorize]
public sealed class MyCredentialsController : Controller
{
    private readonly ICredentialAwardService _awards;
    private readonly CredentialIssuerContext _issuerContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyCredentialsController"/> class.
    /// </summary>
    /// <param name="awards">The award service.</param>
    /// <param name="issuerContext">The issuer.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="clock">The clock.</param>
    public MyCredentialsController(
        ICredentialAwardService awards,
        CredentialIssuerContext issuerContext,
        IAuthorizationService authorizationService,
        IClock clock)
    {
        _awards = awards;
        _issuerContext = issuerContext;
        _authorizationService = authorizationService;
        _clock = clock;
    }

    /// <summary>
    /// The learner's credentials.
    /// </summary>
    /// <returns>The page.</returns>
    [HttpGet("learning/credentials", Name = "LearningMyCredentials")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.AccessLearning))
        {
            return Forbid();
        }

        var issuer = await _issuerContext.GetAsync();
        var model = new MyCredentialsViewModel();

        foreach (var award in await _awards.ListForUserAsync(UserId(), HttpContext.RequestAborted))
        {
            model.Entries.Add(new MyCredentialEntry
            {
                Award = award,
                Status = award.GetStatus(_clock.UtcNow),
                ShareUrl = issuer is null ? null : CredentialUrls.Verify(issuer.BaseUrl, award.PublicId),
            });
        }

        return View(model);
    }

    /// <summary>
    /// Downloads one of the learner's credentials in JSON-LD or as the signed JSON Web Token.
    /// </summary>
    /// <param name="id">The award id.</param>
    /// <param name="format"><c>json</c> or <c>jwt</c>.</param>
    /// <returns>The file.</returns>
    [HttpGet("learning/credentials/mine/{id}/{format}")]
    public async Task<IActionResult> Download(string id, string format)
    {
        var award = await OwnAwardAsync(id);

        if (award is null || string.IsNullOrEmpty(award.Credential) || format is not ("json" or "jwt"))
        {
            return NotFound();
        }

        var name = FileName(award.Achievement?.Name);

        Response.Headers.XContentTypeOptions = "nosniff";

        return format == "json"
            ? File(Encoding.UTF8.GetBytes(award.Credential), CredentialsConstants.OpenBadges.JsonLdMediaType, name + ".json")
            : File(Encoding.ASCII.GetBytes(award.Jwt), CredentialsConstants.OpenBadges.JwtMediaType, name + ".jwt");
    }

    /// <summary>
    /// Sets whether the learner's name shows on the public verification page of one of their credentials.
    /// </summary>
    /// <param name="id">The award id.</param>
    /// <param name="showName">Whether the name shows.</param>
    /// <returns>My credentials.</returns>
    [HttpPost("learning/credentials/mine/{id}/show-name")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ShowName(string id, bool showName)
    {
        if (await OwnAwardAsync(id) is null)
        {
            return NotFound();
        }

        await _awards.SetShowNameAsync(id, UserId(), showName, HttpContext.RequestAborted);

        return RedirectToRoute("LearningMyCredentials");
    }

    private async Task<CredentialAward> OwnAwardAsync(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.AccessLearning))
        {
            return null;
        }

        var award = await _awards.FindByIdAsync(id, HttpContext.RequestAborted);
        var userId = UserId();

        return award is not null && !string.IsNullOrEmpty(userId) && string.Equals(award.RecipientUserId, userId, StringComparison.Ordinal)
            ? award
            : null;
    }

    private string UserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static string FileName(string name)
    {
        var builder = new StringBuilder();

        foreach (var character in name ?? string.Empty)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        var result = builder.ToString().Trim('-');

        return string.IsNullOrEmpty(result) ? "credential" : result;
    }
}
