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
                (nameof(SurveySettings.DefaultMinimumGroupSize), new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("The minimum group size of new anonymous surveys; 0 turns the check off.")))
            .AdditionalProperties(true);
}
