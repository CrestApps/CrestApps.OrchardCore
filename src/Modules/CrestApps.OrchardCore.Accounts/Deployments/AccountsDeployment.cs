using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts.Recipes;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Accounts.ViewModels;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Lists.Indexes;
using OrchardCore.Localization;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Accounts.Deployments;

/// <summary>
/// Exports every account, with all of its parts, and optionally the records the accounts hold, as an <c>Accounts</c>
/// recipe step.
/// </summary>
public sealed class AccountsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AccountsDeploymentStep>("Content");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AccountsDeploymentStep>("Accounts");

    public override string Name => nameof(AccountsDeploymentStep);

    /// <summary>
    /// Gets or sets whether the records the accounts hold, such as their contacts and opportunities, are exported too.
    /// </summary>
    public bool IncludeRecords { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class AccountsDeploymentSource : DeploymentSourceBase<AccountsDeploymentStep>
{
    private const int BatchSize = 500;

    private readonly ISession _session;
    private readonly IAccountContentTypeProvider _accountContentTypeProvider;

    public AccountsDeploymentSource(ISession session, IAccountContentTypeProvider accountContentTypeProvider)
    {
        _session = session;
        _accountContentTypeProvider = accountContentTypeProvider;
    }

    protected override async Task ProcessAsync(AccountsDeploymentStep step, DeploymentPlanResult result)
    {
        var accountTypes = (await _accountContentTypeProvider.GetAccountTypesAsync()).Select(type => type.Name).ToArray();
        var accounts = new JsonArray();
        var records = new JsonArray();
        var accountIds = new List<string>();

        if (accountTypes.Length > 0)
        {
            foreach (var account in await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(accountTypes)).ListAsync())
            {
                accounts.Add(JObject.FromObject(account));
                accountIds.Add(account.ContentItemId);
            }
        }

        // The records an account holds sit in it through Orchard Core's list part.
        if (step.IncludeRecords)
        {
            foreach (var batch in accountIds.Chunk(BatchSize))
            {
                var items = await _session.Query<ContentItem, ContainedPartIndex>(index => index.ListContentItemId.IsIn(batch))
                    .With<ContentItemIndex>(index => index.Latest)
                    .ListAsync();

                foreach (var item in items)
                {
                    records.Add(JObject.FromObject(item));
                }
            }
        }

        var data = new JsonObject
        {
            ["name"] = AccountsStep.Name,
            ["Accounts"] = accounts,
        };

        if (step.IncludeRecords)
        {
            data["Records"] = records;
        }

        result.Steps.Add(data);
    }
}

internal sealed class AccountsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AccountsDeploymentStep>
{
    public override IDisplayResult Display(AccountsDeploymentStep step, BuildDisplayContext context)
        => Combine(
            View("AccountsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("AccountsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(AccountsDeploymentStep step, BuildEditorContext context)
        => Initialize<AccountsDeploymentStepViewModel>("AccountsDeploymentStep_Fields_Edit", model => model.IncludeRecords = step.IncludeRecords)
            .Location("Content");

    public override async Task<IDisplayResult> UpdateAsync(AccountsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AccountsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.IncludeRecords);

        step.IncludeRecords = model.IncludeRecords;

        return Edit(step, context);
    }
}
