using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Locking.Distributed;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Credentials.Recipes;

/// <summary>
/// Imports credential awards as learner records, as they were issued: the signed credential is not changed. People are
/// named by user name; a row whose recipient is not a user of the site, or whose status slot another award holds, is
/// reported and skipped. An award already on the site (same public code) is skipped.
/// </summary>
internal sealed class LearningCredentialAwardsRecipeStep : NamedRecipeStepHandler
{
    private readonly CredentialAwardStore _store;
    private readonly LearnerUserLookup _users;
    private readonly IDistributedLock _distributedLock;
    private readonly global::YesSql.ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningCredentialAwardsRecipeStep"/> class.
    /// </summary>
    /// <param name="store">The award store.</param>
    /// <param name="users">The user lookup.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public LearningCredentialAwardsRecipeStep(CredentialAwardStore store, LearnerUserLookup users, IDistributedLock distributedLock, global::YesSql.ISession session, IStringLocalizer<LearningCredentialAwardsRecipeStep> stringLocalizer)
        : base(CredentialsConstants.RecipeStepName)
    {
        _store = store;
        _users = users;
        _distributedLock = distributedLock;
        _session = session;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Awards"] is not JsonArray rows)
        {
            return;
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            var award = row.Deserialize<CredentialAward>(CredentialAwardsJson.Options);

            if (award is null || string.IsNullOrEmpty(award.PublicId) || string.IsNullOrEmpty(award.DefinitionId))
            {
                context.Errors.Add(S["A row of the credential awards step has no public code or definition; it was skipped."]);

                continue;
            }

            if (await _store.FindByPublicIdAsync(award.PublicId) is not null)
            {
                continue;
            }

            var userName = row["RecipientUserName"]?.GetValue<string>();

            if (award.ErasedUtc is null)
            {
                award.RecipientUserId = await _users.FindUserIdAsync(userName);

                if (award.RecipientUserId is null)
                {
                    context.Errors.Add(S["The credential award {0} was skipped: no user is named '{1}'.", award.PublicId, userName ?? string.Empty]);

                    continue;
                }
            }

            // A slot outside its list would make the whole list fail to encode.
            if (award.StatusListNumber < 1 || award.StatusListIndex < 0 || award.StatusListIndex >= CredentialsConstants.StatusListCapacity)
            {
                context.Errors.Add(S["The credential award {0} was skipped: its status list slot ({1}, {2}) is outside the status lists.", award.PublicId, award.StatusListNumber, award.StatusListIndex]);

                continue;
            }

            // Slots are checked and taken under the lock awards take, so an award made meanwhile cannot take the same one.
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(DefaultCredentialAwardService.AwardLockKey, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));

            if (!locked)
            {
                context.Errors.Add(S["The credential award {0} was skipped: other awards were being made. Run the step again.", award.PublicId]);

                continue;
            }

            await using var held = locker;

            if (await _store.IsSlotTakenAsync(award.StatusListNumber, award.StatusListIndex))
            {
                context.Errors.Add(S["The credential award {0} was skipped: another award holds its status list slot ({1}, {2}).", award.PublicId, award.StatusListNumber, award.StatusListIndex]);

                continue;
            }

            award.AwardedBy = await _users.FindUserIdAsync(row["AwardedByUserName"]?.GetValue<string>());
            award.RevokedBy = await _users.FindUserIdAsync(row["RevokedByUserName"]?.GetValue<string>());

            if (string.IsNullOrEmpty(award.ItemId) || await _store.FindByIdAsync(award.ItemId) is not null)
            {
                award.ItemId = null;
            }

            await _store.CreateAsync(award);

            // Saved before the lock is released, as an award is.
            await _session.SaveChangesAsync();
        }
    }
}

/// <summary>
/// The JSON shape of credential awards in recipes: the award as stored, with people named by user name.
/// </summary>
internal static class CredentialAwardsJson
{
    /// <summary>
    /// Gets the serializer options.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Writes an award as a recipe row.
    /// </summary>
    /// <param name="award">The award.</param>
    /// <param name="userNames">The user names by user id.</param>
    /// <returns>The row.</returns>
    public static JsonObject ToRow(CredentialAward award, IDictionary<string, string> userNames)
    {
        var row = JsonSerializer.SerializeToNode(award, Options).AsObject();

        row.Remove(nameof(CredentialAward.RecipientUserId));
        row.Remove(nameof(CredentialAward.AwardedBy));
        row.Remove(nameof(CredentialAward.RevokedBy));
        row.Remove(nameof(CredentialAward.IsRevoked));
        row["RecipientUserName"] = Name(award.RecipientUserId, userNames);
        row["AwardedByUserName"] = Name(award.AwardedBy, userNames);
        row["RevokedByUserName"] = Name(award.RevokedBy, userNames);

        return row;
    }

