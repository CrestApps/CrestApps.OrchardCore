using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Saves answers to one of a work order's checklists, as the checklist form of its page does: each answer is checked
/// against its question, the ones that fit are saved, and the others are reported.
/// </summary>
internal sealed class AnswerWorkOrderChecklistTool : WorkOrderToolBase
{
    public const string ToolName = "answerWorkOrderChecklist";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "number": { "type": "string", "description": "The work order's number, such as WO-000123, from listWorkOrders. Its ID works too." },
        "checklist": { "type": "string", "description": "The checklist's name, or its position on the work order from 1. When it matches none, the checklists and their questions are returned." },
        "answers": {
          "type": "array",
          "description": "The answers to save.",
          "items": {
            "type": "object",
            "properties": {
              "question": { "type": "string", "description": "The question's ID, or its exact text." },
              "value": { "type": ["string", "number", "boolean", "array"], "items": { "type": "string" }, "description": "The answer: Yes or No; Pass, Fail or N/A; a number within the question's limits; one of its choices; a list of its choices for a multiple choice question; a date as yyyy-MM-dd; or text. Empty text clears the answer." }
            },
            "required": ["question", "value"],
            "additionalProperties": false
          }
        }
      },
      "required": ["number", "checklist", "answers"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Saves answers to the questions of one of a work order's checklists. Each answer must fit the question's kind and limits; the ones that fit are saved and the others are reported.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ManageWorkOrders;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var workOrder = await WorkOrderToolSupport.FindWorkOrderAsync(services, WorkOrderToolSupport.Read(arguments, "number"), cancellationToken);

        if (workOrder is null)
        {
            return WorkOrderToolSupport.WorkOrderNotFound;
        }

        var reference = WorkOrderToolSupport.Read(arguments, "checklist");
        var index = FindChecklist(workOrder, reference);

        if (index < 0)
        {
            return WorkOrderToolSupport.Json(new
            {
                error = workOrder.Checklists.Count == 0
                    ? $"{workOrder.Number} has no checklists."
                    : $"No single checklist of {workOrder.Number} matches '{reference}'. Pick one of these by its position or name.",
                checklists = WorkOrderToolResults.Checklists(workOrder),
            });
        }

        var checklist = workOrder.Checklists[index];

        if (WorkOrderToolSupport.ReadObjects(arguments, "answers") is not { Count: > 0 } answers)
        {
            return "Give the answers as a list of objects with a question and a value.";
        }

        var resolved = new List<(ChecklistQuestion Question, string Value)>();

        foreach (var answer in answers)
        {
            var question = FindQuestion(checklist, WorkOrderToolSupport.GetText(answer, "question"));

            if (question is null)
            {
                return WorkOrderToolSupport.Json(new
                {
                    error = $"No single question of {checklist.Name} matches '{WorkOrderToolSupport.GetText(answer, "question")}'. Use a question's id or exact text; nothing was saved.",
                    checklist = WorkOrderToolResults.Checklist(checklist, index),
                });
            }

            resolved.Add((question, Value(answer)));
        }

        var service = services.GetRequiredService<WorkOrderService>();
        var saved = new List<string>();
        var errors = new List<string>();

        foreach (var (question, value) in resolved)
        {
            var current = checklist.Answers.FirstOrDefault(answer => answer.QuestionId == question.Id)?.Value;

            if (string.Equals(string.IsNullOrWhiteSpace(value) ? null : value.Trim(), current, StringComparison.Ordinal))
            {
                saved.Add(question.Text);

                continue;
            }

            var result = await service.AnswerAsync(workOrder.ItemId, index, question.Id, value, user.Identity?.Name, cancellationToken);

            if (result.Succeeded)
            {
                saved.Add(question.Text);
            }
            else
            {
                errors.AddRange(result.Errors);
            }
        }

        if (saved.Count > 0 && await WorkOrderToolSupport.SaveAsync(services, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        var stillRequired = checklist.Questions
            .Where(question => question.Required && WorkOrderService.IsShown(checklist, question) && !checklist.Answers.Any(answer => answer.QuestionId == question.Id))
            .Select(question => question.Text)
            .ToList();

        return WorkOrderToolSupport.Json(new
        {
            id = workOrder.ItemId,
            number = workOrder.Number,
            checklist = checklist.Name,
            saved,
            refused = errors,
            stillRequired,
            adminPath = WorkOrderToolSupport.WorkOrderPath(services, workOrder.ItemId),
        });
    }

    private static int FindChecklist(WorkOrder workOrder, string reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return -1;
        }

        if (int.TryParse(reference, NumberStyles.None, CultureInfo.InvariantCulture, out var position) && position >= 1 && position <= workOrder.Checklists.Count)
        {
            return position - 1;
        }

        var matches = workOrder.Checklists
            .Select((checklist, index) => (checklist, index))
            .Where(entry => string.Equals(entry.checklist.Name?.Trim(), reference, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0].index : -1;
    }

    private static ChecklistQuestion FindQuestion(WorkOrderChecklist checklist, string reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        if (checklist.Questions.FirstOrDefault(question => question.Id == reference) is { } byId)
        {
            return byId;
        }

        var matches = checklist.Questions.Where(question => string.Equals(question.Text?.Trim(), reference, StringComparison.OrdinalIgnoreCase)).ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    // A list answers a multiple choice question, one choice per line as the page posts it.
    private static string Value(JsonElement answer)
    {
        if (!answer.TryGetProperty("value", out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Array => string.Join('\n', value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim())),
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }
}
