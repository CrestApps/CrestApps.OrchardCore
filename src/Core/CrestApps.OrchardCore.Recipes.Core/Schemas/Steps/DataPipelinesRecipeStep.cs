using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the <c>DataPipelines</c> recipe step of the Orchard Core Data Pipelines module.
/// </summary>
public sealed class DataPipelinesRecipeStep : RecipeStepSchemaBase
{
    private static readonly string[] _stepTypes =
    [
        "Aggregate",
        "CalculatedFields",
        "ConvertTimeZone",
        "CreateFile",
        "DataSource",
        "Distinct",
        "Filter",
        "Join",
        "Limit",
        "ReadMediaFile",
        "SaveContentItems",
        "SaveToMedia",
        "SelectFields",
        "SendByEmail",
        "SendToWebApi",
        "ShareDownloadLink",
        "Sort",
        "Union",
        "UploadToFtp",
        "UploadToSftp",
        "ZipFiles",
    ];

    /// <inheritdoc />
    public override string Name => "DataPipelines";

    protected override JsonSchema CreateSchema()
    {
        var step = RecipeStepSchemaBuilders.Object(
            [
                ("StepId", RecipeStepSchemaBuilders.String().MinLength(1).Description("The identifier of the step in the pipeline, referenced by the connections.")),
                ("Type", RecipeStepSchemaBuilders.SuggestedString(_stepTypes).Description("The type of the step, for example 'DataSource' or 'CreateFile'.")),
                ("Title", RecipeStepSchemaBuilders.String().Description("The title of the step shown in the designer.")),
                ("X", RecipeStepSchemaBuilders.Integer().Description("The horizontal position of the step in the designer.")),
                ("Y", RecipeStepSchemaBuilders.Integer().Description("The vertical position of the step in the designer.")),
                ("Properties", RecipeStepSchemaBuilders.Object(additionalProperties: true)
                    .Description("The settings of the step, under the name of their type, for example { \"DataSourceStepSettings\": { \"Source\": \"Contents\", \"DataSet\": \"BlogPost\" } }. Settings whose names start with 'Protected' hold secrets, which are not exported: enter them again before publishing.")),
            ],
            ["StepId", "Type"]);

        var connection = RecipeStepSchemaBuilders.Object(
            [
                ("SourceStepId", RecipeStepSchemaBuilders.String().Description("The identifier of the step whose output is connected.")),
                ("SourcePort", RecipeStepSchemaBuilders.String().Description("The output port of the source step, for example 'Output'.")),
                ("TargetStepId", RecipeStepSchemaBuilders.String().Description("The identifier of the step that receives the rows.")),
                ("TargetPort", RecipeStepSchemaBuilders.String().Description("The input port of the target step, for example 'Input'.")),
            ],
            ["SourceStepId", "TargetStepId"]);

        var pipeline = RecipeStepSchemaBuilders.Object(
            [
                ("PipelineId", RecipeStepSchemaBuilders.String()
                    .MaxLength(26)
                    .Description("The identifier of the pipeline, at most 26 characters. An existing pipeline with this identifier gets the imported definition as its draft; otherwise a pipeline is created, with a new identifier when none is given.")),
                ("Name", RecipeStepSchemaBuilders.String().MinLength(1).Description("The name of the pipeline.")),
                ("Description", RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String()).Description("The description of the pipeline.")),
                ("IsEnabled", RecipeStepSchemaBuilders.Boolean().Default(true).Description("Whether the pipeline can run.")),
                ("Definition", RecipeStepSchemaBuilders.Object(
                    [
                        ("Steps", RecipeStepSchemaBuilders.Array(step).Description("The steps of the pipeline.")),
                        ("Connections", RecipeStepSchemaBuilders.Array(connection).Description("The connections between the ports of the steps.")),
                    ])
                    .Description("The steps and connections of the pipeline. The definition is imported as a draft and runs only once a user publishes it.")),
            ],
            ["Name"]);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Pipelines", RecipeStepSchemaBuilders.Array(pipeline).Description("The data pipelines to create or update.")),
            ],
            ["Pipelines"]);
    }
}
