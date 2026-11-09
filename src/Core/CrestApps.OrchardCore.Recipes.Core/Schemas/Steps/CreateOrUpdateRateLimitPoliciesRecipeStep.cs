using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the <c>CreateOrUpdateRateLimitPolicies</c> recipe step of the Orchard Core Rate Limits module.
/// </summary>
public sealed class CreateOrUpdateRateLimitPoliciesRecipeStep : RecipeStepSchemaBase
{
    private static readonly string[] _groupNames =
    [
        "email-confirmation",
        "password-authentication",
        "password-recovery",
        "two-factor-authentication",
        "two-factor-code-send",
        "two-factor-recovery",
        "user-registration",
    ];

    private static readonly string[] _queueProcessingOrders = ["OldestFirst", "NewestFirst"];

    /// <inheritdoc />
    public override string Name => "CreateOrUpdateRateLimitPolicies";

    protected override JsonSchema CreateSchema()
    {
        var limiter = RecipeStepSchemaBuilders.Object(
            [
                ("Id", RecipeStepSchemaBuilders.String().Description("The identifier of the limiter, for example \"[js: uuid()]\".")),
                ("Source", RecipeStepSchemaBuilders.SuggestedString(["FixedWindow", "SlidingWindow", "TokenBucket", "Concurrency"])
                    .Description("The algorithm of the limiter.")),
                ("Properties", RecipeStepSchemaBuilders.Object(
                    [
                        ("FixedWindowRateLimiterData", RecipeStepSchemaBuilders.Object(
                            [
                                ("PermitLimit", RecipeStepSchemaBuilders.Integer().Minimum(1).Description("The number of requests allowed in a window.")),
                                ("WindowSeconds", RecipeStepSchemaBuilders.Integer().Minimum(1).Description("The length of the window, in seconds.")),
                                ("QueueLimit", RecipeStepSchemaBuilders.Integer().Minimum(0).Description("The number of requests that wait when the limit is reached.")),
                            ])
                            .Description("The settings of a 'FixedWindow' limiter.")),
                        ("SlidingWindowRateLimiterData", RecipeStepSchemaBuilders.Object(
                            [
                                ("PermitLimit", RecipeStepSchemaBuilders.Integer().Minimum(1).Description("The number of requests allowed in a window.")),
                                ("WindowSeconds", RecipeStepSchemaBuilders.Integer().Minimum(1).Description("The length of the window, in seconds.")),
                                ("SegmentsPerWindow", RecipeStepSchemaBuilders.Integer().Minimum(1).Description("The number of segments the window is divided into.")),
                                ("QueueLimit", RecipeStepSchemaBuilders.Integer().Minimum(0).Description("The number of requests that wait when the limit is reached.")),
                            ])
                            .Description("The settings of a 'SlidingWindow' limiter.")),
                        ("TokenBucketRateLimiterData", RecipeStepSchemaBuilders.Object(
                            [
                                ("TokenLimit", RecipeStepSchemaBuilders.Integer().Minimum(1).Description("The maximum number of tokens in the bucket.")),
                                ("TokensPerPeriod", RecipeStepSchemaBuilders.Integer().Minimum(1).Description("The number of tokens added at each period.")),
                                ("ReplenishmentPeriodSeconds", RecipeStepSchemaBuilders.Integer().Minimum(1).Description("The period between two replenishments, in seconds.")),
                                ("QueueLimit", RecipeStepSchemaBuilders.Integer().Minimum(0).Description("The number of requests that wait when no token is left.")),
                                ("QueueProcessingOrder", RecipeStepSchemaBuilders.String().Enum(_queueProcessingOrders).Description("The order in which queued requests are processed.")),
                            ])
                            .Description("The settings of a 'TokenBucket' limiter.")),
                        ("ConcurrencyRateLimiterData", RecipeStepSchemaBuilders.Object(
                            [
                                ("PermitLimit", RecipeStepSchemaBuilders.Integer().Minimum(1).Description("The number of requests processed at the same time.")),
                                ("QueueLimit", RecipeStepSchemaBuilders.Integer().Minimum(0).Description("The number of requests that wait when the limit is reached.")),
                                ("QueueProcessingOrder", RecipeStepSchemaBuilders.String().Enum(_queueProcessingOrders).Description("The order in which queued requests are processed.")),
                            ])
                            .Description("The settings of a 'Concurrency' limiter.")),
                    ])
                    .Description("The settings of the limiter, under the name of the data type of its algorithm.")),
            ],
            ["Source"]);

        var policy = RecipeStepSchemaBuilders.Object(
            [
                ("PolicyId", RecipeStepSchemaBuilders.String().Description("The identifier of the policy. A policy with the same identifier, or else with the same name, is updated; otherwise a policy is created.")),
                ("Name", RecipeStepSchemaBuilders.String().MinLength(1).Description("The unique name of the policy.")),
                ("Description", RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String()).Description("The description of the policy.")),
                ("OwnerId", RecipeStepSchemaBuilders.String().Description("The identifier of the user who owns the policy, for example \"[js: parameters('AdminUserId')]\".")),
                ("Author", RecipeStepSchemaBuilders.String().Description("The user name of the author of the policy, for example \"[js: parameters('AdminUsername')]\".")),
                ("Scope", RecipeStepSchemaBuilders.String().Enum("Global", "Endpoint", "Group").Description("Which requests the policy limits: every request, the requests to one path, or the endpoints of a rate-limit group.")),
                ("Path", RecipeStepSchemaBuilders.String().Pattern("^/").Description("The request path that starts with '/'. Required when the scope is 'Endpoint'.")),
                ("GroupName", RecipeStepSchemaBuilders.SuggestedString(_groupNames).Description("The rate-limit group of the endpoints. Required when the scope is 'Group'.")),
                ("IsEnabled", RecipeStepSchemaBuilders.Boolean().Description("Whether the policy is enforced.")),
                ("EnabledUtc", RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String().Format(Formats.DateTime)).Description("The UTC date and time when the policy was enabled. Set automatically when omitted.")),
                ("Limiters", RecipeStepSchemaBuilders.Array(limiter).Description("The limiters of the policy. A request must be allowed by every limiter.")),
            ],
            ["Name", "Scope"]);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("policies", RecipeStepSchemaBuilders.Array(policy).Description("The rate-limit policies to create or update.")),
            ],
            ["policies"]);
    }
}
