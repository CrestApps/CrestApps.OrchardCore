using System.Security.Claims;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.MailingLists.ContentTransfer;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// The ContentTransfer import of mailing list members signs each row up through the member service as a silent import,
/// with the row's status, finds the list by name or id, and never subscribes an address that unsubscribed or bounced.
/// </summary>
public sealed class MailingListMembersImportTargetTests
{
    [Fact]
    public async Task Rows_AreSignedUpSilently_AsAnImport_WithTheirStatus()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);
        var target = Target(harness, list);
        var batch = Batch(
            Row(1, list.Name, "Ada@Example.com", "Ada Lovelace", null),
            Row(2, list.ItemId, "grace@example.com", null, "pending"),
            Row(3, list.Name.ToUpperInvariant(), "alan@example.com", null, "Subscribed"));

        // Act
        await target.ImportAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(batch.Rows, row => Assert.False(row.Failed, row.Error));
        Assert.Empty(harness.Emails.Sent);

        var ada = await harness.Members.FindAsync(list.ItemId, "ada@example.com", TestContext.Current.CancellationToken);
        var grace = await harness.Members.FindAsync(list.ItemId, "grace@example.com", TestContext.Current.CancellationToken);
        var alan = await harness.Members.FindAsync(list.ItemId, "alan@example.com", TestContext.Current.CancellationToken);

        Assert.Equal(MailingListMemberStatus.Subscribed, ada.Status);
        Assert.Equal("Ada Lovelace", ada.Contact.Name);
        Assert.Equal(MailingListMemberSources.Import, ada.Source);
        Assert.Equal("entry-1", ada.SourceId);
        Assert.Equal(MailingListMemberStatus.Pending, grace.Status);
        Assert.Null(grace.VerificationNonce);
        Assert.Equal(MailingListMemberStatus.Subscribed, alan.Status);
    }

    [Fact]
    public async Task AnAddressThatUnsubscribed_IsRefused_AndStaysUnsubscribed()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.None);
        var target = Target(harness, list);

        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        await harness.Service.UnsubscribeAsync(list.ItemId, "ada@example.com", MailingListMemberSources.UnsubscribeLink, cancellationToken: TestContext.Current.CancellationToken);
        harness.Emails.Sent.Clear();

        var batch = Batch(Row(1, list.Name, "ADA@example.com", null, "Subscribed"));

        // Act
        await target.ImportAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        var row = Assert.Single(batch.Rows);
        Assert.True(row.Failed);
        Assert.Contains("unsubscribed", row.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MailingListMemberStatus.Unsubscribed, (await harness.Members.FindAsync(list.ItemId, "ada@example.com", TestContext.Current.CancellationToken)).Status);
        Assert.Empty(harness.Emails.Sent);
    }

    [Fact]
    public async Task AnAddressThatStoppedAllMarketing_IsRefused_AndNotAdded()
    {
        // Arrange
        var harness = new MailingListServiceHarness { MarketingOptOut = true };
        var list = harness.AddList(MailingListConfirmation.None);
        var target = Target(harness, list);
        var batch = Batch(Row(1, list.Name, "ada@example.com", null, null), Row(2, list.Name, "grace@example.com", null, "Pending"));

        // Act
        await target.ImportAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(batch.Rows, row =>
        {
            Assert.True(row.Failed);
            Assert.Contains("stop all marketing", row.Error, StringComparison.OrdinalIgnoreCase);
        });

        Assert.Empty(harness.Members.All);
        Assert.Empty(harness.Emails.Sent);
    }

    [Fact]
    public async Task AnAddressAlreadySubscribed_IsKeptAsItIs()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.None);
        var target = Target(harness, list);

        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);

        var batch = Batch(Row(1, list.Name, "ada@example.com", null, "Pending"));

        // Act
        await target.ImportAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        var member = await harness.Members.FindAsync(list.ItemId, "ada@example.com", TestContext.Current.CancellationToken);
        Assert.False(Assert.Single(batch.Rows).Failed);
        Assert.Equal(MailingListMemberStatus.Subscribed, member.Status);
        Assert.Equal(MailingListMemberSources.Widget, member.Source);
    }

    [Fact]
    public async Task RowsWithoutAListOrAddress_OrWithAnUnknownListOrStatus_AreRefused()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);
        var target = Target(harness, list);
        var batch = Batch(
            Row(1, null, "ada@example.com", null, null),
            Row(2, "no-such-list", "ada@example.com", null, null),
            Row(3, list.Name, "not an address", null, null),
            Row(4, list.Name, "ada@example.com", null, "Unsubscribed"),
            Row(5, list.Name, "ada@example.com", null, "2"));

        // Act
        await target.ImportAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.All(batch.Rows, row =>
        {
            Assert.True(row.Failed);
            Assert.False(string.IsNullOrEmpty(row.Error));
        });

        Assert.Empty(harness.Members.All);
    }

    [Fact]
    public async Task OnlyPeopleWhoManageMembers_MayImport_AndTheFileHasTheListAndAddressColumns()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var target = Target(harness, harness.AddList(MailingListConfirmation.Email));
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "Test"));

        // Act
        var allowed = await target.AuthorizeAsync(user, Authorization(MailingListsPermissions.ManageMailingListMembers.Name));
        var denied = await target.AuthorizeAsync(user, Authorization(MailingListsPermissions.ViewMailingListMembers.Name));
        var columns = await target.GetColumnsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(allowed);
        Assert.False(denied);
        Assert.False(target.IsContentType);
        Assert.Equal(MailingListMembersImportTarget.TargetName, target.Name);
        Assert.Equal(["List", "Email", "Name", "Status"], columns.Select(column => column.Name));
        Assert.Equal(["List", "Email"], columns.Where(column => column.IsRequired).Select(column => column.Name));
    }

    private static MailingListMembersImportTarget Target(MailingListServiceHarness harness, params MailingList[] lists)
    {
        var manager = new Mock<IMailingListManager>();

        manager.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => lists.FirstOrDefault(list => list.ItemId == id));
        manager.Setup(value => value.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => lists.FirstOrDefault(list => string.Equals(list.Name, name, StringComparison.OrdinalIgnoreCase)));

        return new MailingListMembersImportTarget(manager.Object, harness.Members, harness.Service, harness.Consent.Object, new PassThroughStringLocalizer<MailingListMembersImportTarget>());
    }

    private static ContentTransferImportBatch Batch(params ContentTransferImportRow[] rows)
        => new(new ContentTransferEntry { EntryId = "entry-1", UploadedFileName = "members.csv" }, rows);

    private static ContentTransferImportRow Row(int index, string list, string email, string name, string status)
        => new(index, new Dictionary<string, string>
        {
            ["List"] = list,
            ["Email"] = email,
            ["Name"] = name,
            ["Status"] = status,
        });

    private static IAuthorizationService Authorization(string grant)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == grant)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }
}
