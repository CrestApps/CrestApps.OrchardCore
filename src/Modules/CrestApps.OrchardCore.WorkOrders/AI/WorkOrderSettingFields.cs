using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// The fields of the four work order settings records as the settings AI tools read and write them: the JSON schema of
/// each kind, a record's fields, and how the given fields overlay the editor's fields.
/// </summary>
internal static class WorkOrderSettingFields
{
    private const string TypeFields = """
    {
      "name": { "type": "string", "description": "(types, checklists, service-levels, sub-statuses) The name." },
      "color": { "type": "string", "description": "(types, sub-statuses) A color, such as #1e88e5. Empty text clears it." },
      "defaultDurationMinutes": { "type": "integer", "minimum": 1, "description": "(types) How long a new work order of the type is estimated to take, in minutes." },
      "crewSize": { "type": "integer", "minimum": 1, "description": "(types) How many technicians the work needs." },
      "defaultPriority": { "type": "string", "enum": ["Low", "Normal", "High", "Urgent"], "description": "(types) The priority a new work order of the type starts with." },
      "serviceLevelPolicyId": { "type": "string", "description": "(types) The ID of the service level (kind service-levels) its work orders are timed by. Empty text clears it." },
      "billingMode": { "type": "string", "enum": ["NotBillable", "FixedPrice", "TimeAndMaterials"], "description": "(types) How a completed work order is billed." },
      "currency": { "type": "string", "description": "(types) The three-letter currency code of its prices. Defaults to USD." },
      "fixedPrice": { "type": "number", "exclusiveMinimum": 0, "description": "(types) The price, needed when billingMode is FixedPrice." },
      "requiresSignature": { "type": "boolean", "description": "(types) Whether the customer must sign before the work order can be completed." },
      "requiredPhotos": { "type": "integer", "minimum": 0, "description": "(types) How many photos must be added before the work order can be completed." },
      "checklistTemplateIds": { "type": "array", "items": { "type": "string" }, "description": "(types) The IDs of the checklists (kind checklists) a new work order gets; replaces the list." },
      "requiredSkills": {
        "type": "array",
        "description": "(types) The skills the work needs; replaces the list.",
        "items": {
          "type": "object",
          "properties": {
            "skillId": { "type": "string", "description": "The skill's ID, from the Skills feature." },
            "minProficiency": { "type": "integer", "minimum": 1, "maximum": 5, "description": "The lowest proficiency accepted, 1 to 5. Defaults to 1." }
          },
          "required": ["skillId"],
          "additionalProperties": false
        }
      },
      "requiredCertificationIds": { "type": "array", "items": { "type": "string" }, "description": "(types) The IDs of the certifications the technician must hold, from the Skills feature; replaces the list." },
      "appointmentTypeIds": { "type": "array", "items": { "type": "string" }, "description": "(types) The IDs of the Scheduling appointment types whose confirmed bookings create a work order of this type; replaces the list." },
      "defaultLines": {
        "type": "array",
        "description": "(types) The lines a new work order of the type starts with; replaces the list.",
        "items": {
          "type": "object",
          "properties": {
            "id": { "type": "string", "description": "The ID of a line the type already has, to keep its ID. Leave it out for a new line." },
            "kind": { "type": "string", "enum": ["Product", "Service", "Labor", "Expense"], "description": "What the line is. Needed for a new line." },
            "description": { "type": "string", "description": "What the line is for. Needed for a new line." },
            "stockItemId": { "type": "string", "description": "For a product line, the Inventory stock item its parts come from." },
            "quantity": { "type": "number", "minimum": 0, "description": "The quantity planned. Defaults to 1." },
            "unitPrice": { "type": "number", "minimum": 0, "description": "The price of one unit." },
            "billable": { "type": "boolean", "description": "Whether the customer is billed for it. Defaults to true." }
          },
          "required": [],
          "additionalProperties": false
        }
      }
    }
    """;

