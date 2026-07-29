---
sidebar_label: Workflow Activity Schemas
sidebar_position: 6.5
title: Workflow Activity Schemas
description: Describe every workflow event and task so the WorkflowType recipe step produces a complete, self-documenting JSON schema.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Recipes |
| **Feature ID** | `CrestApps.OrchardCore.Recipes` |

The `WorkflowType` recipe step imports workflow definitions. A workflow definition is mostly a list of activities, and every activity stores its configuration inside a free-form `Properties` bag whose shape depends entirely on the activity type. Without extra metadata, a JSON schema can only say "the activity name must be one of these values" — it cannot tell you that `EmailTask` needs a `Recipients` expression, that `IfElseTask` produces `True` and `False` outcomes, or that `ForkTask` invents its own outcome names.

**Workflow activity schema definitions** close that gap. They follow the same extensibility model already used by `IContentPartSchemaDefinition` and `IContentFieldSchemaDefinition`: each workflow event or task contributes its own schema fragment, and the `WorkflowType` recipe step composes all of them into a single, fully described schema.

## What the schema describes

For every registered activity the composed schema declares a `Properties` object whose JSON Schema `description` summarises the activity, and whose members document its configuration:

- **Category** — the group the activity belongs to, for example `Messaging`, `Control Flow` or `Content`. Written into the `description`.
- **Description** — what the activity actually does. Written into the `description`.
- **Outcomes** — the outcome names that can be used as `Transitions[].SourceOutcomeName`. Written into the `description`, together with a note when the activity can produce additional outcomes derived from its own configuration (`ForkTask`, `ScriptTask` and `UserTaskEvent` do this).
- **Properties** — every property the activity persists, each emitted as a real JSON Schema member with its type, a description derived from the activity's editor view, whether it supports Liquid or JavaScript syntax, and whether it is required.

Display text and the dynamic-outcome flag are not emitted into the JSON Schema. They are carried on the `WorkflowActivityDescriptor` returned by `IWorkflowActivitySchemaService.GetActivityDescriptorsAsync`, so other features can build their own activity listings.

### The property bag stays open

`ActivityRecord` derives from `Entity`, whose `Properties` member is a free-form `JsonObject`. Any module can persist an extra section there through a section display driver, exactly as Orchard Core does for `ActivityMetadata`. The generated schema therefore documents the well known properties without rejecting the rest, so a recipe exported from a tenant with extra modules enabled still validates.

The same reasoning applies one level up: `WorkflowType` also derives from `Entity`, so a workflow type may carry its own `Properties` object.

The step also validates structural rules that a plain enum cannot express: a start activity must be an event, every activity must carry an `ActivityId` because the step never generates one, and each transition must reference a source activity, a source outcome and a destination activity. Non-start activities may be either tasks or events, because an event placed in the middle of a workflow halts execution and blocks until it is triggered.

## How it is composed

The activity entry in the schema is an object with a shared shape (`ActivityId`, `Name`, `IsStart`, `X`, `Y`, `Properties`) plus a list of conditional branches:

```jsonc
{
  "allOf": [
    // A start activity must be an event. Any activity is valid elsewhere.
    { "if": { "properties": { "IsStart": { "const": true } } }, "then": { /* Name enum of events */ } },

    // One branch per activity, selecting the matching Properties schema.
    { "if": { "properties": { "Name": { "const": "EmailTask" } } }, "then": { "properties": { "Properties": { /* EmailTask schema */ } } } }
  ]
}
```

Editors and AI agents that understand JSON Schema will therefore offer the correct property list, descriptions and required fields as soon as the activity name is typed.

## Expression properties

Most activity properties are typed as `WorkflowExpression<T>` in Orchard Core. These serialize as an object holding a single `Expression` string:

```json
{
  "Recipients": {
    "Expression": "{{ Workflow.Input.ContentItem.Content.ContactPart.Email.Text }}"
  }
}
```

The schema mirrors that shape, so a bare string is rejected. Each expression property's description also states which syntax is evaluated at runtime — Liquid or JavaScript — because the two are not interchangeable.

`Expression` is optional and nullable. Orchard Core omits null values when it serializes a workflow type, so an expression that was never filled in is persisted as an empty object. Both of the following are valid:

```json
{ "Sender": {} }
```

```json
{ "Sender": { "Expression": null } }
```

Enum properties accept either the member name or its ordinal, matching Orchard Core's serializer configuration:

```json
{ "BodyFormat": "Html" }
```

```json
{ "BodyFormat": 2 }
```

## Every activity supports `ActivityMetadata`

Regardless of type, every activity may carry an `ActivityMetadata` object with an optional `Title`. The title is nullable, matching the recipes that Orchard Core itself ships. This property is added automatically by the base class and must never be declared by a definition.

`ActivityMetadata` is the only section Orchard Core stores in the activity property bag, and `Title` is its only member, so its own schema is closed — an unknown member such as `Subtitle` is rejected.

```json
{
  "Name": "EmailTask",
  "Properties": {
    "ActivityMetadata": {
      "Title": "Notify the sales team"
    },
    "Recipients": {
      "Expression": "sales@example.com"
    }
  }
}
```

## Example recipe step

```json
{
  "name": "WorkflowType",
  "data": [
    {
      "WorkflowTypeId": "notify-on-publish",
      "Name": "Notify on publish",
      "IsEnabled": true,
      "IsSingleton": false,
      "DeleteFinishedWorkflows": false,
      "Activities": [
        {
          "ActivityId": "1",
          "Name": "ContentPublishedEvent",
          "IsStart": true,
          "X": 100,
          "Y": 100,
          "Properties": {
            "ActivityMetadata": {
              "Title": "When an article is published"
            },
            "ContentTypeFilter": [ "Article" ]
          }
        },
        {
          "ActivityId": "2",
          "Name": "EmailTask",
          "IsStart": false,
          "X": 400,
          "Y": 100,
          "Properties": {
            "Recipients": {
              "Expression": "editors@example.com"
            },
            "Subject": {
              "Expression": "{{ Workflow.Input.ContentItem.DisplayText }} was published"
            },
            "BodyFormat": "Html",
            "HtmlBody": {
              "Expression": "<p>The article is now live.</p>"
            }
          }
        }
      ],
      "Transitions": [
        {
          "SourceActivityId": "1",
          "SourceOutcomeName": "Done",
          "DestinationActivityId": "2"
        }
      ]
    }
  ]
}
```

## Describing a custom event or task

Create a class deriving from `WorkflowActivitySchemaDefinitionBase` and place it next to your other schema definitions. The class name conventionally matches the activity name with a `Schema` suffix.

```csharp
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace MyCompany.Workflows.Schemas;

public sealed class SendWebhookTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name { get; } = "SendWebhookTask";

    protected override string Category => "HTTP";

    protected override string DisplayText => "Send Webhook Task";

    protected override string Description => "Posts the workflow payload to an external endpoint";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["Url"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Url", WorkflowActivitySchemaBuilders.LiquidExpression("The absolute URL the payload is posted to."));
        yield return ("Payload", WorkflowActivitySchemaBuilders.LiquidExpression("The JSON body sent to the endpoint."));
        yield return ("TimeoutSeconds", WorkflowActivitySchemaBuilders.Integer("How long to wait for a response before failing. Defaults to 30."));
        yield return ("Retry", WorkflowActivitySchemaBuilders.Boolean("Whether a failed request is retried once."));
    }
}
```

Register it from your module's `Startup` class:

```csharp
services.AddWorkflowActivitySchema<SendWebhookTaskSchema>();
```

The registration extension lives in `Microsoft.Extensions.DependencyInjection` and registers the definition as an `IWorkflowActivitySchemaDefinition`. Definitions are resolved once per activity name; if two definitions declare the same `Name`, the last one registered wins, which lets you override a built-in description.

### Available property builders

`WorkflowActivitySchemaBuilders` provides the fragments used by every built-in definition:

| Builder | Use for |
| --- | --- |
| `Expression(description)` | A `WorkflowExpression<T>` whose syntax is chosen by another property. |
| `LiquidExpression(description)` | A `WorkflowExpression<T>` evaluated as Liquid. |
| `ScriptExpression(description)` | A `WorkflowExpression<T>` evaluated as JavaScript. |
| `String(description)` | A plain string. |
| `StringEnum(description, values)` | A string restricted to known values. |
| `EnumValue(description, names)` | A .NET enum, accepting the member name or its ordinal. |
| `Boolean(description)` | A boolean. |
| `Integer(description)` | A whole number. |
| `Number(description)` | A floating point number. |
| `StringArray(description)` | An array of strings. |
| `StringEnumArray(description, values)` | An array of strings restricted to known values. |
| `Any(description)` | A value of any JSON type. |
| `ActivityMetadata()` | Reserved for the base class; do not return it from `GetPropertyDefinitions`. |

