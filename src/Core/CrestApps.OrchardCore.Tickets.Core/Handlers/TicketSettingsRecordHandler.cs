using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Core.Handlers;

/// <summary>
/// Keeps a ticket settings record (a status, a priority, a desk or a saved view) tidy: a recipe's JSON is applied, the
/// time stamps are set, and the name is required and unique.
/// </summary>
/// <typeparam name="T">The record.</typeparam>
public sealed class TicketSettingsRecordHandler<T> : CatalogEntryHandlerBase<T>
    where T : CatalogItem, ITicketSettingsRecord
{
    private readonly ICatalog<T> _store;
    private readonly IClock _clock;
    private readonly IServiceProvider _services;

    // The owner names of views being imported that match no user here, until the view is validated.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TicketView, string> _unknownOwners = new();

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketSettingsRecordHandler{T}"/> class.
    /// </summary>
    /// <param name="store">The records.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketSettingsRecordHandler(ICatalog<T> store, IClock clock, IServiceProvider services, IStringLocalizer<TicketSettingsRecordHandler<T>> stringLocalizer)
    {
        _services = services;
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task InitializingAsync(InitializingContext<T> context, CancellationToken cancellationToken = default)
    {
        // A recipe creates the record from its JSON; the editor hands none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        await ResolveOwnerAsync(context.Model, context.Data);
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<T> context, CancellationToken cancellationToken = default)
    {
        context.Model.Name = context.Model.Name?.Trim();

        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task UpdatingAsync(UpdatingContext<T> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);
        await ResolveOwnerAsync(context.Model, context.Data);
        context.Model.Name = context.Model.Name?.Trim();
        context.Model.ModifiedUtc = _clock.UtcNow;
    }

    // A personal view travels with its owner's user name (a user id differs from site to site), which is turned into the
    // user of this site here; a name with no user here is kept aside so validation refuses the view with that name.
    private async Task ResolveOwnerAsync(T model, System.Text.Json.Nodes.JsonNode data)
    {
        if (model is not TicketView view
            || data is not System.Text.Json.Nodes.JsonObject json
            || json[TicketView.OwnerUserNameProperty] is not System.Text.Json.Nodes.JsonValue value
            || !value.TryGetValue<string>(out var userName)
            || string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        var user = _services.GetService<UserManager<IUser>>() is { } users ? await users.FindByNameAsync(userName.Trim()) : null;

        if (user is not null && _services.GetService<UserManager<IUser>>() is { } manager)
        {
            view.OwnerId = await manager.GetUserIdAsync(user);
            _unknownOwners.Remove(view);
        }
        else
        {
            _unknownOwners.AddOrUpdate(view, userName.Trim());
        }
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public override async Task UpdatedAsync(UpdatedContext<T> context, CancellationToken cancellationToken = default)
    {
        // Tickets keep copies of a status's category and a priority's rank; a step brings them in line with the record.
        var key = context.Model switch
        {
            TicketStatus status => TicketSettingsChangesStep.StatusPrefix + status.ItemId,
            TicketPriority priority => TicketSettingsChangesStep.PriorityPrefix + priority.ItemId,
            _ => null,
        };

        if (key is not null && _services.GetService<ResumableSteps>() is { } steps)
        {
            await steps.RequestAsync(TicketSettingsChangesStep.StepName, new Dictionary<string, string> { [key] = "0" });
        }
    }

    public override async Task ValidatingAsync(ValidatingContext<T> context, CancellationToken cancellationToken = default)
    {
        var name = context.Model.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(ITicketSettingsRecord.Name)]));

            return;
        }

        if (context.Model is TicketDesk desk)
        {
            if (desk.ReopenWindowDays < 0 || desk.AutoCloseDays < 0 || desk.RatingRequestDelayHours < 0)
            {
                context.Result.Fail(new ValidationResult(S["Days can't be negative."], [nameof(TicketDesk.AutoCloseDays)]));
            }

            if (!string.IsNullOrEmpty(desk.AssignmentStrategy)
                && desk.AssignmentStrategy is not (TeamAssignmentStrategies.LeastLoaded or TeamAssignmentStrategies.RoundRobin))
            {
                context.Result.Fail(new ValidationResult(S["Choose how new tickets are assigned."], [nameof(TicketDesk.AssignmentStrategy)]));
            }
        }

        // A badge color is written into a style attribute, so only a hex color or a color name is kept, from the editor
        // and from a recipe alike.
        var color = context.Model switch
        {
            TicketStatus status => status.Color,
            TicketPriority priority => priority.Color,
            _ => null,
        };

        if (!string.IsNullOrEmpty(color) && !TicketColors.IsValid(color))
        {
            context.Result.Fail(new ValidationResult(S["The color {0} isn't a hex color (such as #1f6feb) or a color name (such as teal).", color], [nameof(TicketStatus.Color)]));
        }

        // Saved views are per owner, so two people may each have one called "My open tickets".
        if (context.Model is TicketView view)
        {
            // A user id differs from site to site, so a view imported with an owner who isn't a user here is refused
            // rather than kept as a view nobody can see.
            if (_unknownOwners.TryGetValue(view, out var unknownName))
            {
                context.Result.Fail(new ValidationResult(S["The view {0} belongs to the user {1}, who isn't a user of this site. Add the user first, or remove its owner to bring it in as a shared view.", name, unknownName], [nameof(TicketView.OwnerId)]));
            }
            else if (!string.IsNullOrEmpty(view.OwnerId)
                && _services.GetService<UserManager<IUser>>() is { } users
                && await users.FindByIdAsync(view.OwnerId) is null)
            {
                context.Result.Fail(new ValidationResult(S["The view {0} belongs to the user {1}, who isn't a user of this site. Add the user first, or remove its owner to bring it in as a shared view.", name, view.OwnerId], [nameof(TicketView.OwnerId)]));
            }

            return;
        }

        foreach (var other in await _store.GetAllAsync(cancellationToken))
        {
            if (!string.Equals(other.ItemId, context.Model.ItemId, StringComparison.Ordinal) && string.Equals(other.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                context.Result.Fail(new ValidationResult(S["Another one is already called {0}.", name], [nameof(ITicketSettingsRecord.Name)]));

                return;
            }
        }
    }
}