    private const string ChecklistFields = """
    {
      "questions": {
        "type": "array",
        "description": "(checklists) The questions, in order; replaces the list. Give a question's id to keep it, and its answers on work orders made from now on.",
        "items": {
          "type": "object",
          "properties": {
            "id": { "type": "string", "description": "The ID of a question the checklist already has. Leave it out for a new question." },
            "section": { "type": "string", "description": "The heading the question is shown under." },
            "text": { "type": "string", "description": "The question. Needed for a new question." },
            "kind": { "type": "string", "enum": ["YesNo", "PassFailNA", "Text", "Number", "Choice", "MultiChoice", "Photo", "Signature", "MeterReading", "Date"], "description": "The kind of answer it takes. Needed for a new question." },
            "required": { "type": "boolean", "description": "Whether it must be answered before the work order can be completed." },
            "choices": { "type": "array", "items": { "type": "string" }, "description": "The answers to choose from, at least two, for Choice and MultiChoice." },
            "min": { "type": "number", "description": "The lowest number accepted, for Number and MeterReading." },
            "max": { "type": "number", "description": "The highest number accepted, for Number and MeterReading." },
            "unit": { "type": "string", "description": "The unit of a Number, or the meter of a MeterReading." },
            "help": { "type": "string", "description": "A hint shown with the question." },
            "showWhenQuestionId": { "type": "string", "description": "Show the question only when this earlier, already saved question has the answer showWhenEquals." },
            "showWhenEquals": { "type": "string", "description": "The answer of showWhenQuestionId that shows the question." }
          },
          "required": [],
          "additionalProperties": false
        }
      }
    }
    """;

    private const string ServiceLevelFields = """
    {
      "hoursCalendarId": { "type": "string", "description": "(service-levels) The ID of the Business Hours schedule the clock runs in, as Content > Business Hours lists them. Empty text clears it, and the clock runs around the clock." },
      "warningPercent": { "type": "integer", "minimum": 1, "maximum": 99, "description": "(service-levels) How far through a target, in percent, the warning comes." },
      "pauseStatuses": { "type": "array", "items": { "type": "string", "enum": ["Ready", "Scheduled", "InProgress", "OnHold"] }, "description": "(service-levels) The statuses that stop the clock; replaces the list. A new service level stops it in OnHold." },
      "targets": {
        "type": "array",
        "description": "(service-levels) The response and resolution targets by priority. Only the priorities given change; give 0 for both to remove a priority's target.",
        "items": {
          "type": "object",
          "properties": {
            "priority": { "type": "string", "enum": ["Low", "Normal", "High", "Urgent"], "description": "The priority." },
            "responseHours": { "type": "number", "minimum": 0, "description": "Hours to respond, 0 for no target." },
            "resolveHours": { "type": "number", "minimum": 0, "description": "Hours to resolve, 0 for no target." }
          },
          "required": ["priority"],
          "additionalProperties": false
        }
      }
    }
    """;

    private const string SubStatusFields = """
    {
      "parentStatus": { "type": "string", "enum": ["New", "Ready", "Scheduled", "InProgress", "OnHold", "Completed", "Closed", "Canceled"], "description": "(sub-statuses) The status it is a finer state of. A new one goes under OnHold." },
      "pausesSla": { "type": "boolean", "description": "(sub-statuses) Whether the service level clock stops while a work order is in it." }
    }
    """;

    private static readonly Dictionary<string, string[]> _kindFields = new(StringComparer.Ordinal)
    {
        [WorkOrderSettingKinds.Types] = ["name", "color", "defaultDurationMinutes", "crewSize", "defaultPriority", "serviceLevelPolicyId", "billingMode", "currency", "fixedPrice", "requiresSignature", "requiredPhotos", "checklistTemplateIds", "requiredSkills", "requiredCertificationIds", "appointmentTypeIds", "defaultLines"],
        [WorkOrderSettingKinds.Checklists] = ["name", "questions"],
        [WorkOrderSettingKinds.ServiceLevels] = ["name", "hoursCalendarId", "warningPercent", "pauseStatuses", "targets"],
        [WorkOrderSettingKinds.SubStatuses] = ["name", "color", "parentStatus", "pausesSla"],
    };

