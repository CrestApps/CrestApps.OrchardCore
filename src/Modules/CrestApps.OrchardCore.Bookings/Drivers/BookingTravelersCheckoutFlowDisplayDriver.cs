using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Bookings.ViewModels;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Customers.Core;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Bookings.Drivers;

/// <summary>
/// Each traveler's details on the Contact step, once per person of each booking whose product asks for them, from the
/// Traveler content type's fields. What the buyer enters is kept on the booking as soon as the step is valid.
/// </summary>
internal sealed class BookingTravelersCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    // Enough for a group, not for a form that never ends.
    private const int MaxTravelers = 20;

    private readonly IBookingStore _bookings;
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;

    public BookingTravelersCheckoutFlowDisplayDriver(
        IBookingStore bookings,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager)
    {
        _bookings = bookings;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
    }

    protected override string StepKey
        => CustomersConstants.ContactStepKey;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
        => await BuildAsync(flow, context.Updater, update: false);

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
        => await BuildAsync(flow, context.Updater, update: true);

    private async Task<IDisplayResult> BuildAsync(CheckoutFlow flow, IUpdateModel updater, bool update)
    {
        if (flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            !await HasFieldsAsync())
        {
            return null;
        }

        var bookings = new List<(Booking Booking, int People)>();

        foreach (var booking in await _bookings.ListByOrderAsync(session.ReferenceId))
        {
            if (booking.Status is BookingStatus.Held or BookingStatus.Requested &&
                (await _contentManager.GetAsync(booking.ContentItemId, VersionOptions.Published))?.TryGet<BookingPart>(out var part) == true &&
                part.AskForTravelers)
            {
                bookings.Add((booking, Math.Min(MaxTravelers, Math.Max(1, booking.People))));
            }
        }

        if (bookings.Count == 0)
        {
            return null;
        }

        var sections = new List<(string Title, IList<IShape> Editors)>();

        foreach (var (booking, people) in bookings)
        {
            var editors = new List<IShape>();
            var travelers = new List<JsonObject>();

            for (var i = 0; i < people; i++)
            {
                var item = await _contentManager.NewAsync(BookingsConstants.TravelerContentType);

                if (booking.Travelers is { } kept && i < kept.Count && kept[i] is not null)
                {
                    item.Merge(kept[i].DeepClone(), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
                }

                var prefix = $"Traveler.{booking.ItemId}.{i}";
                var editor = update
                    ? await _contentItemDisplayManager.UpdateEditorAsync(item, updater, isNew: true, htmlFieldPrefix: prefix)
                    : await _contentItemDisplayManager.BuildEditorAsync(item, updater, isNew: true, htmlFieldPrefix: prefix);

                editors.Add(editor);
                travelers.Add(GetContent(item));
            }

            // A field left wrong keeps the buyer on the step, with what they typed, until it is put right.
            if (update && updater.ModelState.IsValid)
            {
                booking.Travelers = travelers;

                await _bookings.UpdateAsync(booking);
            }

            sections.Add((booking.Title, editors));
        }

        return Initialize<BookingTravelersViewModel>("CheckoutBookingTravelers", model => model.Bookings = sections)
            .Location("Content:6");
    }

    private async Task<bool> HasFieldsAsync()
    {
        var type = await _contentDefinitionManager.GetTypeDefinitionAsync(BookingsConstants.TravelerContentType);

        return type is not null && type.Parts.Any(part =>
            part.PartDefinition.Fields.Any() ||
            !string.Equals(part.PartDefinition.Name, BookingsConstants.TravelerContentType, StringComparison.Ordinal));
    }

    private static JsonObject GetContent(ContentItem item)
    {
        var content = new JsonObject();

        foreach (var (name, value) in (JsonObject)item.Content)
        {
            if (value is JsonObject)
            {
                content[name] = value.DeepClone();
            }
        }

        return content;
    }
}
