using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.MailingLists.Recipes;

/// <summary>
/// Creates or updates mailing list members, matched by <c>ItemId</c>, then by list and address, with the evidence of
/// their consent. It moves records between sites: no e-mail is sent, no welcome coupon is given and nothing is pushed to
/// a provider.
/// </summary>
internal sealed class MailingListMembersRecipeStep : CatalogRecipeStepBase<MailingListMember>
{
    /// <summary>
    /// The name of the step.
    /// </summary>
    public const string RecipeName = "MailingListMembers";

    /// <summary>
    /// The member of the step that holds the members.
    /// </summary>
    public const string EntriesName = "Members";

    private readonly IMailingListMemberStore _members;
    private readonly IMailingListManager _lists;

    internal readonly IStringLocalizer S;

    public MailingListMembersRecipeStep(
        IMailingListMemberStore members,
        IMailingListManager lists,
        IStringLocalizer<MailingListMembersRecipeStep> stringLocalizer)
        : base(RecipeName, EntriesName)
    {
        _members = members;
        _lists = lists;
        S = stringLocalizer;
    }

    protected override async ValueTask<MailingListMember> FindByIdAsync(string id)
        => await _members.FindByIdAsync(id);

    protected override async ValueTask<CatalogRecipeLookup<MailingListMember>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var listId = token[nameof(MailingListMember.ListId)] is JsonValue listValue && listValue.TryGetValue<string>(out var list) ? list : null;
        var email = token[nameof(MailingListMember.Contact)] is JsonObject contact && contact["Email"] is JsonValue emailValue && emailValue.TryGetValue<string>(out var text) ? text : null;
        var existing = await _members.FindAsync(listId, EmailAddresses.Normalize(email));

        return existing is null ? CatalogRecipeLookup<MailingListMember>.NotFound : CatalogRecipeLookup<MailingListMember>.Found(existing);
    }

    protected override ValueTask<MailingListMember> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var member = new MailingListMember();

        CatalogDeploymentSerializer.Populate(member, token);

        return ValueTask.FromResult(member);
    }

    protected override ValueTask UpdateAsync(MailingListMember entry, JsonObject token)
    {
        CatalogDeploymentSerializer.Populate(entry, token);

        return ValueTask.CompletedTask;
    }

    protected override void Preparing(MailingListMember entry, JsonObject token)
    {
        entry.NormalizedEmail = EmailAddresses.Normalize(entry.Contact?.Email) ?? entry.NormalizedEmail;
        entry.SyncState = MailingListSyncState.Synced;
        entry.SyncError = null;

        if (entry.StatusChangedUtc == default)
        {
            entry.StatusChangedUtc = entry.SubscribedUtc ?? entry.CreatedUtc;
        }
    }

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(MailingListMember entry)
    {
        var result = new ValidationResultDetails();

        if (string.IsNullOrEmpty(entry.ListId) || await _lists.FindByIdAsync(entry.ListId) is null)
        {
            result.Fail(new ValidationResult(S["The mailing list {0} of a member does not exist.", entry.ListId ?? "-"]));
        }

        if (!EmailAddresses.IsValid(entry.Contact?.Email))
        {
            result.Fail(new ValidationResult(S["A member of mailing list {0} has no valid e-mail address.", entry.ListId ?? "-"]));
        }

        return result;
    }

    protected override async ValueTask CreateAsync(MailingListMember entry)
        => await _members.CreateAsync(entry);

    protected override async ValueTask CommitUpdateAsync(MailingListMember entry, JsonObject token)
        => await _members.UpdateAsync(entry);
}
