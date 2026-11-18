using System.Security.Claims;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.AI;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Skills.AI;

/// <summary>
/// The skill and certificate tools check the permission of the matching Skills page first, write through the Skills
/// services so their rules decide, name people through the subject providers, and ask a subject's provider (such as the
/// staff scope) before changing that person's skills or certificates.
/// </summary>
public sealed class SkillToolTests
{
    private const string LuisId = "u-luis";

    public static TheoryData<string, string> Tools => new()
    {
        { ListSkillsTool.TheName, SkillsPermissions.ViewSkills.Name },
        { SaveSkillTool.TheName, SkillsPermissions.ManageSkills.Name },
        { DeleteSkillTool.TheName, SkillsPermissions.ManageSkills.Name },
        { ListCertificationTypesTool.TheName, SkillsPermissions.ViewSkills.Name },
        { SaveCertificationTypeTool.TheName, SkillsPermissions.ManageSkills.Name },
        { DeleteCertificationTypeTool.TheName, SkillsPermissions.ManageSkills.Name },
        { GetSubjectSkillsTool.TheName, SkillsPermissions.ViewSkills.Name },
        { SaveSkillAssignmentTool.TheName, SkillsPermissions.AssignSkills.Name },
        { RemoveSkillAssignmentTool.TheName, SkillsPermissions.AssignSkills.Name },
        { FindSkillHoldersTool.TheName, SkillsPermissions.ViewSkills.Name },
        { ListCertificatesTool.TheName, SkillsPermissions.ViewSkills.Name },
        { SaveCertificateTool.TheName, SkillsPermissions.AssignSkills.Name },
        { SetCertificateStatusTool.TheName, SkillsPermissions.AssignSkills.Name },
        { DeleteCertificateTool.TheName, SkillsPermissions.AssignSkills.Name },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Arrange
        Assert.NotNull(permission);
        var services = AgentToolTestContext.CreateServices(signedIn: false).BuildServiceProvider();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(Create(toolName), services, []);

        // Assert
        Assert.Equal(SkillsAIToolBase.NotAllowedMessage, result);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWithoutThePermissionOfItsPage(string toolName, string permission)
    {
        // Arrange
        var services = AgentToolTestContext.CreateServices(authorize: (asked, _) => asked.Name != permission).BuildServiceProvider();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(Create(toolName), services, new() { ["skillId"] = "any" });

        // Assert
        Assert.Equal(SkillsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
    }

    [Fact]
    public async Task Tool_RefusesAnArgumentItDoesNotDeclare()
    {
        // Arrange
        var services = AgentToolTestContext.CreateServices().BuildServiceProvider();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteSkillTool(), services, new() { ["skillId"] = "x", ["force"] = true });

        // Assert
        Assert.StartsWith("Nothing was done: 'force' is not an argument of deleteSkill.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveSkill_CreatesASkillThatRequiresAnEnabledCertification_AndListSkillsNamesIt()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var osha = await harness.AddCertificationAsync("OSHA Forklift");
        var retired = await harness.AddCertificationAsync("Retired", certification => certification.Enabled = false);

        // Act
        var created = await InvokeAsync(harness, new SaveSkillTool(), new() { ["name"] = "Forklift operation", ["category"] = "Equipment", ["requiredCertificationId"] = osha.ItemId });
        var refused = await InvokeAsync(harness, new SaveSkillTool(), new() { ["name"] = "Crane", ["requiredCertificationId"] = retired.ItemId });
        var duplicate = await InvokeAsync(harness, new SaveSkillTool(), new() { ["name"] = "forklift operation" });
        var list = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new ListSkillsTool(), new() { ["name"] = "Fork" }));

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(created).GetProperty("created").GetBoolean());
        Assert.StartsWith("Nothing was saved: there is no enabled certification type", refused, StringComparison.Ordinal);
        Assert.Equal("Nothing was saved: Another skill is already named 'forklift operation'.", duplicate);

        var skill = Assert.Single(list.GetProperty("items").EnumerateArray());

