using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Media;
using OrchardCore.Media.Fields;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Reads the published credential definitions from their content items.
/// </summary>
internal sealed class ContentCredentialDefinitionCatalog : ICredentialDefinitionCatalog
{
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly IMediaFileStore _mediaFileStore;
    private readonly CredentialIssuerContext _issuerContext;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentCredentialDefinitionCatalog"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="session">The session.</param>
    /// <param name="mediaFileStore">The media file store, for the image address.</param>
    /// <param name="issuerContext">The issuer, for absolute addresses.</param>
    /// <param name="services">The services, for the Skills managers when Skills is on.</param>
    public ContentCredentialDefinitionCatalog(
        IContentManager contentManager,
        ISession session,
        IMediaFileStore mediaFileStore,
        CredentialIssuerContext issuerContext,
        IServiceProvider services)
    {
        _contentManager = contentManager;
        _session = session;
        _mediaFileStore = mediaFileStore;
        _issuerContext = issuerContext;
        _services = services;
    }

    /// <inheritdoc/>
    public async Task<CredentialDefinition> FindAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(definitionId))
        {
            return null;
        }

        var item = await _contentManager.GetAsync(definitionId, VersionOptions.Published);

        return item?.ContentType == CredentialsConstants.ContentTypes.CredentialDefinition
            ? await MapAsync(item)
            : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CredentialDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        var definitions = new List<CredentialDefinition>();

        foreach (var item in await ListItemsAsync(cancellationToken))
        {
            definitions.Add(await MapAsync(item));
        }

        return definitions;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CredentialDefinition>> ListByRuleAsync(string kind, string targetId, CancellationToken cancellationToken = default)
    {
        var definitions = new List<CredentialDefinition>();

        foreach (var item in await ListItemsAsync(cancellationToken))
        {
            var rules = (item.TryGet<CredentialDefinitionPart>(out var definitionPart) ? definitionPart.Rules : null) ?? [];

            if (rules.Any(rule => rule.Kind == kind && rule.TargetId == targetId))
            {
                definitions.Add(await MapAsync(item));
            }
        }

        return definitions;
    }

    private async Task<IEnumerable<ContentItem>> ListItemsAsync(CancellationToken cancellationToken)
        => await _session.Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == CredentialsConstants.ContentTypes.CredentialDefinition && index.Published)
            .ListAsync(cancellationToken);

    private async Task<CredentialDefinition> MapAsync(ContentItem item)
    {
        var part = item.TryGet<CredentialDefinitionPart>(out var found) ? found : new CredentialDefinitionPart();
        var issuer = await _issuerContext.GetAsync();
        var alignments = (part.Alignments ?? []).ToList();

        if (issuer is not null)
        {
            await AddSkillsAlignmentsAsync(part, CredentialUrls.Achievement(issuer.BaseUrl, item.ContentItemId), issuer.Name, alignments);
        }

        return new CredentialDefinition
        {
            DefinitionId = item.ContentItemId,
            VersionId = item.ContentItemVersionId,
            Title = item.DisplayText,
            Rules = (part.Rules ?? []).ToList(),
            Achievement = new CredentialAchievement
            {
                Name = item.DisplayText,
                Description = part.Description,
                Criteria = part.Criteria,
                ImageUrl = ImageUrl(item, issuer?.BaseUrl),
                AchievementType = part.AchievementType,
                Tags = (part.Tags ?? []).ToList(),
                Alignments = alignments,
                ValidityMonths = part.ValidityMonths,
            },
        };
    }

    private string ImageUrl(ContentItem item, string baseUrl)
    {
        var path = item.Get<ContentPart>(item.ContentType)?.Get<MediaField>("Image")?.Paths?.FirstOrDefault();

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var url = _mediaFileStore.MapPathToPublicUrl(path);

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
        {
            return absolute.ToString();
        }

        if (string.IsNullOrEmpty(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var site))
        {
            return null;
        }

        return site.GetLeftPart(UriPartial.Authority) + (url.StartsWith('/') ? url : "/" + url);
    }

    private async Task AddSkillsAlignmentsAsync(CredentialDefinitionPart part, string achievementUrl, string issuerName, List<CredentialAlignment> alignments)
    {
        if (!string.IsNullOrEmpty(part.SkillId) && _services.GetService<ISkillManager>() is { } skills && await skills.FindByIdAsync(part.SkillId) is { } skill)
        {
            alignments.Add(new CredentialAlignment
            {
                TargetName = skill.Name,
                TargetUrl = $"{achievementUrl}#skill-{skill.ItemId}",
                TargetFramework = issuerName,
                TargetCode = skill.ItemId,
                TargetType = "ceasn:Competency",
            });
        }

        if (!string.IsNullOrEmpty(part.CertificationId) && _services.GetService<ICertificationManager>() is { } certifications && await certifications.FindByIdAsync(part.CertificationId) is { } certification)
        {
            alignments.Add(new CredentialAlignment
            {
                TargetName = certification.Name,
                TargetUrl = $"{achievementUrl}#certification-{certification.ItemId}",
                TargetFramework = issuerName,
                TargetCode = certification.ItemId,
                TargetType = "ceterms:Credential",
            });
        }
    }
}
