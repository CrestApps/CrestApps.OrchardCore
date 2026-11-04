using System.Data.Common;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Teams.Core.Indexes;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Workforce on the shared teams: its settings stored on the team, the check that keeps a team with workers, and the
/// migration that moves the teams Workforce kept in its own collection to the Teams collection.
/// </summary>
public sealed class WorkforceTeamsTests
{
    [Fact]
    public async Task WorkforceSettings_AreStoredOnTheTeam_AndReadBackInALaterRequest()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Boston", TeamKind.Site);

        // Act
        await PlanningKit.UpdateTeamAsync(harness, team, saved => saved.AlterWorkforceSettings(settings =>
        {
            settings.Location = new WorkforceLocation { Address = "1 Main St", Latitude = 42.36, Longitude = -71.06 };
            settings.OpenShiftClaimPolicy = OpenShiftClaimPolicy.FirstCome;
            settings.TimeClockPolicyId = "policy";
        }));

        var stored = await harness.RunAsync(services => services.GetRequiredService<ITeamManager>().FindByIdAsync(team.ItemId).AsTask(), commit: false);
        var settings = stored.GetWorkforceSettings();

        // Assert
        Assert.Equal("1 Main St", settings.Location.Address);
        Assert.Equal(42.36, settings.Location.Latitude);
        Assert.Equal(OpenShiftClaimPolicy.FirstCome, settings.OpenShiftClaimPolicy);
        Assert.Equal("policy", settings.TimeClockPolicyId);
        Assert.False(string.IsNullOrEmpty(settings.CalendarId));
    }

    [Fact]
    public async Task ATeam_WithWorkersOrSubTeams_CannotBeDeleted_AndAnEmptyOneCan()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var site = await WorkforceTests.AddTeamAsync(harness, "Boston", TeamKind.Site);
        var staffed = await WorkforceTests.AddTeamAsync(harness, "Billing", TeamKind.Team, site.ItemId);
        var empty = await WorkforceTests.AddTeamAsync(harness, "Archive", TeamKind.Team);

        await WorkforceTests.AddWorkerAsync(harness, "Ana Lopez", worker => worker.TeamMemberships.Add(new TeamMembership { TeamId = staffed.ItemId }));

        // Act
        var (parentBlockers, staffedBlockers, emptyBlockers) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITeamManager>();

            return (
                await manager.GetDeletionBlockersAsync(site),
                await manager.GetDeletionBlockersAsync(staffed),
                await manager.GetDeletionBlockersAsync(empty));
        }, commit: false);

        // Assert
        Assert.Single(parentBlockers);
        Assert.Single(staffedBlockers);
        Assert.Empty(emptyBlockers);
    }

    [Fact]
    public async Task Move_CopiesTheOldTeams_WithTheirIdsAndWorkforceFields_AndRemovesTheOldCopies()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(schema: CreateLegacyIndexTableAsync);
        var region = new LegacyWorkforceTeam
        {
            ItemId = "01kregion0000000000000000a",
            Name = "East",
            Kind = TeamKind.Region,
            TimeZoneId = "America/New_York",
            CalendarId = "calendar-east",
            Enabled = true,
        };
        var site = new LegacyWorkforceTeam
        {
            ItemId = "01ksite000000000000000000b",
            Name = "Boston",
            Kind = TeamKind.Site,
            ParentId = region.ItemId,
            LinkedRoleName = "Boston staff",
            Location = new WorkforceLocation { Address = "1 Main St", Latitude = 42.36, Longitude = -71.06 },
            CalendarId = "calendar-boston",
            LeadWorkerId = "lead",
            OpenShiftClaimPolicy = OpenShiftClaimPolicy.BySeniority,
            ShiftTradesNeedApproval = false,
            TimeOffShiftHandling = TimeOffShiftHandling.Remove,
            TimeClockPolicyId = "policy",
            Enabled = false,
        };

        await InsertLegacyDocumentsAsync(harness.Store, region, site);

        // Act
        var moved = await MoveAsync(harness.Store);
        var teams = await harness.RunAsync(async services => (await services.GetRequiredService<ITeamManager>().ListTreeAsync()).Select(item => item.Team).ToList(), commit: false);
        var (legacyDocuments, legacyTableExists) = await ReadLegacyStateAsync(harness.Store);

        // Assert
        Assert.Equal(2, moved);
        Assert.Equal([region.ItemId, site.ItemId], teams.Select(team => team.ItemId));

        var movedSite = teams[1];
        var settings = movedSite.GetWorkforceSettings();

        Assert.Equal("Boston", movedSite.Name);
        Assert.Equal(TeamKind.Site, movedSite.Kind);
        Assert.Equal(region.ItemId, movedSite.ParentId);
        Assert.Equal("Boston staff", movedSite.LinkedRoleName);
        Assert.False(movedSite.Enabled);
        Assert.Equal("1 Main St", settings.Location.Address);
        Assert.Equal(-71.06, settings.Location.Longitude);
        Assert.Equal("calendar-boston", settings.CalendarId);
        Assert.Equal("lead", settings.LeadWorkerId);
        Assert.Equal(OpenShiftClaimPolicy.BySeniority, settings.OpenShiftClaimPolicy);
        Assert.False(settings.ShiftTradesNeedApproval);
        Assert.Equal(TimeOffShiftHandling.Remove, settings.TimeOffShiftHandling);
        Assert.Equal("policy", settings.TimeClockPolicyId);
        Assert.Equal("calendar-east", teams[0].GetWorkforceSettings().CalendarId);
        Assert.Equal(0, legacyDocuments);
        Assert.False(legacyTableExists);
    }

    [Fact]
    public async Task Move_LeavesATeamAlreadyInTheTeamsCollectionAsItIs()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(schema: CreateLegacyIndexTableAsync);
        var existing = await WorkforceTests.AddTeamAsync(harness, "Boston", TeamKind.Site);

        await InsertLegacyDocumentsAsync(harness.Store, new LegacyWorkforceTeam
        {
            ItemId = existing.ItemId,
            Name = "Old Boston",
            Kind = TeamKind.Team,
            Enabled = true,
        });

        // Act
        var moved = await MoveAsync(harness.Store);
        var teams = await harness.RunAsync(async services => (await services.GetRequiredService<ITeamManager>().GetAllAsync()).ToList(), commit: false);

        // Assert
        Assert.Equal(0, moved);
        Assert.Equal(["Boston"], teams.Select(team => team.Name));
    }

    private static async Task CreateLegacyIndexTableAsync(ISchemaBuilder schemaBuilder)
    {
        // The table Workforce's first migration made in its own collection for the old team documents, with its
        // index names, which are unique across the database and so must not be reused by the Teams collection.
        await schemaBuilder.CreateMapIndexTableAsync<TeamIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<TeamKind>("Kind")
            .Column<string>("ParentId", column => column.Nullable().WithLength(26))
            .Column<bool>("Enabled"),
            collection: WorkforceConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<TeamIndex>(table =>
        {
            table.CreateIndex("IDX_TeamIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TeamIndex_Parent", "ParentId");
        }, collection: WorkforceConstants.CollectionName);
    }

    private static async Task InsertLegacyDocumentsAsync(IStore store, params LegacyWorkforceTeam[] teams)
    {
        await using var connection = store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // YesSql gives document ids itself, so the old documents take ids well above any the harness has used.
        var id = 100_000;

        foreach (var team in teams)
        {
            await using var command = connection.CreateCommand();

            command.CommandText = "INSERT INTO Workforce_Document (Id, Type, Content, Version) VALUES (@id, @type, @content, 0)";
            AddParameter(command, "@id", id++);
            AddParameter(command, "@type", WorkforceTeamsMove.LegacyTypeName);
            AddParameter(command, "@content", store.Configuration.ContentSerializer.Serialize(team));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<int> MoveAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction, throwOnError: true);
        var moved = await WorkforceTeamsMove.MoveAsync(schemaBuilder, store, session);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return moved;
    }

    private static async Task<(long Documents, bool TableExists)> ReadLegacyStateAsync(IStore store)
    {
        await using var connection = store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var documents = connection.CreateCommand();

        documents.CommandText = "SELECT COUNT(*) FROM Workforce_Document WHERE Type = @type";
        AddParameter(documents, "@type", WorkforceTeamsMove.LegacyTypeName);

        await using var table = connection.CreateCommand();

        table.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Workforce_TeamIndex'";

        return (
            Convert.ToInt64(await documents.ExecuteScalarAsync(TestContext.Current.CancellationToken)),
            Convert.ToInt64(await table.ExecuteScalarAsync(TestContext.Current.CancellationToken)) > 0);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // The shape Workforce stored its teams in before they had their own module.
    private sealed class LegacyWorkforceTeam : CatalogItem
    {
        public string Name { get; set; }

        public TeamKind Kind { get; set; } = TeamKind.Team;

        public string ParentId { get; set; }

        public string Description { get; set; }

        public string LinkedRoleName { get; set; }

        public string TimeZoneId { get; set; }

        public WorkforceLocation Location { get; set; }

        public string CalendarId { get; set; }

        public string LeadWorkerId { get; set; }

        public OpenShiftClaimPolicy? OpenShiftClaimPolicy { get; set; }

        public bool? ShiftTradesNeedApproval { get; set; }

        public TimeOffShiftHandling? TimeOffShiftHandling { get; set; }

        public string TimeClockPolicyId { get; set; }

        public bool Enabled { get; set; } = true;

        public DateTime CreatedUtc { get; set; }

        public DateTime? ModifiedUtc { get; set; }
    }
}