        Assert.Equal("OSHA Forklift", skill.GetProperty("requiredCertificationName").GetString());
        Assert.Equal("Equipment", skill.GetProperty("category").GetString());
        Assert.Equal(1, list.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task SaveCertificationType_SavesValidityAndReminders_AndChangesOnlyTheGivenFields()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var created = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SaveCertificationTypeTool(), new()
        {
            ["name"] = "OSHA Forklift",
            ["issuer"] = "OSHA",
            ["validityMonths"] = 36,
            ["reminderDays"] = new[] { 30, 60 },
        }));
        var id = created.GetProperty("certificationType").GetProperty("id").GetString();
        var updated = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SaveCertificationTypeTool(), new() { ["certificationId"] = id, ["requiresDocument"] = true }));
        var invalid = await InvokeAsync(harness, new SaveCertificationTypeTool(), new() { ["certificationId"] = id, ["reminderDays"] = new[] { 0 } });

        // Assert
        var type = updated.GetProperty("certificationType");

        Assert.Equal(36, type.GetProperty("validityMonths").GetInt32());
        Assert.Equal(new[] { 60, 30 }, type.GetProperty("reminderDays").EnumerateArray().Select(day => day.GetInt32()));
        Assert.True(type.GetProperty("requiresDocument").GetBoolean());
        Assert.Equal("OSHA", type.GetProperty("issuer").GetString());
        Assert.Equal("Nothing was saved: Reminders are days before expiry, so each must be 1 or more.", invalid);
    }

    [Fact]
    public async Task SkillAndCertificate_ASkillThatRequiresACertificationCountsOnlyOnceItsCertificateIsVerified()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var osha = await harness.AddCertificationAsync("OSHA Forklift", certification => certification.ValidityMonths = 36);
        var forklift = await harness.AddSkillAsync("Forklift operation", skill => skill.RequiredCertificationId = osha.ItemId);

        // Act
        var assigned = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SaveSkillAssignmentTool(), new()
        {
            ["subjectKind"] = "User",
            ["subjectId"] = "luis",
            ["skillId"] = forklift.ItemId,
            ["proficiency"] = 4,
        }));
        var recorded = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SaveCertificateTool(), new()
        {
            ["subjectKind"] = "User",
            ["subjectId"] = LuisId,
            ["certificationId"] = osha.ItemId,
            ["number"] = "FL-22871",
            ["issuedOn"] = "2026-03-01",
        }));
        var certificateId = recorded.GetProperty("certificate").GetProperty("id").GetString();
        var verified = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SetCertificateStatusTool(), new() { ["certificateId"] = certificateId, ["action"] = "verify" }));
        var person = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new GetSubjectSkillsTool(), new() { ["subjectKind"] = "User", ["subjectId"] = LuisId }));
        var holders = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new FindSkillHoldersTool(), new() { ["skillId"] = forklift.ItemId, ["minimumProficiency"] = 3 }));
        var tooHigh = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new FindSkillHoldersTool(), new() { ["skillId"] = forklift.ItemId, ["minimumProficiency"] = 5 }));

        // Assert
        Assert.False(assigned.GetProperty("countsToday").GetBoolean());
        Assert.Contains("OSHA Forklift", assigned.GetProperty("whyNotCounting").GetString(), StringComparison.Ordinal);
        Assert.Equal("Pending", recorded.GetProperty("certificate").GetProperty("status").GetString());
        Assert.Equal("2029-02-28", recorded.GetProperty("certificate").GetProperty("expiresOn").GetString());
        Assert.Equal("Valid", verified.GetProperty("certificate").GetProperty("status").GetString());
        Assert.Equal(AgentToolTestContext.UserName, verified.GetProperty("certificate").GetProperty("verifiedBy").GetString());
        Assert.True(Assert.Single(person.GetProperty("skills").EnumerateArray()).GetProperty("countsToday").GetBoolean());
        Assert.Equal("Luis Ortega", person.GetProperty("subject").GetProperty("name").GetString());
        Assert.Single(person.GetProperty("certificates").EnumerateArray());

        var holder = Assert.Single(holders.GetProperty("items").EnumerateArray());

        Assert.Equal("Luis Ortega", holder.GetProperty("name").GetString());
        Assert.Equal(4, holder.GetProperty("proficiency").GetInt32());
        Assert.Equal(0, tooHigh.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task SaveSkillAssignment_RefusesAnUnknownPerson_AndAnInvalidProficiency()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var skill = await harness.AddSkillAsync("Welding");

        // Act
        var unknown = await InvokeAsync(harness, new SaveSkillAssignmentTool(), new() { ["subjectKind"] = "User", ["subjectId"] = "ghost", ["skillId"] = skill.ItemId });
        var level = await InvokeAsync(harness, new SaveSkillAssignmentTool(), new() { ["subjectKind"] = "User", ["subjectId"] = LuisId, ["skillId"] = skill.ItemId, ["proficiency"] = 9 });
        var removedNothing = await InvokeAsync(harness, new RemoveSkillAssignmentTool(), new() { ["subjectKind"] = "User", ["subjectId"] = LuisId, ["skillId"] = skill.ItemId });

        // Assert
        Assert.Equal("Nothing was saved: There is no user 'ghost'. Give a user id, user name or email.", unknown);
        Assert.Equal("Nothing was saved: The proficiency of 'Welding' must be from 1 to 5.", level);
        Assert.StartsWith("Nothing was removed: Luis Ortega was not given the skill", removedNothing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaffSubjects_AreChangedOnlyWhenTheirProviderAllowsTheCaller()
    {
        // Arrange
        var staff = new FakeStaffProvider();
        await using var harness = await CreateAsync(staff);
        var skill = await harness.AddSkillAsync("Welding");

        // Act
        var refused = await InvokeAsync(harness, new SaveSkillAssignmentTool(), new() { ["subjectKind"] = "Staff", ["subjectId"] = "s-priya", ["skillId"] = skill.ItemId });
        var missing = await InvokeAsync(harness, new GetSubjectSkillsTool(), new() { ["subjectKind"] = "Staff", ["subjectId"] = "s-ghost" });
        staff.Allowed = true;
        var saved = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SaveSkillAssignmentTool(), new() { ["subjectKind"] = "Staff", ["subjectId"] = "s-priya", ["skillId"] = skill.ItemId }));
        var holders = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new FindSkillHoldersTool(), new() { ["skillId"] = skill.ItemId, ["subjectKind"] = "Staff" }));

        // Assert
        Assert.Equal("Nothing was saved: You are not allowed to change the skills or certificates of Priya Shah: they are not among the people you manage.", refused);
        Assert.Equal("There is no staff member with the id 's-ghost'. Use getStaffMember to find their id.", missing);
        Assert.True(saved.GetProperty("countsToday").GetBoolean());
        Assert.Equal("Priya Shah", Assert.Single(holders.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task StaffSubjects_AreRefused_WhenNoProviderDescribesThem()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var result = await InvokeAsync(harness, new GetSubjectSkillsTool(), new() { ["subjectKind"] = "Staff", ["subjectId"] = "s-priya" });

        // Assert
        Assert.Equal("Staff members can't be used here because the Staff feature is not enabled.", result);
    }

    [Fact]
    public async Task SaveCertificate_TakesAMediaLibraryPath_AndRefusesAMissingFileOrAnAddress()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var osha = await harness.AddCertificationAsync("OSHA Forklift", certification => certification.RequiresDocument = true);
        var recorded = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SaveCertificateTool(), new() { ["subjectKind"] = "User", ["subjectId"] = LuisId, ["certificationId"] = osha.ItemId }));
        var id = recorded.GetProperty("certificate").GetProperty("id").GetString();

        // Act
        var noCopy = await InvokeAsync(harness, new SetCertificateStatusTool(), new() { ["certificateId"] = id, ["action"] = "verify" });
        var missing = await InvokeAsync(harness, new SaveCertificateTool(), new() { ["certificateId"] = id, ["documentPath"] = "certificates/other.pdf" });
        var address = await InvokeAsync(harness, new SaveCertificateTool(), new() { ["certificateId"] = id, ["documentPath"] = "https://example.com/luis.pdf" });
        var climb = await InvokeAsync(harness, new SaveCertificateTool(), new() { ["certificateId"] = id, ["documentPath"] = "certificates/../secrets.txt" });
        var holder = await InvokeAsync(harness, new SaveCertificateTool(), new() { ["certificateId"] = id, ["subjectId"] = "someone" });
        var attached = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SaveCertificateTool(), new() { ["certificateId"] = id, ["documentPath"] = "/media/certificates/luis.pdf" }));
        var verified = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SetCertificateStatusTool(), new() { ["certificateId"] = id, ["action"] = "verify" }));

        // Assert
        Assert.Equal("Nothing was changed: Add a copy of the certificate before marking it valid.", noCopy);
        Assert.StartsWith("Nothing was saved: there is no file 'certificates/other.pdf'", missing, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was saved: 'https://example.com/luis.pdf' is an address", address, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was saved: 'certificates/../secrets.txt' is not a media library path", climb, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was saved: the holder of a certificate can't change.", holder, StringComparison.Ordinal);
        Assert.Equal("certificates/luis.pdf", attached.GetProperty("certificate").GetProperty("documentPath").GetString());
        Assert.Equal("Valid", verified.GetProperty("certificate").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ListCertificates_FiltersExpiringValidCertificates_AndRevokeAddsTheReason()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var osha = await harness.AddCertificationAsync("OSHA Forklift");
        var soon = await RecordAsync(harness, osha.ItemId, "A-1", new DateOnly(2026, 11, 20));
        await RecordAsync(harness, osha.ItemId, "B-2", new DateOnly(2027, 6, 1));

        await InvokeAsync(harness, new SetCertificateStatusTool(), new() { ["certificateId"] = soon, ["action"] = "verify" });

        // Act
        var expiring = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new ListCertificatesTool(), new() { ["expiringWithinDays"] = 30 }));
        var revoked = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SetCertificateStatusTool(), new() { ["certificateId"] = soon, ["action"] = "revoke", ["reason"] = "Failed the refresher." }));
        var afterRevoke = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new ListCertificatesTool(), new() { ["expiringWithinDays"] = 30 }));
        var byPerson = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new ListCertificatesTool(), new() { ["subjectKind"] = "User", ["subjectId"] = "luis" }));

        // Assert
        Assert.Equal("A-1", Assert.Single(expiring.GetProperty("items").EnumerateArray()).GetProperty("number").GetString());
        Assert.Equal("Revoked", revoked.GetProperty("certificate").GetProperty("status").GetString());
        Assert.Equal("Failed the refresher.", revoked.GetProperty("certificate").GetProperty("notes").GetString());
        Assert.Equal(0, afterRevoke.GetProperty("total").GetInt32());
        Assert.Equal(2, byPerson.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Deletes_RemoveTheRecord_AndReportAMissingOne()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var osha = await harness.AddCertificationAsync("OSHA Forklift");
        var skill = await harness.AddSkillAsync("Welding");
        var certificate = await RecordAsync(harness, osha.ItemId, "A-1", null);

        // Act
        var deletedCertificate = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new DeleteCertificateTool(), new() { ["certificateId"] = certificate }));
        var deletedSkill = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new DeleteSkillTool(), new() { ["skillId"] = skill.ItemId }));
        var deletedType = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new DeleteCertificationTypeTool(), new() { ["certificationId"] = osha.ItemId }));
        var missing = await InvokeAsync(harness, new DeleteSkillTool(), new() { ["skillId"] = skill.ItemId });
        var list = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new ListCertificatesTool(), []));

        // Assert
        Assert.True(deletedCertificate.GetProperty("deleted").GetBoolean());
        Assert.True(deletedSkill.GetProperty("deleted").GetBoolean());
        Assert.True(deletedType.GetProperty("deleted").GetBoolean());
        Assert.StartsWith("Nothing was deleted: there is no skill with the id", missing, StringComparison.Ordinal);
        Assert.Equal(0, list.GetProperty("total").GetInt32());
    }

    private static async Task<string> RecordAsync(SkillsTestHarness harness, string certificationId, string number, DateOnly? expiresOn)
    {
        var arguments = new Dictionary<string, object>
        {
            ["subjectKind"] = "User",
            ["subjectId"] = LuisId,
            ["certificationId"] = certificationId,
            ["number"] = number,
        };

        if (expiresOn is { } date)
        {
            arguments["expiresOn"] = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        var json = AgentToolTestContext.ReadJson(await InvokeAsync(harness, new SaveCertificateTool(), arguments));

        return json.GetProperty("certificate").GetProperty("id").GetString();
    }

    private static Task<string> InvokeAsync(SkillsTestHarness harness, AIFunction tool, Dictionary<string, object> arguments)
        => harness.RunAsync(services => AgentToolTestContext.InvokeAsync(tool, services, arguments));

    private static Task<SkillsTestHarness> CreateAsync(FakeStaffProvider staff = null)
        => SkillsTestHarness.CreateAsync(services =>
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, AgentToolTestContext.UserId),
                    new Claim(ClaimTypes.Name, AgentToolTestContext.UserName),
                ], "Test")),
            };

            // A double, not HttpContextAccessor: its async-local value set here would not reach the tool calls.
            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);
            services.AddSingleton(httpContextAccessor.Object);
            services.AddSingleton<IAuthorizationService>(new FakeAuthorizationService((_, _) => true));

            var localClock = new Mock<ILocalClock>();
            localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value);
            services.AddSingleton(localClock.Object);

            var luis = new User { UserId = LuisId, UserName = "luis", Email = "luis@example.com" };
            var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
            userManager.Setup(manager => manager.FindByIdAsync(LuisId)).ReturnsAsync(luis);
            userManager.Setup(manager => manager.FindByNameAsync("luis")).ReturnsAsync(luis);
            userManager.Setup(manager => manager.GetUserIdAsync(luis)).ReturnsAsync(LuisId);
            services.AddSingleton(userManager.Object);

            var displayNames = new Mock<IDisplayNameProvider>();
            displayNames.Setup(provider => provider.GetAsync(luis, It.IsAny<CancellationToken>())).ReturnsAsync("Luis Ortega");
            services.AddSingleton(displayNames.Object);

            var userSearch = new Mock<IUserSearchService>();
            userSearch.Setup(search => search.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids.Contains(LuisId)
                    ? [new UserSearchResult { UserId = LuisId, UserName = "luis", DisplayName = "Luis Ortega" }]
                    : []);
            services.AddSingleton(userSearch.Object);
            services.AddScoped<ISkillSubjectProvider, UserSkillSubjectProvider>();

            if (staff is not null)
            {
                services.AddSingleton<ISkillSubjectProvider>(staff);
            }

            var file = new Mock<IFileStoreEntry>();
            file.SetupGet(entry => entry.Path).Returns("certificates/luis.pdf");
            var mediaFileStore = new Mock<IMediaFileStore>();
            mediaFileStore.Setup(store => store.MapPathToPublicUrl(It.IsAny<string>())).Returns((string path) => "/media/" + path);
            mediaFileStore.Setup(store => store.GetFileInfoAsync("certificates/luis.pdf")).ReturnsAsync(file.Object);
            services.AddSingleton(mediaFileStore.Object);
        });

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            ListSkillsTool.TheName => new ListSkillsTool(),
            SaveSkillTool.TheName => new SaveSkillTool(),
            DeleteSkillTool.TheName => new DeleteSkillTool(),
            ListCertificationTypesTool.TheName => new ListCertificationTypesTool(),
            SaveCertificationTypeTool.TheName => new SaveCertificationTypeTool(),
            DeleteCertificationTypeTool.TheName => new DeleteCertificationTypeTool(),
            GetSubjectSkillsTool.TheName => new GetSubjectSkillsTool(),
            SaveSkillAssignmentTool.TheName => new SaveSkillAssignmentTool(),
            RemoveSkillAssignmentTool.TheName => new RemoveSkillAssignmentTool(),
            FindSkillHoldersTool.TheName => new FindSkillHoldersTool(),
            ListCertificatesTool.TheName => new ListCertificatesTool(),
            SaveCertificateTool.TheName => new SaveCertificateTool(),
            SetCertificateStatusTool.TheName => new SetCertificateStatusTool(),
            _ => new DeleteCertificateTool(),
        };

    private sealed class FakeStaffProvider : ISkillSubjectProvider
    {
        public bool Allowed { get; set; }

        public string Kind => SkillSubjectKind.Staff;

        public Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(ids.Where(id => id == "s-priya").ToDictionary(id => id, _ => "Priya Shah"));

        public Task<bool> CanManageAsync(ClaimsPrincipal user, string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Allowed);
    }
}
