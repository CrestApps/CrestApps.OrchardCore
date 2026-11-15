using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// Finds sessions by start and by source.
/// </summary>
public sealed class SchedulingSessionIndex : CatalogItemIndex
{
    public string AppointmentTypeId { get; set; }

    public DateTime StartUtc { get; set; }

    public SchedulingSessionStatus Status { get; set; }

    public string Source { get; set; }

    public string SourceId { get; set; }
}

/// <summary>
/// Finds the registrations of a session.
/// </summary>
public sealed class SessionRegistrationIndex : MapIndex
{
    public string AppointmentId { get; set; }

    public string SessionId { get; set; }

    public string UserId { get; set; }

    public AppointmentStatus Status { get; set; }

    public DateTime? HoldExpiresUtc { get; set; }
}

/// <summary>
/// Finds the waitlist of a session.
/// </summary>
public sealed class SessionWaitlistIndex : MapIndex
{
    public string EntryId { get; set; }

    public string SessionId { get; set; }

    public string UserId { get; set; }

    public WaitlistStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class SchedulingSessionIndexProvider : IndexProvider<SchedulingSession>
{
    public SchedulingSessionIndexProvider()
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    public override void Describe(DescribeContext<SchedulingSession> context)
        => context.For<SchedulingSessionIndex>()
            .Map(session => new SchedulingSessionIndex
            {
                ItemId = session.ItemId,
                AppointmentTypeId = session.AppointmentTypeId,
                StartUtc = session.StartUtc,
                Status = session.Status,
                Source = session.Source,
                SourceId = session.SourceId,
            });
}

public sealed class SessionRegistrationIndexProvider : IndexProvider<Appointment>
{
    public SessionRegistrationIndexProvider()
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Appointment> context)
        => context.For<SessionRegistrationIndex>()
            .When(appointment => !string.IsNullOrEmpty(appointment.SessionId))
            .Map(appointment => new SessionRegistrationIndex
            {
                AppointmentId = appointment.ItemId,
                SessionId = appointment.SessionId,
                UserId = appointment.UserId,
                Status = appointment.Status,
                HoldExpiresUtc = appointment.HoldExpiresUtc,
            });
}

public sealed class SessionWaitlistIndexProvider : IndexProvider<WaitlistEntry>
{
    public SessionWaitlistIndexProvider()
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    public override void Describe(DescribeContext<WaitlistEntry> context)
        => context.For<SessionWaitlistIndex>()
            .When(entry => !string.IsNullOrEmpty(entry.SessionId))
            .Map(entry => new SessionWaitlistIndex
            {
                EntryId = entry.ItemId,
                SessionId = entry.SessionId,
                UserId = entry.UserId,
                Status = entry.Status,
                CreatedUtc = entry.CreatedUtc,
            });
}

/// <summary>
/// The tables of sessions, kept apart from the first tables of Scheduling so an install adds them in a step of its own.
/// </summary>
public static class SchedulingSessionIndexSchema
{
    public static IIndexProvider[] Providers() => [new SchedulingSessionIndexProvider(), new SessionRegistrationIndexProvider(), new SessionWaitlistIndexProvider()];

    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = SchedulingConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<SchedulingSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AppointmentTypeId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("StartUtc")
            .Column<int>("Status")
            .Column<string>("Source", column => column.Nullable().WithLength(100))
            .Column<string>("SourceId", column => column.Nullable().WithLength(100)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SchedulingSessionIndex>(table =>
        {
            table.CreateIndex("IDX_SchedulingSessionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SchedulingSessionIndex_Start", "StartUtc", "Status");
            table.CreateIndex("IDX_SchedulingSessionIndex_Source", "Source", "SourceId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<SessionRegistrationIndex>(table => table
            .Column<string>("AppointmentId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("HoldExpiresUtc", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SessionRegistrationIndex>(table =>
        {
            table.CreateIndex("IDX_SessionRegistrationIndex_Session", "SessionId", "Status");
            table.CreateIndex("IDX_SessionRegistrationIndex_Hold", "Status", "HoldExpiresUtc");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<SessionWaitlistIndex>(table => table
            .Column<string>("EntryId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SessionWaitlistIndex>(table => table.CreateIndex("IDX_SessionWaitlistIndex_Session", "SessionId", "Status", "CreatedUtc"), collection: collection);
    }
}
