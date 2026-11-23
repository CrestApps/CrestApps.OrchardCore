using CrestApps.Core;
using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.MailingLists.Mailchimp;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

public sealed class MailchimpMailingListSettingsValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ValidSettings_ReturnsNoErrors()
    {
        // Arrange
        var list = MailchimpList(new MailchimpListSettings { AudienceId = "a1b2c3d4e5", CouponMergeTag = "WELCOME_1" });

        // Act
        var errors = await CreateValidator().ValidateAsync(list, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidateAsync_NoAudienceAndNoMergeTag_ReturnsNoErrors()
    {
        // Arrange
        var list = MailchimpList(new MailchimpListSettings { AudienceId = null, CouponMergeTag = null });

        // Act
        var errors = await CreateValidator().ValidateAsync(list, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidateAsync_BadAudienceIdAndMergeTag_ReturnsAnErrorForEach()
    {
        // Arrange
        var list = MailchimpList(new MailchimpListSettings { AudienceId = "not an id!", CouponMergeTag = "coupon-code-too-long" });

        // Act
        var errors = await CreateValidator().ValidateAsync(list, TestContext.Current.CancellationToken);

        // Assert
        Assert.Collection(
            errors,
            error => Assert.Equal(nameof(MailchimpListSettings.AudienceId), Assert.Single(error.MemberNames)),
            error => Assert.Equal(nameof(MailchimpListSettings.CouponMergeTag), Assert.Single(error.MemberNames)));
    }

    [Fact]
    public async Task ValidateAsync_ListKeptOnTheSite_IgnoresMailchimpSettings()
    {
        // Arrange
        var list = new MailingList { ProviderName = MailingListsConstants.LocalProviderName };
        list.Put(new MailchimpListSettings { AudienceId = "not an id!" });

        // Act
        var errors = await CreateValidator().ValidateAsync(list, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(errors);
    }

    private static MailingList MailchimpList(MailchimpListSettings settings)
    {
        var list = new MailingList { ProviderName = MailchimpConstants.ProviderName };
        list.Put(settings);

        return list;
    }

    private static MailchimpMailingListSettingsValidator CreateValidator()
        => new(new StringLocalizer<MailchimpMailingListSettingsValidator>(new SourceTextLocalizerFactory()));
}