    private static string Name(string userId, IDictionary<string, string> userNames)
        => userId is not null && userNames.TryGetValue(userId, out var name) ? name : null;
}

/// <summary>
/// The deployment step that exports credential awards as learner records.
/// </summary>
public sealed class LearningCredentialAwardsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningCredentialAwardsDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningCredentialAwardsDeploymentStep>("Credential awards (learner records)");

    /// <inheritdoc/>
    public override string Name => CredentialsConstants.RecipeStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

/// <summary>
/// Exports every credential award, 500 a page, as it was issued. Needs Export learner records; the export is recorded.
/// </summary>
internal sealed class LearningCredentialAwardsDeploymentSource : DeploymentSourceBase<LearningCredentialAwardsDeploymentStep>
{
    private const int PageSize = 500;

    private readonly CredentialAwardStore _store;
    private readonly LearnerUserLookup _users;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ILearnerRecordExportObserver> _exportObservers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningCredentialAwardsDeploymentSource"/> class.
    /// </summary>
    /// <param name="store">The award store.</param>
    /// <param name="users">The user lookup.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="exportObservers">The learner record export observers.</param>
    /// <param name="logger">The logger.</param>
    public LearningCredentialAwardsDeploymentSource(
        CredentialAwardStore store,
        LearnerUserLookup users,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<ILearnerRecordExportObserver> exportObservers,
        ILogger<LearningCredentialAwardsDeploymentSource> logger)
    {
        _store = store;
        _users = users;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _exportObservers = exportObservers;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ProcessAsync(LearningCredentialAwardsDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // A plan can run without a request, such as a scheduled remote deployment; then the plan's own check has passed.
        if (user?.Identity?.IsAuthenticated == true && !await _authorizationService.AuthorizeAsync(user, LearningPermissions.ExportLearnerRecords))
        {
            _logger.LogWarning("Credential awards were not exported: the user lacks the Export learner records permission.");

            return;
        }

        var rows = new JsonArray();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _store.PageAllAsync(skip, PageSize);
            var names = await _users.GetUserNamesAsync(page.SelectMany(award => new[] { award.RecipientUserId, award.AwardedBy, award.RevokedBy }));

            foreach (var award in page)
            {
                rows.Add(CredentialAwardsJson.ToRow(award, names));
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Awards"] = rows,
        });

        foreach (var observer in _exportObservers)
        {
            await observer.ExportedAsync(step.Name, rows.Count, user?.FindFirstValue(ClaimTypes.NameIdentifier), null);
        }
    }
}

/// <summary>
/// Shows the credential awards deployment step in a deployment plan.
/// </summary>
internal sealed class LearningCredentialAwardsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningCredentialAwardsDeploymentStep>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(LearningCredentialAwardsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningCredentialAwardsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningCredentialAwardsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    /// <inheritdoc/>
    public override IDisplayResult Edit(LearningCredentialAwardsDeploymentStep step, BuildEditorContext context)
        => View("LearningCredentialAwardsDeploymentStep_Fields_Edit", step).Location("Content");
}

/// <summary>
/// The CrestApps Recipes schema of the credential awards recipe step.
/// </summary>
internal sealed class LearningCredentialAwardsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    /// <inheritdoc/>
    public string Name => CredentialsConstants.RecipeStepName;

    /// <inheritdoc/>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(CredentialsConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'LearningCredentialAwards'.")),
                ("Awards", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("PublicId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The award's public code (32 hexadecimal characters). An award with this code already on the site is skipped.")),
                        ("DefinitionId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The content item id of the credential definition.")),
                        ("RecipientUserName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The user name of the recipient on this site.")),
                        ("Achievement", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The achievement as it was awarded.")),
                        ("Credential", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Open Badges 3.0 credential as issued, in JSON-LD. Not changed.")),
                        ("Jwt", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The credential signed as a JSON Web Token, as issued. Not changed.")),
                        ("StatusListNumber", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The status list of the award's revocation bit.")),
                        ("StatusListIndex", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The award's slot in the status list. A row whose slot another award holds is skipped.")),
                        ("IssuedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("When it was issued, in UTC.")),
                        ("RevokedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("When it was revoked, in UTC.")))
                    .Required("PublicId", "DefinitionId")).Description("The awards to add, as exported by the deployment step.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

/// <summary>
/// The CrestApps Recipes schema of the credential awards deployment step.
/// </summary>
internal sealed class LearningCredentialAwardsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string StepType => nameof(LearningCredentialAwardsDeploymentStep);

    /// <inheritdoc/>
    protected override string DisplayText => "Credential awards (learner records)";

    /// <inheritdoc/>
    protected override string Description => "Exports every digital credential award as it was issued, with the signed credential, 500 a page. People are named by user name. Needs the Export learner records permission.";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
