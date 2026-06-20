using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.A2A.Recipes;

internal sealed class A2AConnectionStep : NamedRecipeStepHandler
{
    public const string StepKey = "A2AConnection";

    private readonly ICatalogManager<A2AConnection> _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AConnectionStep"/> class.
    /// </summary>
    /// <param name="manager">The connection manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public A2AConnectionStep(
        ICatalogManager<A2AConnection> manager,
        IStringLocalizer<A2AConnectionStep> stringLocalizer)
    : base(StepKey)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<A2AConnectionsStepModel>();
        var tokens = model.Connections.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            A2AConnection connection = null;
            var isNew = false;

            var id = token[nameof(A2AConnection.ItemId)]?.GetValue<string>();

            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                connection = await _manager.FindByIdAsync(id);
            }

            if (connection is not null)
            {
                await _manager.UpdateAsync(connection, token);
            }
            else
            {
                isNew = true;
                connection = await _manager.NewAsync(token);

                if (hasId && UniqueId.IsValid(id))
                {
                    connection.ItemId = id;
                }
            }

            var validationResult = await _manager.ValidateAsync(connection);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            if (isNew)
            {
                await _manager.CreateAsync(connection);
            }
        }
    }

    private sealed class A2AConnectionsStepModel
    {
        /// <summary>
        /// Gets or sets the A2A connections to import.
        /// </summary>
        public JsonArray Connections { get; set; }
    }
}
