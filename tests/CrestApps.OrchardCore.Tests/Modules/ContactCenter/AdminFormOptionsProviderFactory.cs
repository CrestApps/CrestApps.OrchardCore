using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Moq;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Builds the real <see cref="ContactCenterAdminFormOptionsProvider"/> the Contact Center admin editors fill their
/// pickers from, over catalogs holding only what a test gives them.
/// </summary>
internal static class AdminFormOptionsProviderFactory
{
    public static ContactCenterAdminFormOptionsProvider Create(
        IEnumerable<ActivityQueue> queues = null,
        IEnumerable<VoiceMediaItem> voiceMedia = null,
        IEnumerable<IContactCenterVoiceProvider> voiceProviders = null,
        IPhoneNumberService phoneNumberService = null)
    {
        var campaigns = new Mock<ICatalogManager<OmnichannelCampaign>>();
        campaigns.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync((queues ?? []).ToList());

        var queueGroups = new Mock<IActivityQueueGroupManager>();
        queueGroups.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var skills = new Mock<IContactCenterSkillManager>();
        skills.Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var calendars = new Mock<IBusinessHoursCalendarManager>();
        calendars.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var endpoints = new Mock<IOmnichannelChannelEndpointManager>();
        endpoints.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var agents = new Mock<IAgentProfileManager>();
        agents.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var media = new Mock<IVoiceMediaItemManager>();
        media.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync((voiceMedia ?? []).ToList());

        return new ContactCenterAdminFormOptionsProvider(
            campaigns.Object,
            queueManager.Object,
            queueGroups.Object,
            skills.Object,
            calendars.Object,
            endpoints.Object,
            agents.Object,
            voiceProviders ?? [],
            new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null).Object,
            Mock.Of<IDisplayNameProvider>(),
            phoneNumberService is null ? [] : [phoneNumberService],
            media.Object);
    }
}
