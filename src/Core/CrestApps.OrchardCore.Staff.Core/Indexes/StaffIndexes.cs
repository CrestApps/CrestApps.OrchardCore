using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Staff.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Staff.Core.Indexes;

/// <summary>
/// The YesSql index of staff members.
/// </summary>
public sealed class StaffMemberIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the employee number, normalized.
    /// </summary>
    public string NormalizedEmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the linked user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the work e-mail.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string JobTitle { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public StaffStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the employment type.
    /// </summary>
    public EmploymentType EmploymentType { get; set; }

    /// <summary>
    /// Gets or sets the main team.
    /// </summary>
    public string PrimaryTeamId { get; set; }

    /// <summary>
    /// Gets or sets the manager.
    /// </summary>
    public string ManagerId { get; set; }
}

/// <summary>
/// One row per team membership of a staff member, with its dates as keys from <see cref="StaffKeys.ToKey(DateOnly)"/>.
/// </summary>
public sealed class StaffTeamMembershipIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the staff member.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the role.
    /// </summary>
    public TeamMemberRole Role { get; set; }

    /// <summary>
    /// Gets or sets the first day, as a key.
    /// </summary>
    public int? EffectiveFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day, as a key.
    /// </summary>
    public int? EffectiveTo { get; set; }
}

/// <summary>
/// One row per capability of a staff member.
/// </summary>
public sealed class StaffCapabilityIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the staff member.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets the capability.
    /// </summary>
    public string Capability { get; set; }
}

/// <summary>
/// The YesSql index of staff history entries.
/// </summary>
public sealed class StaffHistoryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the staff member.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets what changed.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the change.
    /// </summary>
    public DateTime ChangedUtc { get; set; }
}

/// <summary>
/// Maps staff members to their index rows.
/// </summary>
public sealed class StaffMemberIndexProvider : IndexProvider<StaffMember>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaffMemberIndexProvider"/> class.
    /// </summary>
    public StaffMemberIndexProvider()
    {
        CollectionName = StaffConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<StaffMember> context)
    {
        context.For<StaffMemberIndex>()
            .Map(staff => new StaffMemberIndex
            {
                ItemId = staff.ItemId,
                DisplayName = staff.DisplayName,
                NormalizedEmployeeNumber = StaffKeys.Normalize(staff.EmployeeNumber),
                UserId = string.IsNullOrEmpty(staff.UserId) ? null : staff.UserId,
                Email = staff.Email,
                JobTitle = staff.JobTitle,
                Status = staff.Status,
                EmploymentType = staff.EmploymentType,
                PrimaryTeamId = staff.PrimaryTeamId,
                ManagerId = string.IsNullOrEmpty(staff.ManagerId) ? null : staff.ManagerId,
            });

        context.For<StaffTeamMembershipIndex>()
            .Map(staff => (staff.TeamMemberships ?? [])
                .Where(membership => !string.IsNullOrEmpty(membership?.TeamId))
                .Select(membership => new StaffTeamMembershipIndex
                {
                    StaffMemberId = staff.ItemId,
                    TeamId = membership.TeamId,
                    Role = membership.Role,
                    EffectiveFrom = StaffKeys.ToKey(membership.EffectiveFrom),
                    EffectiveTo = StaffKeys.ToKey(membership.EffectiveTo),
                }));

        context.For<StaffCapabilityIndex>()
            .Map(staff => (staff.Capabilities ?? [])
                .Where(capability => !string.IsNullOrWhiteSpace(capability))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(capability => new StaffCapabilityIndex
                {
                    StaffMemberId = staff.ItemId,
                    Capability = capability,
                }));
    }
}

/// <summary>
/// Maps staff history entries to their index rows.
/// </summary>
public sealed class StaffHistoryIndexProvider : IndexProvider<StaffHistoryEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaffHistoryIndexProvider"/> class.
    /// </summary>
    public StaffHistoryIndexProvider()
    {
        CollectionName = StaffConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<StaffHistoryEntry> context)
    {
        context.For<StaffHistoryIndex>()
            .Map(entry => new StaffHistoryIndex
            {
                ItemId = entry.ItemId,
                StaffMemberId = entry.StaffMemberId,
                Field = entry.Field,
                ChangedUtc = entry.ChangedUtc,
            });
    }
}

/// <summary>
/// The index providers of the Staff collection, for the module and the tests.
/// </summary>
public static class StaffIndexProviders
{
    /// <summary>
    /// Creates one of each index provider.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All()
        => [new StaffMemberIndexProvider(), new StaffHistoryIndexProvider()];
}

/// <summary>
/// Creates the Staff index tables, for the module's migration and the tests. Index names are unique across the
/// database (SQLite index names are global).
/// </summary>
public static class StaffIndexSchema
{
    /// <summary>
    /// Creates the index tables in the <see cref="StaffConstants.CollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = StaffConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<StaffMemberIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayName", column => column.Nullable().WithLength(255))
            .Column<string>("NormalizedEmployeeNumber", column => column.Nullable().WithLength(64))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<string>("Email", column => column.Nullable().WithLength(255))
            .Column<string>("JobTitle", column => column.Nullable().WithLength(255))
            .Column<StaffStatus>("Status")
            .Column<EmploymentType>("EmploymentType")
            .Column<string>("PrimaryTeamId", column => column.Nullable().WithLength(26))
            .Column<string>("ManagerId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<StaffMemberIndex>(table =>
        {
            table.CreateIndex("IDX_Staff_StaffMemberIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_Staff_StaffMemberIndex_UserId", "UserId");
            table.CreateIndex("IDX_Staff_StaffMemberIndex_EmployeeNumber", "NormalizedEmployeeNumber");
            table.CreateIndex("IDX_Staff_StaffMemberIndex_Status", "Status", "DisplayName");
            table.CreateIndex("IDX_Staff_StaffMemberIndex_Manager", "ManagerId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<StaffTeamMembershipIndex>(table => table
            .Column<string>("StaffMemberId", column => column.WithLength(26))
            .Column<string>("TeamId", column => column.WithLength(26))
            .Column<TeamMemberRole>("Role")
            .Column<int>("EffectiveFrom", column => column.Nullable())
            .Column<int>("EffectiveTo", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<StaffTeamMembershipIndex>(table =>
        {
            table.CreateIndex("IDX_Staff_StaffTeamMembershipIndex_Team", "TeamId", "Role");
            table.CreateIndex("IDX_Staff_StaffTeamMembershipIndex_Member", "StaffMemberId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<StaffCapabilityIndex>(table => table
            .Column<string>("StaffMemberId", column => column.WithLength(26))
            .Column<string>("Capability", column => column.WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<StaffCapabilityIndex>(table =>
        {
            table.CreateIndex("IDX_Staff_StaffCapabilityIndex_Capability", "Capability", "StaffMemberId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<StaffHistoryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("StaffMemberId", column => column.WithLength(26))
            .Column<string>("Field", column => column.Nullable().WithLength(64))
            .Column<DateTime>("ChangedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<StaffHistoryIndex>(table =>
        {
            table.CreateIndex("IDX_Staff_StaffHistoryIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_Staff_StaffHistoryIndex_Member", "StaffMemberId", "ChangedUtc");
        }, collection: collection);
    }
}
