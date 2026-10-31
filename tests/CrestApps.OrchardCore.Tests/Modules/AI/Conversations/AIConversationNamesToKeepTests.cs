using CrestApps.Core;
using CrestApps.Core.AI.Chat.Privacy;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Drivers;
using CrestApps.OrchardCore.AI.Services;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

/// <summary>
/// The names a redaction keeps because they are not the customer's: live, a call's transcript came back with the
/// assistant's own name redacted wherever it introduced itself.
/// </summary>
public sealed class AIConversationNamesToKeepTests
{
    [Fact]
    public async Task TheSiteName_IsKept()
    {
        // Arrange
        var site = new Mock<ISite>();
        site.SetupGet(value => value.SiteName).Returns(" Contoso Motors ");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        var context = NewContext(new AIChatSession { SessionId = "s1" });
        context.NamesToKeep.Add("contoso motors");

        // Act
        await new AISiteNamePrivacyHandler(siteService.Object).CollectingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["contoso motors"], context.NamesToKeep);

        var unnamed = NewContext(new AIChatSession { SessionId = "s2" });
        site.SetupGet(value => value.SiteName).Returns(" ");
        await new AISiteNamePrivacyHandler(siteService.Object).CollectingAsync(unnamed, TestContext.Current.CancellationToken);
        Assert.Empty(unnamed.NamesToKeep);
    }

    [Fact]
    public async Task TheAssistantNames_OfThePersonaTheConversationStartedWith_AndOfItsProfilesPersonaNow_AreKept()
    {
        // Arrange: the call started as Sarah's and was handed to a profile whose persona is Alex.
        var profile = new AIProfile { ItemId = "p2" };
        profile.Put(new AIPersonaMetadata { PersonaId = "service-desk" });

        var profileManager = new Mock<IAIProfileManager>();
        profileManager.Setup(value => value.FindByIdAsync("p2", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var personaManager = new Mock<IAIPersonaManager>();
        personaManager.Setup(value => value.FindByNameAsync("sales-desk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIPersona { Name = "sales-desk", AssistantName = "Sarah" });
        personaManager.Setup(value => value.FindByNameAsync("service-desk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIPersona { Name = "service-desk", AssistantName = "Alex" });

        var context = NewContext(new AIChatSession { SessionId = "s1", ProfileId = "p2", PersonaId = "sales-desk" });

        // Act
        await new AIPersonaNamePrivacyHandler(personaManager.Object, profileManager.Object).CollectingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Sarah", "Alex"], context.NamesToKeep);
    }

    [Fact]
    public async Task AConversationWithoutAPersona_KeepsNoAssistantName()
    {
        // Arrange
        var profileManager = new Mock<IAIProfileManager>();
        profileManager.Setup(value => value.FindByIdAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(new AIProfile { ItemId = "p1" });
        var personaManager = new Mock<IAIPersonaManager>(MockBehavior.Strict);
        var context = NewContext(new AIChatSession { SessionId = "s1", ProfileId = "p1" });

        // Act
        await new AIPersonaNamePrivacyHandler(personaManager.Object, profileManager.Object).CollectingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(context.NamesToKeep);
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("  ", new string[0])]
    [InlineData("Sarah", new[] { "Sarah" })]
    [InlineData("Sarah, Contoso Motors\r\nAlex\n\nsarah", new[] { "Sarah", "Contoso Motors", "Alex" })]
    public void TheNamesToKeep_AreEnteredOnePerLineOrSeparatedByCommas(string entered, string[] expected)
    {
        Assert.Equal(expected, AIProfileDataPrivacyDisplayDriver.SplitNames(entered));
    }

    private static AIConversationPrivacyContext NewContext(AIChatSession session)
        => new(session, AIDataStorageMode.RedactPersonalData);
}
