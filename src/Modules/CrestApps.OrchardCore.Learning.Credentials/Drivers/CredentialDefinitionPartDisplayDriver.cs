using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Credentials.ViewModels;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Credentials.Drivers;

/// <summary>
/// The editor of a credential definition: the achievement it states and the rules that award it.
/// </summary>
internal sealed class CredentialDefinitionPartDisplayDriver : ContentPartDisplayDriver<CredentialDefinitionPart>
{
    private const int _maximumTags = 20;
    private const int _maximumValidityMonths = 1200;

    private readonly IEnumerable<ICredentialRuleKind> _ruleKinds;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialDefinitionPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="ruleKinds">The registered rule kinds.</param>
    /// <param name="services">The services, for the Skills managers when Skills is on.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public CredentialDefinitionPartDisplayDriver(
        IEnumerable<ICredentialRuleKind> ruleKinds,
        IServiceProvider services,
        IStringLocalizer<CredentialDefinitionPartDisplayDriver> stringLocalizer)
    {
        _ruleKinds = ruleKinds;
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CredentialDefinitionPart part, BuildPartEditorContext context)
        => Initialize<CredentialDefinitionPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Description = part.Description;
            model.Criteria = part.Criteria;
            model.AchievementType = string.IsNullOrEmpty(part.AchievementType) ? "Badge" : part.AchievementType;
            model.Tags = string.Join(", ", part.Tags ?? []);
            model.ValidityMonths = part.ValidityMonths;
            model.SkillId = part.SkillId;
            model.CertificationId = part.CertificationId;
            model.Rules = (part.Rules ?? []).Select(rule => new CredentialRuleRow { Value = $"{rule.Kind}|{rule.TargetId}" }).ToList();
            model.Alignments = (part.Alignments ?? []).Select(alignment => new CredentialAlignmentRow
            {
                TargetName = alignment.TargetName,
                TargetUrl = alignment.TargetUrl,
                TargetFramework = alignment.TargetFramework,
                TargetCode = alignment.TargetCode,
                TargetType = alignment.TargetType,
            }).ToList();

            await PopulateAsync(model);
        });

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(CredentialDefinitionPart part, UpdatePartEditorContext context)
    {
        var model = new CredentialDefinitionPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Description))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Description), S["The description is required: the credential states it."]);
        }

        if (string.IsNullOrWhiteSpace(model.Criteria))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Criteria), S["The criteria are required: say what a person did to earn the credential."]);
        }

        var achievementType = string.IsNullOrWhiteSpace(model.AchievementType) ? "Badge" : model.AchievementType.Trim();

        if (!CredentialsConstants.AchievementTypes.Contains(achievementType, StringComparer.Ordinal))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AchievementType), S["Pick an achievement type from the list."]);
        }

        if (model.ValidityMonths is { } months && (months < 1 || months > _maximumValidityMonths))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ValidityMonths), S["The validity is a number of months from 1 to {0}, or empty when the credential does not expire.", _maximumValidityMonths]);
        }

        var tags = (model.Tags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tags.Count > _maximumTags)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Tags), S["A credential has at most {0} tags.", _maximumTags]);
        }

        var kinds = _ruleKinds.Select(kind => kind.Kind).ToHashSet(StringComparer.Ordinal);
        var rules = new List<CredentialAwardRule>();

        foreach (var row in model.Rules ?? [])
        {
            if (string.IsNullOrWhiteSpace(row?.Value))
            {
                continue;
            }

            var separator = row.Value.IndexOf('|', StringComparison.Ordinal);
            var kind = separator > 0 ? row.Value.Substring(0, separator) : null;
            var target = separator > 0 ? row.Value.Substring(separator + 1) : null;

            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(target) || !kinds.Contains(kind))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Rules), S["A rule is not one the site offers. Pick it again."]);

                continue;
            }

            if (!rules.Any(rule => rule.Kind == kind && rule.TargetId == target))
            {
                rules.Add(new CredentialAwardRule { Kind = kind, TargetId = target });
            }
        }

        var alignments = new List<CredentialAlignment>();

        foreach (var row in (model.Alignments ?? []).Where(row => row is not null && !row.IsEmpty))
        {
            if (string.IsNullOrWhiteSpace(row.TargetName) ||
                !Uri.TryCreate(row.TargetUrl?.Trim(), UriKind.Absolute, out var url) ||
                (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Alignments), S["An alignment needs a name and an http or https address."]);

                continue;
            }

            alignments.Add(new CredentialAlignment
            {
                TargetName = row.TargetName.Trim(),
                TargetUrl = url.ToString(),
                TargetFramework = Trim(row.TargetFramework),
                TargetCode = Trim(row.TargetCode),
                TargetType = Trim(row.TargetType),
            });
        }

        part.Description = model.Description?.Trim();
        part.Criteria = model.Criteria?.Trim();
        part.AchievementType = achievementType;
        part.Tags = tags;
        part.ValidityMonths = model.ValidityMonths;
        part.SkillId = Trim(model.SkillId);
        part.CertificationId = Trim(model.CertificationId);
        part.Rules = rules;
        part.Alignments = alignments;

        return Edit(part, context);
    }

    private async Task PopulateAsync(CredentialDefinitionPartViewModel model)
    {
        model.AchievementTypes = CredentialsConstants.AchievementTypes
            .Select(type => new SelectListItem(AchievementTypeName(type), type))
            .ToList();

        foreach (var kind in _ruleKinds)
        {
            var group = new SelectListGroup { Name = kind.DisplayName.Value };

            foreach (var target in await kind.ListTargetsAsync())
            {
                model.RuleOptions.Add(new SelectListItem(target.Title, $"{kind.Kind}|{target.Id}") { Group = group });
            }
        }

        var skills = _services.GetService<ISkillManager>();
        var certifications = _services.GetService<ICertificationManager>();

        model.SkillsEnabled = skills is not null;

        if (skills is not null)
        {
            model.Skills = (await skills.GetEnabledAsync()).Select(skill => new SelectListItem(skill.Name, skill.ItemId)).ToList();
        }

        if (certifications is not null)
        {
            model.Certifications = (await certifications.GetEnabledAsync()).Select(certification => new SelectListItem(certification.Name, certification.ItemId)).ToList();
        }
    }

    private string AchievementTypeName(string type)
        => type switch
        {
            "Badge" => S["Badge"],
            "Certificate" => S["Certificate"],
            "CertificateOfCompletion" => S["Certificate of completion"],
            "Certification" => S["Certification"],
            "Course" => S["Course"],
            "Competency" => S["Competency"],
            "LearningProgram" => S["Learning program"],
            "MicroCredential" => S["Micro-credential"],
            "Award" => S["Award"],
            "Achievement" => S["Achievement"],
            "License" => S["License"],
            "Membership" => S["Membership"],
            _ => type,
        };

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
