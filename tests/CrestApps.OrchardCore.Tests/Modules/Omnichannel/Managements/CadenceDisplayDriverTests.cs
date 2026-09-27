using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// The cadence editor was split into a General card and a Follow-up steps card that post one form, confirmed live. These
/// pin how the driver stores the steps from that post.
/// </summary>
public sealed class CadenceDisplayDriverTests
{
    // A row with no delay is one the user added and never filled in, and a follow-up with no wait, or a negative one, is
    // not a follow-up.
    [Fact]
    public async Task UpdateAsync_DropsStepsWithNoPositiveDelay_TrimsTheirMessages_AndKeepsTheirOrder()
    {
        // Arrange
        var cadence = new Cadence();
        var model = new CadenceViewModel
        {
            DisplayText = "  Renewal follow-up ",
            Enabled = true,
            Steps =
            [
                new CadenceStep { DelayMinutes = 1440, Message = "  Just checking in about your renewal.  " },
                new CadenceStep { DelayMinutes = 0, Message = "Blank row" },
                null,
                new CadenceStep { DelayMinutes = 60, IsAiGenerated = true, Message = null },
                new CadenceStep { DelayMinutes = -5, Message = "Negative" },
                new CadenceStep { DelayMinutes = 4320, Message = "Last reminder.\n" },
            ],
        };

        // Act
        await new CadenceDisplayDriver().UpdateAsync(cadence, PostedFormUpdateModel.CreateContext(model));

        // Assert
        Assert.Equal("Renewal follow-up", cadence.DisplayText);

        // Kept in the order they were entered, not sorted by delay.
        Assert.Equal([1440, 60, 4320], cadence.Steps.Select(step => step.DelayMinutes));
        Assert.Equal(["Just checking in about your renewal.", null, "Last reminder."], cadence.Steps.Select(step => step.Message));
        Assert.Equal([false, true, false], cadence.Steps.Select(step => step.IsAiGenerated));
    }

    [Fact]
    public async Task UpdateAsync_WithNoSteps_StoresNone()
    {
        // Arrange
        var cadence = new Cadence
        {
            Steps = [new CadenceStep { DelayMinutes = 30, Message = "Old step" }],
        };
        var model = new CadenceViewModel { DisplayText = "Empty", Steps = null };

        // Act
        await new CadenceDisplayDriver().UpdateAsync(cadence, PostedFormUpdateModel.CreateContext(model));

        // Assert
        Assert.Empty(cadence.Steps);
    }
}
