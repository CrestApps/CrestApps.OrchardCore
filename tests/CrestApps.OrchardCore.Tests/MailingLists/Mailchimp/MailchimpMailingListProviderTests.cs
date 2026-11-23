using System.Globalization;
using System.Net;
using CrestApps.Core;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

public sealed class MailchimpMailingListProviderTests
{
    private const string Email = "Person@Example.com";

    [Fact]
    public void Provider_IsNamedMailchimp_AndHasEveryCapability()
    {
        // Arrange
        using var context = new MailchimpTestContext();

        // Act
        var capabilities = context.Provider.Capabilities;

        // Assert
        Assert.Equal("Mailchimp", context.Provider.Name);
        Assert.Equal("Mailchimp", context.Provider.DisplayName.Value);
        Assert.True(capabilities.HasFlag(MailingListProviderCapabilities.ConfirmsAddresses));
        Assert.True(capabilities.HasFlag(MailingListProviderCapabilities.DeliversWelcomeOffer));
        Assert.True(capabilities.HasFlag(MailingListProviderCapabilities.ReportsChanges));
        Assert.True(capabilities.HasFlag(MailingListProviderCapabilities.SendsCampaigns));
    }

    [Fact]
    public async Task IsConfigured_FollowsTheCredentials()
    {
        // Arrange
        using var configured = new MailchimpTestContext();
        using var notConfigured = new MailchimpTestContext(configured: false);

        // Act and assert
        Assert.True(await configured.Provider.IsConfiguredAsync(TestContext.Current.CancellationToken));
        Assert.False(await notConfigured.Provider.IsConfiguredAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Push_SubscribedMember_PutsTheMemberOnItsHash_WithNamesAndTags_AndTheKeyInTheHeaderOnly()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler
            .Answer(HttpMethod.Put, path, """{"id":"x","status":"subscribed"}""")
            .Answer(HttpMethod.Post, path + "/tags", string.Empty, HttpStatusCode.NoContent);

        var list = MailchimpTestContext.List(configure: settings => settings.Tags = ["website-signup", "news"]);
        var member = MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed, "Ada Lovelace Byron");

        // Act
        var result = await context.Provider.PushAsync(list, member, MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(MailchimpSubscriberHash.Compute(Email), result.ProviderMemberId);

        var (_, _, request, body) = Assert.Single(context.Handler.CallsTo(HttpMethod.Put, path));
        Assert.Equal("https://us21.api.mailchimp.com" + path, request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("key-123-us21", request.Headers.Authorization.Parameter);
        Assert.DoesNotContain("key-123", request.RequestUri.ToString(), StringComparison.Ordinal);
        Assert.Equal(Email, body["email_address"]!.GetValue<string>());
        Assert.Equal("subscribed", body["status_if_new"]!.GetValue<string>());
        Assert.Null(body["status"]);
        Assert.Equal("Ada", body["merge_fields"]!["FNAME"]!.GetValue<string>());
        Assert.Equal("Lovelace Byron", body["merge_fields"]!["LNAME"]!.GetValue<string>());

        var (_, _, _, tags) = Assert.Single(context.Handler.CallsTo(HttpMethod.Post, path + "/tags"));
        Assert.Equal(["website-signup", "news"], tags["tags"]!.AsArray().Select(tag => tag!["name"]!.GetValue<string>()));
        Assert.All(tags["tags"]!.AsArray(), tag => Assert.Equal("active", tag!["status"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Push_SubscribedMember_ThatMailchimpHoldsAsUnsubscribed_IsAskedToConfirmAgain()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler
            .Answer(HttpMethod.Put, path, """{"id":"x","status":"unsubscribed"}""")
            .Answer(HttpMethod.Put, path, """{"id":"x","status":"pending"}""");

        var member = MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed);

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(), member, MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var calls = context.Handler.CallsTo(HttpMethod.Put, path).ToList();
        Assert.Equal(2, calls.Count);
        Assert.Null(calls[0].Body["status"]);
        Assert.Equal("pending", calls[1].Body["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Push_SubscribedMember_ThatMailchimpHoldsAsSubscribed_IsPutOnce()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler.Answer(HttpMethod.Put, path, """{"id":"x","status":"subscribed"}""");

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(), MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Single(context.Handler.Calls);
    }

    [Fact]
    public async Task Push_PendingMember_OnAListTheProviderConfirms_IsAddedAsPending()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler.Answer(HttpMethod.Put, path, """{"id":"x","status":"pending"}""");

        var list = MailchimpTestContext.List(MailingListConfirmation.Provider);

        // Act
        var result = await context.Provider.PushAsync(list, MailchimpTestContext.Member(Email, MailingListMemberStatus.Pending), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var (_, _, _, body) = Assert.Single(context.Handler.Calls);
        Assert.Equal("pending", body["status_if_new"]!.GetValue<string>());
        Assert.Null(body["status"]);
    }

    [Fact]
    public async Task Push_PendingMember_PushedTwice_NeverAsksMailchimpForASecondConfirmation()
    {
        // Arrange
        // The outbox may send the same change twice. The second push finds the member pending at Mailchimp and leaves
        // its status alone, so Mailchimp sends one confirmation e-mail.
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler.Answer(HttpMethod.Put, path, """{"id":"x","status":"pending"}""");

        var list = MailchimpTestContext.List(MailingListConfirmation.Provider);
        var member = MailchimpTestContext.Member(Email, MailingListMemberStatus.Pending);

        // Act
        var first = await context.Provider.PushAsync(list, member, MailingListPushKind.Upsert, TestContext.Current.CancellationToken);
        var second = await context.Provider.PushAsync(list, member, MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        var calls = context.Handler.CallsTo(HttpMethod.Put, path).ToList();
        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Null(call.Body["status"]));
    }

    [Fact]
    public async Task Push_PendingMember_ThatMailchimpHoldsAsUnsubscribed_IsAskedToConfirm()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler
            .Answer(HttpMethod.Put, path, """{"id":"x","status":"unsubscribed"}""")
            .Answer(HttpMethod.Put, path, """{"id":"x","status":"pending"}""");

        var list = MailchimpTestContext.List(MailingListConfirmation.Provider);

        // Act
        var result = await context.Provider.PushAsync(list, MailchimpTestContext.Member(Email, MailingListMemberStatus.Pending), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var calls = context.Handler.CallsTo(HttpMethod.Put, path).ToList();
        Assert.Equal(2, calls.Count);
        Assert.Equal("pending", calls[1].Body["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Push_PendingMember_ThatMailchimpHoldsAsSubscribed_StaysSubscribed()
    {
        // Arrange
        // A sign-up typed on the site, possibly by someone else, never takes a confirmed subscriber back to pending.
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler.Answer(HttpMethod.Put, path, """{"id":"x","status":"subscribed"}""");

        var list = MailchimpTestContext.List(MailingListConfirmation.Provider);

        // Act
        var result = await context.Provider.PushAsync(list, MailchimpTestContext.Member(Email, MailingListMemberStatus.Pending), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var (_, _, _, body) = Assert.Single(context.Handler.Calls);
        Assert.Null(body["status"]);
    }

    [Fact]
    public async Task Push_PendingMember_OnAListTheSiteConfirms_CallsNothing()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var member = MailchimpTestContext.Member(Email, MailingListMemberStatus.Pending);
        member.ProviderMemberId = "kept";

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(MailingListConfirmation.Email), member, MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("kept", result.ProviderMemberId);
        Assert.Empty(context.Handler.Calls);
    }

    [Fact]
    public async Task Push_WelcomeCouponDeliveredByTheProvider_WritesTheCodeToTheCouponMergeField()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler.Answer(HttpMethod.Put, path, """{"id":"x","status":"subscribed"}""");

        var list = MailchimpTestContext.List(configure: settings => settings.CouponMergeTag = "OFFER");
        list.Put(new MailingListWelcomeCoupon
        {
            IsEnabled = true,
            CouponId = "coupon-1",
            Delivery = MailingListCouponDelivery.Provider,
        });

        var member = MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed);
        member.CouponCodeId = "code-1";
        member.Put(new MailingListMemberCoupon { Code = "WELCOME-ABC123" });

        // Act
        var result = await context.Provider.PushAsync(list, member, MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var (_, _, _, body) = Assert.Single(context.Handler.Calls);
        Assert.Equal("WELCOME-ABC123", body["merge_fields"]!["OFFER"]!.GetValue<string>());
    }

    [Fact]
    public async Task Push_WelcomeCouponDeliveredByEmail_SendsNoCouponMergeField()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler.Answer(HttpMethod.Put, path, """{"id":"x","status":"subscribed"}""");

        var list = MailchimpTestContext.List(configure: settings => settings.SendNameMergeFields = false);
        list.Put(new MailingListWelcomeCoupon
        {
            IsEnabled = true,
            CouponId = "coupon-1",
            Delivery = MailingListCouponDelivery.Email,
        });

        var member = MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed, "Ada");
        member.CouponCodeId = "code-1";
        member.Put(new MailingListMemberCoupon { Code = "WELCOME-ABC123" });

        // Act
        await context.Provider.PushAsync(list, member, MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        var (_, _, _, body) = Assert.Single(context.Handler.Calls);
        Assert.Null(body["merge_fields"]);
    }

    [Fact]
    public async Task Push_Unsubscribe_PatchesTheStatus_AndAnAddressMailchimpDoesNotHaveIsDone()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(), MailchimpTestContext.Member(Email, MailingListMemberStatus.Unsubscribed), MailingListPushKind.Unsubscribe, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var (_, _, _, body) = Assert.Single(context.Handler.CallsTo(HttpMethod.Patch, path));
        Assert.Equal("unsubscribed", body["status"]!.GetValue<string>());
        Assert.Empty(context.Handler.CallsTo(HttpMethod.Put, path));
    }

    [Fact]
    public async Task Push_Erase_DeletesPermanently_AndAMemberAlreadyGoneIsDone()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email) + "/actions/delete-permanent";
        context.Handler
            .Answer(HttpMethod.Post, path, string.Empty, HttpStatusCode.NoContent)
            .Answer(HttpMethod.Post, path, """{"title":"Resource Not Found","status":404}""", HttpStatusCode.NotFound);

        var member = MailchimpTestContext.Member(Email, MailingListMemberStatus.Unsubscribed);

        // Act
        var first = await context.Provider.PushAsync(MailchimpTestContext.List(), member, MailingListPushKind.Erase, TestContext.Current.CancellationToken);
        var second = await context.Provider.PushAsync(MailchimpTestContext.List(), member, MailingListPushKind.Erase, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(2, context.Handler.CallsTo(HttpMethod.Post, path).Count());
    }

    [Fact]
    public async Task Push_Erase_OfAMemberWithoutAnAddressLeft_UsesTheKeptHash()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var hash = MailchimpSubscriberHash.Compute(Email);
        var path = $"/3.0/lists/{MailchimpTestContext.AudienceId}/members/{hash}/actions/delete-permanent";
        context.Handler.Answer(HttpMethod.Post, path, string.Empty, HttpStatusCode.NoContent);

        var member = MailchimpTestContext.Member(Email, MailingListMemberStatus.Unsubscribed);
        member.Contact = null;
        member.NormalizedEmail = null;
        member.ProviderMemberId = hash;

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(), member, MailingListPushKind.Erase, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Single(context.Handler.CallsTo(HttpMethod.Post, path));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    public async Task Push_AnError_IsRetryableOnlyForRateLimitsTimeOutsAndServerErrors(HttpStatusCode status, bool canRetry)
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler.Problem(HttpMethod.Put, path, status, "Some Title", "Details.");

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(), MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(canRetry, result.CanRetry);
        Assert.Contains(((int)status).ToString(CultureInfo.InvariantCulture), result.Error, StringComparison.Ordinal);
        Assert.Contains("Some Title", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Push_ARefusal_NeverCarriesTheAddressMailchimpNamedInItsDetail()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = MailchimpTestContext.MemberPath(Email);
        context.Handler.Problem(HttpMethod.Put, path, HttpStatusCode.BadRequest, "Invalid Resource", "Person@Example.com looks fake or invalid, please enter a real email address.");

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(), MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.CanRetry);
        Assert.DoesNotContain("@", result.Error, StringComparison.Ordinal);
        Assert.Contains("looks fake or invalid", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Push_AConnectionFailure_IsRetryable()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        context.Handler.Throw(HttpMethod.Put, MailchimpTestContext.MemberPath(Email), new HttpRequestException("No route to host."));

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(), MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.CanRetry);
    }

    [Fact]
    public async Task Push_ATimeOut_IsRetryable()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        context.Handler.Throw(HttpMethod.Put, MailchimpTestContext.MemberPath(Email), new TaskCanceledException("The request timed out."));

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(), MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.CanRetry);
    }

    [Fact]
    public async Task Push_WithoutCredentials_FailsWithoutCallingMailchimp()
    {
        // Arrange
        using var context = new MailchimpTestContext(configured: false);

        // Act
        var result = await context.Provider.PushAsync(MailchimpTestContext.List(), MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.CanRetry);
        Assert.Empty(context.Handler.Calls);
    }

    [Fact]
    public async Task Push_WithoutAnAudience_FailsWithoutCallingMailchimp()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var list = MailchimpTestContext.List(configure: settings => settings.AudienceId = null);

        // Act
        var result = await context.Provider.PushAsync(list, MailchimpTestContext.Member(Email, MailingListMemberStatus.Subscribed), MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.CanRetry);
        Assert.Empty(context.Handler.Calls);
    }

    [Fact]
    public async Task GetChanges_ReadsTheMembersChangedSince_AndPagesByOffset()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        var path = $"/3.0/lists/{MailchimpTestContext.AudienceId}/members";
        context.Handler.Answer(HttpMethod.Get, path, $$"""
            {
              "total_items": 1200,
              "members": [
                { "id": "{{MailchimpSubscriberHash.Compute("a@example.com")}}", "email_address": "a@example.com", "status": "unsubscribed", "last_changed": "2026-11-21T08:30:00+00:00", "merge_fields": { "FNAME": "Ada", "LNAME": "Byron", "ADDRESS": { "addr1": "1 Main" } } },
                { "id": "{{MailchimpSubscriberHash.Compute("b@example.com")}}", "email_address": "b@example.com", "status": "cleaned", "last_changed": "2026-11-21T09:00:00+00:00", "merge_fields": {} },
                { "id": "{{MailchimpSubscriberHash.Compute("c@example.com")}}", "email_address": "c@example.com", "status": "transactional", "last_changed": "2026-11-21T09:30:00+00:00", "merge_fields": {} }
              ]
            }
            """);

        var since = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var page = await context.Provider.GetChangesAsync(MailchimpTestContext.List(), since, "500", TestContext.Current.CancellationToken);

        // Assert
        var (_, _, request, _) = Assert.Single(context.Handler.Calls);
        var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
        Assert.Equal("2026-11-20T00:00:00+00:00", query["since_last_changed"].ToString());
        Assert.Equal("500", query["offset"].ToString());
        Assert.Equal("500", query["count"].ToString());
        Assert.Equal("503", page.NextCursor);

        Assert.Equal(2, page.Changes.Count);
        var unsubscribed = page.Changes[0];
        Assert.Equal("list-1", unsubscribed.ListId);
        Assert.Equal("a@example.com", unsubscribed.Email);
        Assert.Equal(MailingListMemberStatus.Unsubscribed, unsubscribed.Status);
        Assert.Equal("Ada Byron", unsubscribed.Name);
        Assert.Equal(new DateTime(2026, 11, 21, 8, 30, 0, DateTimeKind.Utc), unsubscribed.OccurredUtc);
        Assert.Equal(MailchimpSubscriberHash.Compute("a@example.com"), unsubscribed.ProviderMemberId);
        Assert.Equal(MailingListMemberStatus.Cleaned, page.Changes[1].Status);
    }

    [Fact]
    public async Task GetChanges_OnTheLastPage_HasNoCursor()
    {
        // Arrange
        using var context = new MailchimpTestContext();
        context.Handler.Answer(HttpMethod.Get, $"/3.0/lists/{MailchimpTestContext.AudienceId}/members", """
            { "total_items": 1, "members": [ { "email_address": "a@example.com", "status": "subscribed", "last_changed": "2026-11-21T08:30:00+00:00" } ] }
            """);

        // Act
        var page = await context.Provider.GetChangesAsync(MailchimpTestContext.List(), DateTime.UnixEpoch, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(page.NextCursor);
        Assert.Single(page.Changes);
    }

    [Theory]
    [InlineData(MailingListConfirmation.Provider, true)]
    [InlineData(MailingListConfirmation.Email, false)]
    [InlineData(MailingListConfirmation.None, false)]
    public async Task GetChanges_ASubscribedMember_IsVerifiedOnlyWhenTheProviderConfirms(MailingListConfirmation confirmation, bool verified)
    {
        // Arrange
        using var context = new MailchimpTestContext();
        context.Handler.Answer(HttpMethod.Get, $"/3.0/lists/{MailchimpTestContext.AudienceId}/members", """
            { "total_items": 1, "members": [ { "email_address": "a@example.com", "status": "subscribed", "last_changed": "2026-11-21T08:30:00+00:00" } ] }
            """);

        // Act
        var page = await context.Provider.GetChangesAsync(MailchimpTestContext.List(confirmation), DateTime.UnixEpoch, null, TestContext.Current.CancellationToken);

        // Assert
        var change = Assert.Single(page.Changes);
        Assert.Equal(MailingListMemberStatus.Subscribed, change.Status);
        Assert.Equal(verified, change.Verified);
    }

    [Fact]
    public async Task GetChanges_WithoutCredentials_Throws_SoTheReconciliationKeepsItsLastRun()
    {
        // Arrange
        using var context = new MailchimpTestContext(configured: false);

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Provider.GetChangesAsync(MailchimpTestContext.List(), DateTime.UnixEpoch, null, TestContext.Current.CancellationToken));
    }
}
