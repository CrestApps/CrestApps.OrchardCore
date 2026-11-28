using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Adds an internal note to a transaction's timeline under the caller's name, as the Add note action of its page
/// (<see cref="TransactionAdminActions.AddNote"/>), saved and committed at once.
/// </summary>
internal sealed class AddTransactionNoteTool : TransactionAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "addTransactionNote";

    private const int MaxNoteLength = 2000;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "transactionId": {
          "type": "string",
          "description": "The transaction's id, from listTransactions."
        },
        "note": {
          "type": "string",
          "description": "The note, up to 2000 characters. The customer does not see it."
        }
      },
      "required": ["transactionId", "note"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds an internal note to a transaction's timeline under the current user's name.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var note = ReadString(arguments, "note");

        if (note is null || note.Length > MaxNoteLength)
        {
            return $"Nothing was added: give a note of up to {MaxNoteLength} characters.";
        }

        var (transaction, error) = await FindTransactionAsync(services, ReadString(arguments, "transactionId"), cancellationToken);

        if (transaction is null)
        {
            return error;
        }

        var actorId = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        string actorName = null;

        if (!string.IsNullOrEmpty(actorId) &&
            services.GetService<IUserService>() is { } userService &&
            services.GetService<IDisplayNameProvider>() is { } displayNames &&
            await userService.GetUserByUniqueIdAsync(actorId) is { } user)
        {
            actorName = await displayNames.GetAsync(user, cancellationToken);
        }

        TransactionAdminActions.AddNote(transaction, note, services.GetRequiredService<IClock>().UtcNow, actorId, actorName ?? caller.Identity?.Name);

        if (await SaveAsync(services, transaction, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        return Json(new
        {
            id = transaction.ItemId,
            note,
            by = actorName ?? caller.Identity?.Name,
            message = "The note was added.",
        });
    }
}
