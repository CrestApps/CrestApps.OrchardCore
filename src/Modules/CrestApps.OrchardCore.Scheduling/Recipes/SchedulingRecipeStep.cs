using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Scheduling.Recipes;

/// <summary>
/// Imports the Scheduling step: appointment types, resources, appointments and the waitlist, each created or updated by its
/// id. Appointments keep the calendar event they point to, so import the Calendars step with its events first.
/// </summary>
internal sealed class SchedulingRecipeStep : NamedRecipeStepHandler
{
    // Enums are read by name or number.
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingResourceStore _resourceStore;
    private readonly AppointmentStore _appointmentStore;
    private readonly WaitlistStore _waitlistStore;

    public SchedulingRecipeStep(
        AppointmentTypeStore typeStore,
        SchedulingResourceStore resourceStore,
        AppointmentStore appointmentStore,
        WaitlistStore waitlistStore)
        : base(SchedulingConstants.RecipeStepName)
    {
        _typeStore = typeStore;
        _resourceStore = resourceStore;
        _appointmentStore = appointmentStore;
        _waitlistStore = waitlistStore;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        await ImportAsync(context, "Resources", _resourceStore, resource => !string.IsNullOrWhiteSpace(resource.Name), (incoming, existing) =>
        {
            // The concurrency counter belongs to the site, not to the file.
            incoming.BookingVersion = existing?.BookingVersion ?? 0;
        });

        await ImportAsync(context, "AppointmentTypes", _typeStore, type => !string.IsNullOrWhiteSpace(type.Name), (incoming, _) =>
        {
            incoming.Slug = string.IsNullOrWhiteSpace(incoming.Slug) ? null : incoming.Slug.Trim().ToLowerInvariant();
        });

        await ImportAsync(context, "Appointments", _appointmentStore, appointment => !string.IsNullOrEmpty(appointment.AppointmentTypeId), (incoming, existing) =>
        {
            // Manage links aren't exported; an imported appointment keeps the one it had here, or gets a new one.
            incoming.ManageToken = existing?.ManageToken ?? incoming.ManageToken ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        });

        await ImportAsync(context, "Waitlist", _waitlistStore, entry => !string.IsNullOrEmpty(entry.AppointmentTypeId), (_, _) => { });
    }

    private static async Task ImportAsync<T, TIndex>(RecipeExecutionContext context, string section, DocumentCatalog<T, TIndex> store, Func<T, bool> isValid, Action<T, T> prepare)
        where T : CatalogItem
        where TIndex : CatalogItemIndex
    {
        foreach (var node in (context.Step[section] as JsonArray ?? []).OfType<JsonObject>())
        {
            T incoming;

            try
            {
                incoming = node.Deserialize<T>(JsonOptions);
            }
            catch (JsonException ex)
            {
                context.Errors.Add($"An entry of {section} in the Scheduling step can't be read: {ex.Message}");

                continue;
            }

            if (incoming is null || !isValid(incoming))
            {
                context.Errors.Add($"An entry of {section} in the Scheduling step is missing required values.");

                continue;
            }

            var existing = string.IsNullOrEmpty(incoming.ItemId) ? null : await store.FindByIdAsync(incoming.ItemId);

            prepare(incoming, existing);

            if (existing is null)
            {
                await store.CreateAsync(incoming);
            }
            else
            {
                // The stored record takes the incoming values, so it stays the same document.
                CopyValues(incoming, existing);
                await store.UpdateAsync(existing);
            }
        }
    }

    private static void CopyValues<T>(T source, T target)
    {
        foreach (var property in typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(target, property.GetValue(source));
            }
        }
    }
}
