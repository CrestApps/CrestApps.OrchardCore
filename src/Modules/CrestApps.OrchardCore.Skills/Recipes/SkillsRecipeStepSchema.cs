using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Skills.Recipes;

/// <summary>
/// Describes the Skills recipe step to CrestApps Recipes.
/// </summary>
internal sealed class SkillsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => SkillsConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(SkillsConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'Skills'.")),
                ("Certifications", Array(Certification(), "The certifications to create, or update by id or name.")),
                ("Skills", Array(Skill(), "The skills to create, or update by id or name.")),
                ("Assignments", Array(Assignment(), "The skills people hold. Each one is added, or its proficiency and dates changed.")),
                ("CertificationRecords", Array(Record(), "The certificates people hold, created or updated by id.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Array(JsonSchemaBuilder items, string description)
    {
        return new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(items).Description(description);
    }

    private static JsonSchemaBuilder Text(string description)
    {
        return new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);
    }

    private static JsonSchemaBuilder Date(string description)
    {
        return new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description(description);
    }

    private static JsonSchemaBuilder SubjectKind(string description)
    {
        return new JsonSchemaBuilder().Type(SchemaValueType.String).Examples("User", "Worker").Description(description);
    }

    private static JsonSchemaBuilder Certification()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("itemId", Text("The certification id; omit to match by name.")),
                ("name", Text("The unique certification name.")),
                ("issuer", Text("Who issues it.")),
                ("description", Text("What it covers.")),
                ("validityMonths", new JsonSchemaBuilder().Type(SchemaValueType.Integer, SchemaValueType.Null).Minimum(1).Description("How many months a certificate stays valid; null when it never expires.")),
                ("requiresDocument", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a copy of the certificate is needed before it counts.")),
                ("reminderDays", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1)).Description("Days before expiry that a reminder is raised.")),
                ("enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the certification can be recorded and required by skills.")),
                ("generateNumbers", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the site numbers the certificates it issues.")),
                ("numberPrefix", Text("The prefix of a generated number, such as CPR-.")),
                ("certificateTemplateId", Text("The content item id of the certificate template issued certificates are printed with.")))
            .Required("name")
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder Skill()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("itemId", Text("The skill id; omit to match by name.")),
                ("name", Text("The unique skill name.")),
                ("category", Text("The group the skill is listed under.")),
                ("description", Text("What the skill means.")),
                ("requiredCertificationId", Text("The certification the skill requires, by id.")),
                ("RequiredCertificationName", Text("The certification the skill requires, by name; wins over the id.")),
                ("enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the skill can be assigned and used for matching.")))
            .Required("name")
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder Assignment()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("subjectKind", SubjectKind("Who holds the skill: User (the default) or Worker for a workforce worker. Other modules may register more kinds.")),
                ("subjectId", Text("The user id or the worker id.")),
                ("skillId", Text("The skill, by id.")),
                ("skillName", Text("The skill, by name, when the id isn't known.")),
                ("proficiency", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(5).Description("From 1 (trainee) to 5 (expert); 3 when omitted.")),
                ("effectiveFrom", Date("The first day the skill counts.")),
                ("effectiveTo", Date("The last day the skill counts.")))
            .Required("subjectId")
            .AdditionalProperties(false);
    }

    private static JsonSchemaBuilder Record()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("itemId", Text("The record id; omit for a new record.")),
                ("subjectKind", SubjectKind("Who holds the certificate: User (the default) or Worker for a workforce worker.")),
                ("subjectId", Text("The user id or the worker id.")),
                ("subjectName", Text("The holder's name, shown in lists.")),
                ("certificationId", Text("The certification, by id.")),
                ("certificationName", Text("The certification, by name; wins over the id.")),
                ("number", Text("The certificate number.")),
                ("issuedOn", Date("The day it was issued.")),
                ("expiresOn", Date("The last day it is valid.")),
                ("documentPath", Text("The media library path of a copy of the certificate.")),
                ("status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Pending", "Valid", "Expired", "Revoked").Description("Whether the certificate is waiting for a check, valid, expired or revoked.")),
                ("verifiedBy", Text("Who checked the certificate.")),
                ("verifiedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Format(Formats.DateTime).Description("When the certificate was checked.")),
                ("notes", Text("A note about the record.")),
                ("source", Text("What earned an issued certificate, such as Learning.CertificationPath.")),
                ("sourceId", Text("The id, within the source, of what earned it.")),
                ("evidence", Text("A summary of the evidence.")),
                ("verificationCode", Text("The code that proves an issued certificate on the public verification page.")),
                ("previousRecordId", Text("The certificate this one renews.")),
                ("renewedByRecordId", Text("The certificate that renews this one.")),
                ("revokedReason", Text("Why the certificate was revoked.")),
                ("revokedBy", Text("Who revoked it.")),
                ("revokedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Format(Formats.DateTime).Description("When it was revoked.")))
            .Required("subjectId")
            .AdditionalProperties(true);
    }
}
