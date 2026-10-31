using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.SharedSkills;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tests.Skills;
using Microsoft.Extensions.DependencyInjection;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Contact Center on the shared skill catalog: its skills live in the Skills catalog with the same ids, a copy stays in
/// its own store, and an agent's routing proficiencies and the user's shared skills stay in step both ways. Contact
/// Center names skills by their name, the catalog by id.
/// </summary>
public sealed class SharedSkillsTests
{
    private const string AgentUserId = "agent-user";

    [Fact]
    public async Task ContactCenterReads_ComeFromTheSharedCatalog_AndSkillsMissingThereAreCopiedWithTheirIds()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var existing = await harness.RunAsync(async services =>
        {
            var skill = new ContactCenterSkill { Name = "Spanish", Enabled = true };

            await services.GetRequiredService<ContactCenterSkillStore>().CreateAsync(skill);

            return skill;
        });

        // Act
        var enabled = await harness.RunAsync(services => services.GetRequiredService<IContactCenterSkillManager>().GetEnabledAsync());
        var shared = await harness.RunAsync(services => services.GetRequiredService<ISkillManager>().FindByIdAsync(existing.ItemId).AsTask(), commit: false);

        // Assert
        Assert.Equal([existing.ItemId], enabled.Select(skill => skill.ItemId));
        Assert.NotNull(shared);
        Assert.Equal("Spanish", shared.Name);
    }

    [Fact]
    public async Task ASkillCreatedInContactCenter_IsInTheSharedCatalog_AndInTheContactCenterStore()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var created = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IContactCenterSkillManager>();
            var skill = await manager.NewAsync();

            skill.Name = "French";
            await manager.CreateAsync(skill);

            return skill;
        });
        var shared = await harness.RunAsync(services => services.GetRequiredService<ISkillManager>().FindByNameAsync("French"), commit: false);
        var copy = await harness.RunAsync(services => services.GetRequiredService<ContactCenterSkillStore>().FindByIdAsync(created.ItemId).AsTask(), commit: false);

        // Assert
        Assert.Equal(created.ItemId, shared?.ItemId);
        Assert.Equal("French", copy?.Name);
    }

    [Fact]
    public async Task ASkillAddedToTheSkillsCatalog_IsSeenByContactCenter_AndCopiedToItsStore()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var skill = await harness.AddSkillAsync("Billing");
        var seen = await harness.RunAsync(services => services.GetRequiredService<IContactCenterSkillManager>().FindByNameAsync("Billing"), commit: false);
        var copy = await harness.RunAsync(services => services.GetRequiredService<ContactCenterSkillStore>().FindByIdAsync(skill.ItemId).AsTask(), commit: false);

        // Assert
        Assert.Equal(skill.ItemId, seen?.ItemId);
        Assert.Equal("Billing", copy?.Name);
    }

    [Fact]
    public async Task SavingAnAgentsProficiencies_GivesTheUserTheSharedSkills()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var spanish = await harness.AddSkillAsync("Spanish");

        // Act
        await SaveAgentAsync(harness, new AgentSkill { SkillId = "spanish", Proficiency = 4 }, new AgentSkill { SkillId = "Not in the catalog", Proficiency = 2 });
        var assignments = await GetAssignmentsAsync(harness);

        // Assert (the name matches whatever its case; a tag with no catalog skill stays only in Contact Center)
        var assignment = Assert.Single(assignments);
        Assert.Equal(spanish.ItemId, assignment.SkillId);
        Assert.Equal(4, assignment.Proficiency);
    }

    [Fact]
    public async Task GivingTheUserASkillInTheSkillsCatalog_UpdatesTheAgentsProficiencies_AndKeepsTheirOtherTags()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await harness.AddSkillAsync("Spanish");
        var billing = await harness.AddSkillAsync("Billing");

        await SaveAgentAsync(harness, new AgentSkill { SkillId = "Spanish", Proficiency = 4 }, new AgentSkill { SkillId = "Legacy tag", Proficiency = 2 });

        // Act
        await SkillsTests.AssignAsync(harness, SkillSubject.User(AgentUserId), billing.ItemId, 5);
        var profile = await GetAgentAsync(harness);

        // Assert
        Assert.Equal(
            new[] { ("Billing", 5), ("Legacy tag", 2), ("Spanish", 4) },
            profile.SkillProficiencies.Select(skill => (skill.SkillId, skill.Proficiency)).OrderBy(skill => skill.SkillId, StringComparer.Ordinal));
    }

    [Fact]
    public async Task TakingASkillAwayInTheSkillsCatalog_RemovesItFromTheAgent()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var spanish = await harness.AddSkillAsync("Spanish");

        await SaveAgentAsync(harness, new AgentSkill { SkillId = "Spanish", Proficiency = 4 });

        // Act
        await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().RemoveAsync(SkillSubject.User(AgentUserId), spanish.ItemId, "tester"));
        var profile = await GetAgentAsync(harness);

        // Assert
        Assert.Empty(profile.SkillProficiencies);
    }

    [Fact]
    public async Task SavingAnAgentWithoutSkillChanges_LeavesTheSharedAssignmentsAlone()
    {
        // Arrange
        var handler = new SkillsTests.RecordingAssignmentHandler();
        await using var harness = await CreateAsync(services => services.AddSingleton<ISkillAssignmentHandler>(handler));
        await harness.AddSkillAsync("Spanish");

        await SaveAgentAsync(harness, new AgentSkill { SkillId = "Spanish", Proficiency = 4 });
        handler.Contexts.Clear();

        // Act (a presence change saves the profile again with the same skills)
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IAgentProfileManager>();
            var profile = await manager.FindByUserIdAsync(AgentUserId);

            profile.PresenceStatus = AgentPresenceStatus.Available;
            await manager.UpdateAsync(profile);
        });

        // Assert
        Assert.Empty(handler.Contexts);
    }

    [Fact]
    public async Task Backfill_PushesAgentsSavedBeforeTheCatalogWasShared_Once()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var spanish = await harness.AddSkillAsync("Spanish");

        // A profile written straight to the store, as one saved before Skills was turned on.
        await harness.RunAsync(services => services.GetRequiredService<IAgentProfileStore>().CreateAsync(new AgentProfile
        {
            Name = "agent",
            UserId = AgentUserId,
            SkillProficiencies = [new AgentSkill { SkillId = "Spanish", Proficiency = 3 }],
        }).AsTask());

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<SharedAgentSkillsBackfill>().RunAsync(100));
        var second = await harness.RunAsync(services => services.GetRequiredService<SharedAgentSkillsBackfill>().RunAsync(100));
        var assignments = await GetAssignmentsAsync(harness);

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(spanish.ItemId, Assert.Single(assignments).SkillId);
    }

    private static Task<SkillsTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        return SkillsTestHarness.CreateAsync(
            services =>
            {
                services.AddScoped<IAgentProfileStore, AgentProfileStore>();
                services.AddScoped<IAgentProfileManager, AgentProfileManager>();
                services.AddScoped<IContactCenterSkillStore, ContactCenterSkillStore>();
                services.AddScoped<IContactCenterSkillManager, ContactCenterSkillManager>();
                services.AddSingleton<IContactCenterConfigurationCache, PassThroughConfigurationCache>();
                ContactCenterSharedSkills.AddCatalog(services);
                ContactCenterSharedSkills.AddAgentSync(services);
                configure?.Invoke(services);
            },
            CreateContactCenterSchemaAsync,
            [new AgentProfileIndexProvider(), new ContactCenterSkillIndexProvider()],
            [ContactCenterStorage.CollectionName]);
    }

    private static async Task CreateContactCenterSchemaAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AgentProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("PresenceStatus", column => column.WithLength(50)),
            collection: ContactCenterStorage.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<ContactCenterSkillIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<bool>("Enabled"),
            collection: ContactCenterStorage.CollectionName);
    }

    private static Task SaveAgentAsync(SkillsTestHarness harness, params AgentSkill[] skills)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IAgentProfileManager>();
            var profile = await manager.FindByUserIdAsync(AgentUserId);

            if (profile is null)
            {
                profile = await manager.NewAsync();
                profile.Name = "agent";
                profile.UserId = AgentUserId;
                profile.SkillProficiencies = skills.ToList();
                await manager.CreateAsync(profile);

                return;
            }

            profile.SkillProficiencies = skills.ToList();
            await manager.UpdateAsync(profile);
        });
    }

    private static Task<AgentProfile> GetAgentAsync(SkillsTestHarness harness)
    {
        return harness.RunAsync(services => services.GetRequiredService<IAgentProfileManager>().FindByUserIdAsync(AgentUserId), commit: false);
    }

    private static Task<IReadOnlyList<SkillAssignment>> GetAssignmentsAsync(SkillsTestHarness harness)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(SkillSubject.User(AgentUserId)), commit: false);
    }

    private sealed class PassThroughConfigurationCache : IContactCenterConfigurationCache
    {
        public Task<IReadOnlyCollection<T>> GetEnabledAsync<T>(Func<CancellationToken, Task<IReadOnlyCollection<T>>> factory, CancellationToken cancellationToken = default)
            => factory(cancellationToken);

        public Task InvalidateEnabledAsync<T>() => Task.CompletedTask;
    }
}
