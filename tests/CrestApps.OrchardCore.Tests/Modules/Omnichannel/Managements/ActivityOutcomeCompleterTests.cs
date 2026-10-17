using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// How a call that ended without an agent -- rung out, screened out as a machine -- is dispositioned, and whether the
/// subject's flow decided what happens next or left it to whoever asked, such as the dialer.
/// </summary>
public sealed class ActivityOutcomeCompleterTests
{
    private const string SubjectType = "LeadGeneration";

    [Fact]
    public async Task CompleteAsync_WhenAMachineAnsweredAndTheFlowWiresNoAnswer_AppliesNoAnswerAndSaysTheFlowHandledIt()
    {
        // Arrange: the subject has no rule for a machine, but its "No answer" disposition is wired to Try again. A call a
        // voicemail answered is no answer to the flow, so the flow's Try again decides when to call again.
        var harness = new Harness(
            [Disposition("no-answer", "No Answer", DispositionOutcome.NoAnswer)],
            [TryAgain("no-answer")]);

        // Act
        var result = await harness.CompleteAsync(DispositionOutcome.AnsweringMachine, preferRetriedDisposition: false);

        // Assert
        Assert.True(result.HandledBySubjectFlow);
        Assert.Equal("no-answer", harness.AppliedDispositionId);
    }

    [Fact]
    public async Task CompleteAsync_WhenTheFlowHasNoRuleForTheOutcome_AppliesTheOutcomesDispositionAndLeavesTheRetryToTheCaller()
    {
        // Arrange: both dispositions exist, but neither is wired to anything for the subject.
        var harness = new Harness(
            [
                Disposition("machine", "Answering Machine", DispositionOutcome.AnsweringMachine),
                Disposition("no-answer", "No Answer", DispositionOutcome.NoAnswer),
            ],
            []);

        // Act
        var result = await harness.CompleteAsync(DispositionOutcome.AnsweringMachine, preferRetriedDisposition: false);

        // Assert: the dialer retries it under its profile's limits, because the flow said nothing.
        Assert.False(result.HandledBySubjectFlow);
        Assert.Equal("machine", harness.AppliedDispositionId);
    }

    [Theory]
    [InlineData(true, "call-later", true)]
    [InlineData(false, "machine", false)]
    public async Task CompleteAsync_BorrowsTheRetriedDisposition_OnlyWhenAsked(bool preferRetriedDisposition, string expectedDispositionId, bool expectedHandled)
    {
        // Arrange: the flow's Try again is wired to a disposition with no outcome, the way flows were built before
        // outcomes existed. The dialer no longer borrows it; other callers still may.
        var harness = new Harness(
            [
                Disposition("machine", "Answering Machine", DispositionOutcome.AnsweringMachine),
                Disposition("call-later", "Call Later", DispositionOutcome.None),
            ],
            [TryAgain("call-later")]);

        // Act
        var result = await harness.CompleteAsync(DispositionOutcome.AnsweringMachine, preferRetriedDisposition);

        // Assert
        Assert.Equal(expectedHandled, result.HandledBySubjectFlow);
        Assert.Equal(expectedDispositionId, harness.AppliedDispositionId);
    }

    private static OmnichannelDisposition Disposition(string id, string name, DispositionOutcome outcome)
        => new()
        {
            ItemId = id,
            Name = name,
            Outcome = outcome,
        };

    private static SubjectAction TryAgain(string dispositionId)
        => new()
        {
            ItemId = $"try-again-{dispositionId}",
            Source = OmnichannelConstants.ActionTypes.TryAgain,
            SubjectContentType = SubjectType,
            DispositionId = dispositionId,
        };

    private sealed class Harness
    {
        private readonly ActivityOutcomeCompleter _completer;

        public Harness(OmnichannelDisposition[] dispositions, SubjectAction[] actions)
        {
            var dispositionManager = new Mock<INamedCatalogManager<OmnichannelDisposition>>();
            dispositionManager
                .Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(dispositions);

            var actionCatalog = new Mock<ISourceCatalog<SubjectAction>>();
            actionCatalog
                .Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(actions);

            var dispositionService = new Mock<IActivityDispositionService>();
            dispositionService
                .Setup(value => value.ApplyAsync(It.IsAny<ActivityDispositionRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ActivityDispositionRequest, CancellationToken>((request, _) => AppliedDispositionId = request.DispositionId)
                .ReturnsAsync((ActivityDispositionRequest request, CancellationToken _) => ActivityDispositionResult.Success(request.Activity));

            _completer = new ActivityOutcomeCompleter(
                dispositionService.Object,
                actionCatalog.Object,
                dispositionManager.Object,
                NullLogger<ActivityOutcomeCompleter>.Instance);
        }

        public string AppliedDispositionId { get; private set; }

        public Task<ActivityDispositionResult> CompleteAsync(DispositionOutcome outcome, bool preferRetriedDisposition)
            => _completer.CompleteAsync(
                new ActivityOutcomeCompletionRequest
                {
                    Activity = new OmnichannelActivity
                    {
                        ItemId = "activity-1",
                        SubjectContentType = SubjectType,
                        Source = ActivitySources.PowerDial,
                        Attempts = 1,
                    },
                    Outcome = outcome,
                    PreferRetriedDisposition = preferRetriedDisposition,
                },
                TestContext.Current.CancellationToken);
    }
}
