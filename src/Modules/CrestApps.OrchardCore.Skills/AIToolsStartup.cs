using CrestApps.Core.AI;
using CrestApps.OrchardCore.Skills.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Skills;

/// <summary>
/// Registers the skill and certificate AI tools whenever the Skills and AI features are on. Each checks the permission of
/// the matching Skills page; the deletes ask for approval.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListSkillsTool>(ListSkillsTool.TheName)
            .WithTitle(S["List Skills"])
            .WithDescription(S["Lists the skill catalog with category, required certification and whether each skill is enabled; filter by name."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<SaveSkillTool>(SaveSkillTool.TheName)
            .WithTitle(S["Save Skill"])
            .WithDescription(S["Creates or updates a skill in the catalog: name, category, description, the certification it requires and enabled."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<DeleteSkillTool>(DeleteSkillTool.TheName)
            .WithTitle(S["Delete Skill"])
            .WithDescription(S["Deletes a skill from the catalog. Disable it instead when people still hold it."])
            .WithCategory(S["Skills"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListCertificationTypesTool>(ListCertificationTypesTool.TheName)
            .WithTitle(S["List Certification Types"])
            .WithDescription(S["Lists the certification types people can hold, with issuer, validity in months, reminder days and whether a document is required."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<SaveCertificationTypeTool>(SaveCertificationTypeTool.TheName)
            .WithTitle(S["Save Certification Type"])
            .WithDescription(S["Creates or updates a certification type: name, issuer, validity in months, reminder days before expiry, document required and enabled."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<DeleteCertificationTypeTool>(DeleteCertificationTypeTool.TheName)
            .WithTitle(S["Delete Certification Type"])
            .WithDescription(S["Deletes a certification type. Disable it instead when certificates of this type exist."])
            .WithCategory(S["Skills"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<GetSubjectSkillsTool>(GetSubjectSkillsTool.TheName)
            .WithTitle(S["Get Person's Skills"])
            .WithDescription(S["Returns the skills a person holds (proficiency and effective dates) and their certificates, for a user or a staff member."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<SaveSkillAssignmentTool>(SaveSkillAssignmentTool.TheName)
            .WithTitle(S["Save Skill Assignment"])
            .WithDescription(S["Gives a user or staff member a skill, or changes its proficiency (1 to 5) and effective dates. A skill that requires a certification does not count until the person holds a valid certificate of it; the result says whether it counts today."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<RemoveSkillAssignmentTool>(RemoveSkillAssignmentTool.TheName)
            .WithTitle(S["Remove Skill Assignment"])
            .WithDescription(S["Takes a skill away from a user or staff member."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<FindSkillHoldersTool>(FindSkillHoldersTool.TheName)
            .WithTitle(S["Find Skill Holders"])
            .WithDescription(S["Finds the people who hold a skill at or above a proficiency on a date. Use it to answer \"who can do X\"."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<ListCertificatesTool>(ListCertificatesTool.TheName)
            .WithTitle(S["List Certificates"])
            .WithDescription(S["Lists the certificates people hold, filtered by person, certification type, status or expiring soon, with paging."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<SaveCertificateTool>(SaveCertificateTool.TheName)
            .WithTitle(S["Save Certificate"])
            .WithDescription(S["Records a certificate for a user or staff member, or updates one: certification type, number, issued and expiry dates, document path and notes."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<SetCertificateStatusTool>(SetCertificateStatusTool.TheName)
            .WithTitle(S["Set Certificate Status"])
            .WithDescription(S["Verifies a pending certificate or revokes one with a reason. A revoked certificate no longer satisfies the skills that require it."])
            .WithCategory(S["Skills"])
            .Selectable();

        services.AddCoreAITool<DeleteCertificateTool>(DeleteCertificateTool.TheName)
            .WithTitle(S["Delete Certificate"])
            .WithDescription(S["Deletes a certificate record. Use revoke instead to keep the history."])
            .WithCategory(S["Skills"])
            .RequireApproval()
            .Selectable();
    }
}
