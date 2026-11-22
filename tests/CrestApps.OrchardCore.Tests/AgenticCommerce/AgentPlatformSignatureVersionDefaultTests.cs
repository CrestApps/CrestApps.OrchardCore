using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Handlers;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// Review 11 (M-acp-v1-default): only the admin editor started a new ACP platform on version 2 signatures. One created by
/// a recipe or the API stayed on version 1, which doesn't sign the timestamp and has no replay guard. A new platform now
/// starts on version 2 unless it names a version; an existing one keeps its own.
/// </summary>
public sealed class AgentPlatformSignatureVersionDefaultTests
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ANewPlatformFromARecipe_WithoutAVersion_SignsVersion2()
    {
        var platform = new AgentPlatform();

        await CreateHandler().InitializingAsync(new InitializingContext<AgentPlatform>(platform, new JsonObject { ["Name"] = "Agent" }), TestContext.Current.CancellationToken);

        Assert.Equal(AcpRequestSignature.BoundVersion, platform.AcpSignatureVersion);
        Assert.Equal(AcpRequestSignature.BoundVersion, AcpSignatureCheck.GetVersion(platform));
    }

    [Fact]
    public async Task ANewPlatformThatNamesVersion1_KeepsIt()
    {
        var platform = new AgentPlatform();

        await CreateHandler().InitializingAsync(new InitializingContext<AgentPlatform>(platform, new JsonObject { ["AcpSignatureVersion"] = 1 }), TestContext.Current.CancellationToken);

        Assert.Equal(AcpRequestSignature.BodyOnlyVersion, platform.AcpSignatureVersion);
    }

    [Fact]
    public async Task AnExistingPlatformWithoutAVersion_IsLeftAsItIs_WhenUpdated()
    {
        var platform = new AgentPlatform { ItemId = "platform-1" };

        await CreateHandler().UpdatingAsync(new UpdatingContext<AgentPlatform>(platform, new JsonObject { ["Name"] = "Agent" }), TestContext.Current.CancellationToken);

        Assert.Null(platform.AcpSignatureVersion);
    }

    private static AgentPlatformHandler CreateHandler()
        => new(new TestClock(_now), new PassThroughStringLocalizer<AgentPlatformHandler>());
}