### Rules to follow

- **Describe every supported property.** The `Properties` bag itself is open, so an omitted property will not fail validation — it will silently lose its documentation, its type and its syntax hint. Describe everything the activity writes and still reads, including inherited properties and runtime state. Prefix runtime state with `Runtime state.` so authors know not to set it by hand.
- **Leave obsolete properties out.** Properties marked `[Obsolete]`, and properties the activity persists but never reads, are omitted on purpose so the schema only offers authors a supported way to configure the activity. Because the bag stays open, an existing recipe that still carries them keeps validating. `EmailTask.Body` and `EmailTask.IsHtmlBody` are obsolete in favour of `BodyFormat`, `TextBody` and `HtmlBody`, and `UpdateContentTask.ContentItemIdExpression` is never read because its display driver binds that editor field to `Content`.
- **Use the outcome names, not the labels.** Orchard Core outcome names come from the invariant key inside `S["..."]`, so `S["Drew Blank"]` yields the outcome name `Drew Blank`.
- **Set `HasDynamicOutcomes`** when the activity derives outcomes from its own configuration. The generated description then tells authors that transitions may reference names not present in the static list.
- **Only mark a property required when the activity truly cannot run without it.** Use the display driver's validation and the activity's `ExecuteAsync` method as the source of truth. Express conditionally required properties through `BuildActivitySchemaCore` rather than `RequiredProperties`.
- **Override `AllowAdditionalProperties` to `false`** only when you are certain the activity can never receive an extra member, for example because it is internal to your module and no section driver targets it.

### Conditional requirements and asynchronous work

`RequiredProperties` covers the unconditional case. When a property is only required in some configurations, or when the schema needs asynchronous work such as reading tenant metadata, override one of the two build seams instead.

Override `BuildActivitySchemaCore` to apply object level constraints that `GetPropertyDefinitions` cannot express. Call the base implementation first to obtain the standard envelope, which already contains the shared `ActivityMetadata` property, then refine it. The built-in `ForEachTaskSchema` uses this seam to require `Enumerable` when `Syntax` is `JavaScript` (the default) and `LiquidEnumerable` when `Syntax` is `Liquid`:

```csharp
protected override WorkflowActivitySchema BuildActivitySchemaCore(WorkflowActivitySchemaContext context)
{
    var schema = base.BuildActivitySchemaCore(context);

    schema.Properties = schema.Properties
        .AllOf(new JsonSchemaBuilder()
            .If(new JsonSchemaBuilder()
                .Properties(("Syntax", new JsonSchemaBuilder().Const("Liquid")))
                .Required("Syntax"))
            .Then(RequireExpression("LiquidEnumerable"))
            .Else(RequireExpression("Enumerable")));

    return schema;
}
```

Override `BuildActivitySchemaAsync` when the schema depends on data that must be awaited. It receives the same context and a `CancellationToken`, and its default implementation simply wraps `BuildActivitySchemaCore`:

```csharp
protected override async ValueTask<WorkflowActivitySchema> BuildActivitySchemaAsync(
    WorkflowActivitySchemaContext context,
    CancellationToken cancellationToken = default)
{
    var contentTypes = await _contentDefinitionManager.ListTypeDefinitionsAsync();
    var schema = BuildActivitySchemaCore(context);

    schema.Properties = schema.Properties
        .Properties(("ContentType", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum(contentTypes.Select(type => (JsonNode)type.Name))));

    return schema;
}
```

Definitions are resolved from the service provider, so a definition that needs services can take them through constructor injection. Neither seam caches its result; build the schema fresh on every call so it always reflects the supplied context.

### Tightening the property bag

The bag is open by default. Set the following to reject anything that is not declared:

```csharp
protected override bool AllowAdditionalProperties => false;
```

## Auditing coverage

Activities registered with `AddActivity<TActivity, TDriver>()` that have no schema definition still appear in the `Name` enum, but their `Properties` object is left unconstrained. The unit test suite audits this: it asserts that every built-in activity has a definition, that every definition is actually registered by the Recipes module, that names are unique, and that each definition supplies a category, a description, a description for every property, and either outcomes or `HasDynamicOutcomes`. When you add a new activity to a CrestApps module, add its schema definition and its registration in the same change or the audit will fail.