    private static readonly JsonObject _allFields = Merge(TypeFields, ChecklistFields, ServiceLevelFields, SubStatusFields);

    /// <summary>
    /// Gets every field of every kind.
    /// </summary>
    public static IEnumerable<string> AllFields => _allFields.Select(property => property.Key);

    /// <summary>
    /// Gets the fields of a kind.
    /// </summary>
    public static IReadOnlyList<string> FieldsOf(string kind)
        => _kindFields.GetValueOrDefault(kind) ?? [];

    /// <summary>
    /// Gets the properties of every field, for the save tool's schema.
    /// </summary>
    public static string AllFieldsJson()
        => _allFields.ToJsonString();

    /// <summary>
    /// Gets the JSON schema of the fields of a kind.
    /// </summary>
    public static JsonNode SchemaOf(string kind)
    {
        var properties = new JsonObject();

        foreach (var field in FieldsOf(kind))
        {
            properties[field] = _allFields[field]?.DeepClone();
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };
    }

    /// <summary>
    /// Parses a kind, as the Work Order Settings pages name them.
    /// </summary>
    public static string ParseKind(string text)
        => WorkOrderSettingKinds.All.FirstOrDefault(kind => string.Equals(kind, text?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gives a type's fields.
    /// </summary>
    public static object Record(WorkOrderType type)
        => new
        {
            id = type.ItemId,
            name = type.Name,
            color = type.Color,
            defaultDurationMinutes = type.DefaultDurationMinutes,
            crewSize = type.CrewSize,
            defaultPriority = type.DefaultPriority.ToString(),
            serviceLevelPolicyId = type.ServiceLevelPolicyId,
            billingMode = type.BillingMode.ToString(),
            currency = type.Currency,
            fixedPrice = type.FixedPrice,
            requiresSignature = type.RequiresSignature,
            requiredPhotos = type.RequiredPhotos,
            checklistTemplateIds = type.ChecklistTemplateIds,
            requiredSkills = type.RequiredSkills.Select(skill => new { skillId = skill.SkillId, minProficiency = skill.MinProficiency }),
            requiredCertificationIds = type.RequiredCertificationIds,
            requiredEquipmentTypeIds = type.RequiredEquipmentTypeIds,
            appointmentTypeIds = type.AppointmentTypeIds,
            defaultLines = type.DefaultLines.Select(line => new
            {
                id = line.Id,
                kind = line.Kind.ToString(),
                description = line.Description,
                stockItemId = line.StockItemId,
                quantity = line.QuantityEstimated,
                unitPrice = line.UnitPrice,
                billable = line.Billable,
            }),
        };

    /// <summary>
    /// Gives a checklist's fields.
    /// </summary>
    public static object Record(ChecklistTemplate checklist)
        => new
        {
            id = checklist.ItemId,
            name = checklist.Name,
            questions = checklist.Questions.Select(question => new
            {
                id = question.Id,
                section = question.Section,
                text = question.Text,
                kind = question.Kind.ToString(),
                required = question.Required,
                choices = question.Choices,
                min = question.Min,
                max = question.Max,
                unit = question.Unit ?? question.Meter,
                help = question.Help,
                showWhenQuestionId = question.GetRule()?.Conditions.FirstOrDefault()?.Source,
                showWhenEquals = question.GetRule()?.Conditions.FirstOrDefault()?.Values.FirstOrDefault(),
            }),
        };

    /// <summary>
    /// The work order statuses a service level can stop the clock in, as its editor offers them.
    /// </summary>
    public static readonly WorkOrderStatus[] PausableStatuses =
    [
        WorkOrderStatus.Scheduled,
        WorkOrderStatus.Ready,
        WorkOrderStatus.InProgress,
        WorkOrderStatus.OnHold,
    ];

    /// <summary>
    /// Whether a shared service level policy is one of the work orders' (the Work orders scope of Service Levels).
    /// </summary>
    public static bool IsWorkOrderPolicy(ServiceLevelPolicy policy)
        => policy is not null && string.Equals(policy.Scope, WorkOrdersConstants.ServiceLevelScope, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gives a work order service level's fields: a shared Service Levels policy of the Work orders scope, with its
    /// response and resolution targets in hours by priority, and the statuses that stop its clock. A target with no
    /// priority applies to every priority that has none of its own.
    /// </summary>
    public static object Record(ServiceLevelPolicy policy)
        => new
        {
            id = policy.ItemId,
            name = policy.Name,
            hoursCalendarId = policy.HoursScheduleId,
            warningPercent = policy.WarningPercent,
            pauseStatuses = (policy.TryGet<WorkOrderServiceLevelSettings>(out var settings) && settings is not null ? settings.PauseStatuses : []).Select(status => status.ToString()),
            targets = (policy.Targets ?? [])
                .Where(target => target is not null && target.Minutes > 0)
                .GroupBy(target => target.Priority, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    priority = group.Key,
                    responseHours = Hours(group.FirstOrDefault(target => string.Equals(target.Metric, ServiceLevelMetrics.FirstResponse, StringComparison.OrdinalIgnoreCase))?.Minutes ?? 0),
                    resolveHours = Hours(group.FirstOrDefault(target => string.Equals(target.Metric, ServiceLevelMetrics.Resolution, StringComparison.OrdinalIgnoreCase))?.Minutes ?? 0),
                }),
        };

    /// <summary>
    /// Gives a sub-status's fields.
    /// </summary>
    public static object Record(WorkOrderSubStatus subStatus)
        => new
        {
            id = subStatus.ItemId,
            name = subStatus.Name,
            parentStatus = subStatus.ParentStatus.ToString(),
            color = subStatus.Color,
            pausesSla = subStatus.PausesSla,
        };

    /// <summary>
    /// Overlays the given type fields on the type editor's fields, checking the records they name.
    /// </summary>
    public static async Task<string> OverlayAsync(AIFunctionArguments arguments, WorkOrderTypeEditViewModel form, IServiceProvider services, CancellationToken cancellationToken)
    {
        OverlayText(arguments, "name", value => form.Name = value);
        OverlayText(arguments, "color", value => form.Color = value);
        OverlayText(arguments, "currency", value => form.Currency = value);

        if (ReadInt(arguments, "defaultDurationMinutes", value => form.DefaultDurationMinutes = value) is { } duration)
        {
            return duration;
        }

        if (ReadInt(arguments, "crewSize", value => form.CrewSize = value) is { } crew)
        {
            return crew;
        }

        if (ReadInt(arguments, "requiredPhotos", value => form.RequiredPhotos = value) is { } photos)
        {
            return photos;
        }

        if (ReadEnum<WorkOrderPriority>(arguments, "defaultPriority", value => form.DefaultPriority = value) is { } priority)
        {
            return priority;
        }

        if (ReadEnum<WorkOrderBillingMode>(arguments, "billingMode", value => form.BillingMode = value) is { } billing)
        {
            return billing;
        }

        if (WorkOrderToolSupport.Has(arguments, "fixedPrice"))
        {
            if (WorkOrderToolSupport.ReadDecimal(arguments, "fixedPrice") is not { } price)
            {
                return "The fixedPrice must be a number.";
            }

            form.FixedPrice = price;
        }

        if (WorkOrderToolSupport.ReadBool(arguments, "requiresSignature") is { } signature)
        {
            form.RequiresSignature = signature;
        }

        if (WorkOrderToolSupport.Has(arguments, "serviceLevelPolicyId"))
        {
            var policyId = WorkOrderToolSupport.Read(arguments, "serviceLevelPolicyId");

            if (policyId is not null && !IsWorkOrderPolicy(await services.GetRequiredService<IServiceLevelPolicyManager>().FindByIdAsync(policyId, cancellationToken)))
            {
                return $"No service level has the ID '{policyId}'. Use listWorkOrderSettings with kind service-levels.";
            }

            form.ServiceLevelPolicyId = policyId;
        }

        if (WorkOrderToolSupport.ReadList(arguments, "checklistTemplateIds") is { } checklistIds)
        {
            var checklists = services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>();

            foreach (var checklistId in checklistIds)
            {
                if (await checklists.FindByIdAsync(checklistId, cancellationToken) is null)
                {
                    return $"No checklist has the ID '{checklistId}'. Use listWorkOrderSettings with kind checklists, or create it first.";
                }
            }

            form.ChecklistTemplateIds = [.. checklistIds];
        }

        var lookups = services.GetRequiredService<WorkOrderLookups>();

        if (WorkOrderToolSupport.Has(arguments, "requiredSkills"))
        {
            if (WorkOrderToolSupport.ReadObjects(arguments, "requiredSkills") is not { } skills)
            {
                return "The requiredSkills must be a list of objects with a skillId and a minProficiency.";
            }

            var known = (await lookups.SkillsAsync(cancellationToken)).Select(option => option.Value).ToHashSet(StringComparer.Ordinal);
            var rows = new List<SkillRequirement>();

            foreach (var skill in skills)
            {
                var skillId = WorkOrderToolSupport.GetText(skill, "skillId");

                if (skillId is null || !known.Contains(skillId))
                {
                    return $"No enabled skill has the ID '{skillId}'. Skills come from the Skills feature.";
                }

                rows.Add(new SkillRequirement { SkillId = skillId, MinProficiency = WorkOrderToolSupport.GetInt(skill, "minProficiency") ?? 1 });
            }

            form.RequiredSkills = rows;
        }

        if (WorkOrderToolSupport.ReadList(arguments, "requiredCertificationIds") is { } certificationIds)
        {
            if (await UnknownAsync(certificationIds, lookups.CertificationsAsync(cancellationToken)) is { } unknown)
            {
                return $"No enabled certification has the ID '{unknown}'. Certifications come from the Skills feature.";
            }

            form.RequiredCertificationIds = [.. certificationIds];
        }

        if (WorkOrderToolSupport.ReadList(arguments, "appointmentTypeIds") is { } appointmentTypeIds)
        {
            if (await UnknownAsync(appointmentTypeIds, lookups.AppointmentTypesAsync(cancellationToken)) is { } unknown)
            {
                return $"No Scheduling appointment type has the ID '{unknown}'.";
            }

            form.AppointmentTypeIds = [.. appointmentTypeIds];
        }

        if (WorkOrderToolSupport.Has(arguments, "defaultLines"))
        {
            if (WorkOrderToolSupport.ReadObjects(arguments, "defaultLines") is not { } lines)
            {
                return "The defaultLines must be a list of objects with kind, description, quantity and unitPrice.";
            }

            if (ReadLines(lines, form.Lines, "The type", out var rows) is { } refused)
            {
                return refused;
            }

            form.Lines = rows;
        }

        return null;
    }

    /// <summary>
    /// Reads the lines given to replace a list of editor rows. A line with the ID of a row starts from that row, so only
    /// the properties given change; a line without an ID is new and needs its kind and description.
    /// </summary>
    public static string ReadLines(IReadOnlyList<JsonElement> lines, IList<LineEditModel> existing, string owner, out List<LineEditModel> rows)
    {
        rows = [];

        foreach (var line in lines)
        {
            var id = WorkOrderToolSupport.GetText(line, "id");
            var current = id is null ? null : existing.FirstOrDefault(row => row.Id == id);

            if (id is not null && current is null)
            {
                return $"{owner} has no line with the ID '{id}'. Leave the id out to add a line.";
            }

            var row = new LineEditModel
            {
                Id = id,
                Kind = current?.Kind ?? WorkOrderLineKind.Product,
                Description = current?.Description,
                StockItemId = current?.StockItemId,
                Quantity = current?.Quantity ?? 1m,
                UnitPrice = current?.UnitPrice ?? 0m,
                Billable = current?.Billable ?? true,
            };

            if (WorkOrderToolSupport.HasProperty(line, "kind") || current is null)
            {
                if (!WorkOrderToolSupport.TryParseEnum<WorkOrderLineKind>(WorkOrderToolSupport.GetText(line, "kind"), out var kind))
                {
                    return $"Each line's kind must be one of {WorkOrderToolSupport.Names<WorkOrderLineKind>()}.";
                }

                row.Kind = kind;
            }

            if (WorkOrderToolSupport.HasProperty(line, "description"))
            {
                row.Description = WorkOrderToolSupport.GetText(line, "description");
            }

            if (string.IsNullOrWhiteSpace(row.Description))
            {
                return "Each line needs a description.";
            }

            if (WorkOrderToolSupport.HasProperty(line, "stockItemId"))
            {
                row.StockItemId = WorkOrderToolSupport.GetText(line, "stockItemId");
            }

            if (WorkOrderToolSupport.HasProperty(line, "quantity"))
            {
                if (WorkOrderToolSupport.GetDecimal(line, "quantity") is not { } quantity || quantity < 0)
                {
                    return $"The quantity of \"{row.Description}\" must be a number, 0 or more.";
                }

                row.Quantity = quantity;
            }

            if (WorkOrderToolSupport.HasProperty(line, "unitPrice"))
            {
                if (WorkOrderToolSupport.GetDecimal(line, "unitPrice") is not { } unitPrice || unitPrice < 0)
                {
                    return $"The unit price of \"{row.Description}\" must be a number, 0 or more.";
                }

                row.UnitPrice = unitPrice;
            }

            if (WorkOrderToolSupport.GetBool(line, "billable") is { } billable)
            {
                row.Billable = billable;
            }

            rows.Add(row);
        }

        return null;
    }

    /// <summary>
    /// Overlays the given checklist fields on the checklist editor's fields.
    /// </summary>
    public static string Overlay(AIFunctionArguments arguments, ChecklistTemplateEditViewModel form)
    {
        OverlayText(arguments, "name", value => form.Name = value);

        if (!WorkOrderToolSupport.Has(arguments, "questions"))
        {
            return null;
        }

        if (WorkOrderToolSupport.ReadObjects(arguments, "questions") is not { } questions)
        {
            return "The questions must be a list of objects with text and kind.";
        }

        var rows = new List<QuestionEditModel>();

        foreach (var question in questions)
        {
            // A question with the ID of a saved one starts from it, so only the properties given change.
            var id = WorkOrderToolSupport.GetText(question, "id");
            var current = id is null ? null : form.Questions.FirstOrDefault(row => row.Id == id);

            if (id is not null && current is null)
            {
                return $"The checklist has no question with the ID '{id}'. Leave the id out to add a question.";
            }

            var row = new QuestionEditModel
            {
                Id = id,
                Section = current?.Section,
                Text = current?.Text,
                Kind = current?.Kind ?? ChecklistQuestionKind.Text,
                Required = current?.Required ?? false,
                Choices = current?.Choices,
                Min = current?.Min,
                Max = current?.Max,
                Unit = current?.Unit,
                Help = current?.Help,
                ShowWhenQuestionId = current?.ShowWhenQuestionId,
                ShowWhenEquals = current?.ShowWhenEquals,
            };

            if (WorkOrderToolSupport.HasProperty(question, "text"))
            {
                row.Text = WorkOrderToolSupport.GetText(question, "text");
            }

            if (string.IsNullOrWhiteSpace(row.Text))
            {
                return "Each question needs its text.";
            }

            if (WorkOrderToolSupport.HasProperty(question, "kind") || current is null)
            {
                if (!WorkOrderToolSupport.TryParseEnum<ChecklistQuestionKind>(WorkOrderToolSupport.GetText(question, "kind"), out var kind))
                {
                    return $"The kind of \"{row.Text}\" must be one of {WorkOrderToolSupport.Names<ChecklistQuestionKind>()}.";
                }

                row.Kind = kind;
            }

            foreach (var name in new[] { "min", "max" })
            {
                if (!WorkOrderToolSupport.HasProperty(question, name))
                {
                    continue;
                }

                if (WorkOrderToolSupport.GetDecimal(question, name) is not { } number)
                {
                    return $"The min and max of \"{row.Text}\" must be numbers.";
                }

                if (name == "min")
                {
                    row.Min = number;
                }
                else
                {
                    row.Max = number;
                }
            }

            if (WorkOrderToolSupport.HasProperty(question, "section"))
            {
                row.Section = WorkOrderToolSupport.GetText(question, "section");
            }

            if (WorkOrderToolSupport.GetBool(question, "required") is { } required)
            {
                row.Required = required;
            }

            if (WorkOrderToolSupport.HasProperty(question, "choices"))
            {
                row.Choices = string.Join('\n', WorkOrderToolSupport.GetList(question, "choices") ?? []);
            }

            if (WorkOrderToolSupport.HasProperty(question, "unit"))
            {
                row.Unit = WorkOrderToolSupport.GetText(question, "unit");
            }

            if (WorkOrderToolSupport.HasProperty(question, "help"))
            {
                row.Help = WorkOrderToolSupport.GetText(question, "help");
            }

            if (WorkOrderToolSupport.HasProperty(question, "showWhenQuestionId"))
            {
                row.ShowWhenQuestionId = WorkOrderToolSupport.GetText(question, "showWhenQuestionId");
            }

            if (WorkOrderToolSupport.HasProperty(question, "showWhenEquals"))
            {
                row.ShowWhenEquals = WorkOrderToolSupport.GetText(question, "showWhenEquals");
            }

            rows.Add(row);
        }

        form.Questions = rows;

        return null;
    }

    /// <summary>
    /// Applies the given service level fields to a work order service level, a shared Service Levels policy, the way
    /// the policy editor and the work orders' part of it do: the targets in hours become the response and resolution
    /// targets in minutes of their priority (0 removes one), the pause statuses are kept with the policy, and the
    /// hours schedule is checked by the policy's handler when the manager validates it.
    /// </summary>
    public static string ApplyPolicy(AIFunctionArguments arguments, ServiceLevelPolicy policy, bool isNew)
    {
        OverlayText(arguments, "name", value => policy.Name = value);

        if (WorkOrderToolSupport.Has(arguments, "hoursCalendarId"))
        {
            policy.HoursScheduleId = WorkOrderToolSupport.Read(arguments, "hoursCalendarId");
        }

        if (ReadInt(arguments, "warningPercent", value => policy.WarningPercent = value) is { } warning)
        {
            return warning;
        }

        var settings = policy.TryGet<WorkOrderServiceLevelSettings>(out var stored) && stored is not null
            ? stored
            : new WorkOrderServiceLevelSettings { PauseStatuses = isNew ? [WorkOrderStatus.OnHold] : [] };

        if (WorkOrderToolSupport.ReadList(arguments, "pauseStatuses") is { } pauseStatuses)
        {
            var parsed = new List<WorkOrderStatus>();

            foreach (var status in pauseStatuses)
            {
                if (!WorkOrderToolSupport.TryParseEnum<WorkOrderStatus>(status, out var value) || !PausableStatuses.Contains(value))
                {
                    return $"'{status}' is not a status that can stop the clock. Use {string.Join(", ", PausableStatuses)}.";
                }

                parsed.Add(value);
            }

            settings.PauseStatuses = [.. parsed.Distinct()];
        }

        policy.Put(settings);

        if (WorkOrderToolSupport.Has(arguments, "targets"))
        {
            if (WorkOrderToolSupport.ReadObjects(arguments, "targets") is not { } targets)
            {
                return "The targets must be a list of objects with a priority, responseHours and resolveHours.";
            }

            policy.Targets ??= [];

            foreach (var target in targets)
            {
                if (!WorkOrderToolSupport.TryParseEnum<WorkOrderPriority>(WorkOrderToolSupport.GetText(target, "priority"), out var priority))
                {
                    return $"Each target's priority must be one of {WorkOrderToolSupport.Names<WorkOrderPriority>()}.";
                }

                foreach (var (name, metric) in new[] { ("responseHours", ServiceLevelMetrics.FirstResponse), ("resolveHours", ServiceLevelMetrics.Resolution) })
                {
                    if (!WorkOrderToolSupport.HasProperty(target, name))
                    {
                        continue;
                    }

                    if (WorkOrderToolSupport.GetDecimal(target, name) is not { } hours || hours < 0)
                    {
                        return $"The {name} of {priority} must be a number of hours, 0 or more.";
                    }

                    // The targets follow the priority's key, which the policy's handler keeps; the editor's grid has a
                    // cell for each metric and priority, and an empty cell is no target.
                    policy.Targets = [.. policy.Targets.Where(existing => existing is null
                        || !string.Equals(existing.Metric, metric, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(existing.Priority, priority.ToString(), StringComparison.OrdinalIgnoreCase))];

                    var minutes = hours > 0 ? (int)Math.Round(hours * 60m) : 0;

                    if (minutes > 0)
                    {
                        policy.Targets.Add(new ServiceLevelTarget { Metric = metric, Priority = priority.ToString(), Minutes = minutes });
                    }
                }
            }
        }

        return null;
    }

    private static decimal Hours(int minutes)
        => minutes > 0 ? Math.Round(minutes / 60m, 2) : 0;

    /// <summary>
    /// Overlays the given sub-status fields on the sub-status editor's fields.
    /// </summary>
    public static string Overlay(AIFunctionArguments arguments, WorkOrderSubStatusEditViewModel form)
    {
        OverlayText(arguments, "name", value => form.Name = value);
        OverlayText(arguments, "color", value => form.Color = value);

        if (ReadEnum<WorkOrderStatus>(arguments, "parentStatus", value => form.ParentStatus = value) is { } refused)
        {
            return refused;
        }

        if (WorkOrderToolSupport.ReadBool(arguments, "pausesSla") is { } pauses)
        {
            form.PausesSla = pauses;
        }

        return null;
    }

    private static void OverlayText(AIFunctionArguments arguments, string name, Action<string> set)
    {
        if (WorkOrderToolSupport.Has(arguments, name))
        {
            set(WorkOrderToolSupport.ReadRaw(arguments, name));
        }
    }

    private static string ReadInt(AIFunctionArguments arguments, string name, Action<int> set)
    {
        if (!WorkOrderToolSupport.Has(arguments, name))
        {
            return null;
        }

        if (WorkOrderToolSupport.ReadInt(arguments, name) is not { } value)
        {
            return $"The {name} must be a whole number.";
        }

        set(value);

        return null;
    }

    private static string ReadEnum<TEnum>(AIFunctionArguments arguments, string name, Action<TEnum> set)
        where TEnum : struct, Enum
    {
        if (!WorkOrderToolSupport.Has(arguments, name))
        {
            return null;
        }

        if (!WorkOrderToolSupport.TryParseEnum<TEnum>(WorkOrderToolSupport.Read(arguments, name), out var value))
        {
            return $"The {name} must be one of {WorkOrderToolSupport.Names<TEnum>()}.";
        }

        set(value);

        return null;
    }

    private static async Task<string> UnknownAsync(IEnumerable<string> ids, Task<List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> options)
    {
        var known = (await options).Select(option => option.Value).ToHashSet(StringComparer.Ordinal);

        return ids.FirstOrDefault(id => !known.Contains(id));
    }

    private static JsonObject Merge(params string[] fragments)
    {
        var merged = new JsonObject();

        foreach (var fragment in fragments)
        {
            foreach (var property in JsonNode.Parse(fragment)!.AsObject())
            {
                merged[property.Key] = property.Value?.DeepClone();
            }
        }

        return merged;
    }
}
