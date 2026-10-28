using CrestApps.OrchardCore.Bookings.Indexes;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Bookings.Migrations;

/// <summary>
/// Creates the indexes of dates, bookings and balances, the attachable Booking part, and the Traveler content type,
/// with no fields: travelers are asked for nothing until the store adds some.
/// </summary>
internal sealed class BookingMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public BookingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<BookingSlotIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("SlotKey", column => column.WithLength(16))
            .Column<DateTime>("StartsUtc")
            .Column<int>("Remaining")
            .Column<bool>("Closed"),
            collection: BookingsConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<BookingIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("CartId", column => column.Nullable().WithLength(26))
            .Column<string>("Title", column => column.Nullable().WithLength(255))
            .Column<string>("CustomerName", column => column.Nullable().WithLength(255))
            .Column<DateTime>("StartsUtc")
            .Column<int>("Status")
            .Column<DateTime>("HoldExpiresUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: BookingsConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<BookingBalanceIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("DueUtc")
            .Column<DateTime>("NextAttemptUtc", column => column.Nullable()),
            collection: BookingsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<BookingSlotIndex>(table =>
        {
            table.CreateIndex("IDX_BookingSlotIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_BookingSlotIndex_Slot", "ContentItemId", "SlotKey");
            table.CreateIndex("IDX_BookingSlotIndex_Starts", "StartsUtc");
        }, collection: BookingsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<BookingIndex>(table =>
        {
            table.CreateIndex("IDX_BookingIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_BookingIndex_Order", "OrderId");
            table.CreateIndex("IDX_BookingIndex_Starts", "StartsUtc", "Status");
            table.CreateIndex("IDX_BookingIndex_Hold", "Status", "HoldExpiresUtc");
        }, collection: BookingsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<BookingBalanceIndex>(table =>
        {
            table.CreateIndex("IDX_BookingBalanceIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_BookingBalanceIndex_Due", "Status", "DueUtc");
        }, collection: BookingsConstants.CollectionName);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(BookingPart), part => part
            .Attachable()
            .WithDisplayName("Booking")
            .WithDescription("Makes a product a booking: a departure, a stay or a time slot, with its capacity per date, occupancy prices, deposit and cancellation policy.")
        );

        // The type's own part holds the fields, as the content type editor adds them.
        await _contentDefinitionManager.AlterPartDefinitionAsync(BookingsConstants.TravelerContentType, part => part
            .WithDescription("The fields asked for each traveler of a booking.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(BookingsConstants.TravelerContentType, type => type
            .WithDisplayName("Traveler")
            .WithDescription("What a booking asks for each traveler or attendee, such as a name, a birth date or a passport number. Each field added here is asked for once per person at checkout, for products whose Booking part asks for travelers.")
            .Creatable(false)
            .Listable(false)
            .Draftable(false)
            .Versionable(false)
            .Securable(false)
            .WithPart(BookingsConstants.TravelerContentType, part => part
                .WithPosition("0")
            )
        );

        await CreateReportIndexesAsync();

        return 2;
    }

    /// <summary>
    /// Adds the indexes reports read bookings, dates and balances through a page at a time, newest first by when a
    /// booking was made, a date starts and a balance is due, so each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexesAsync();

        return 2;
    }

    private async Task CreateReportIndexesAsync()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<BookingIndex>(
            SchemaBuilder,
            "IDX_BookingIndex_Report",
            BookingsConstants.CollectionName,
            "CreatedUtc", "Id");

        await IndexTableIndexCreation.CreateIfMissingAsync<BookingSlotIndex>(
            SchemaBuilder,
            "IDX_BookingSlotIndex_Report",
            BookingsConstants.CollectionName,
            "StartsUtc", "Id");

        await IndexTableIndexCreation.CreateIfMissingAsync<BookingBalanceIndex>(
            SchemaBuilder,
            "IDX_BookingBalanceIndex_Report",
            BookingsConstants.CollectionName,
            "DueUtc", "Id");
    }
}
