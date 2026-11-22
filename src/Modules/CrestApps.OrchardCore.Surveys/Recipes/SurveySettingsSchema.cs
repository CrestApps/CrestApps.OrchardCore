using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Surveys.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Describes the Surveys site settings for the Settings recipe step.
/// </summary>
internal sealed class SurveySettingsSchema : SiteSettingsSchemaBase
{
    /// <inheritdoc />
    public override string Name => nameof(SurveySettings);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The Surveys settings.")
            .Properties(
                (nameof(SurveySettings.DefaultRetentionDays), new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Description("The retention of new surveys in days; empty to keep responses.")),
                (nameof(SurveySettings.DefaultIncompleteExpiryHours), new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("After how many idle hours unfinished responses of new surveys are abandoned.")),
                (nameof(SurveySettings.MaxUploadMegabytes), new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("The largest file a respondent may upload, in megabytes.")),
                (nameof(SurveySettings.AllowedRedirectHosts), new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The hosts endings may redirect to besides this site.")),
                (nameof(SurveySettings.DefaultMinimumGroupSize), new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("The minimum group size of new anonymous surveys; never below the floor.")),
                (nameof(SurveySettings.MinimumGroupSizeFloor), new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(1000).Description("The smallest minimum group size any anonymous survey may use; a survey may ask for more, never less. Defaults to 5.")),
                (nameof(SurveySettings.InvitationEmailsPerDay), new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("The most survey invitation and reminder e-mails the site sends a day, counted from midnight in the site's time zone; invitations over it are held back and the hourly reminder task sends them on a later day. Notifications to users don't count. Defaults to 2000.")))
            .AdditionalProperties(true);
}
