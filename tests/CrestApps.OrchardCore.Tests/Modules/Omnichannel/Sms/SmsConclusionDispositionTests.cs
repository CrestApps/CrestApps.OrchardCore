using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Omnichannel.Voice.Models;
using CrestApps.Core.Omnichannel.Voice.Services;
using CrestApps.Core.Omnichannel.Voice.Tools;
using CrestApps.Core.Omnichannel.Voice;
using CrestApps.Core.Security;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.Core.PhoneNumbers;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;

/// <summary>
/// An automated SMS conversation that has run its course has to close itself out: the activity is completed, the
/// outcome and a summary are written to the customer's record, and whatever the subject's workflow says comes
/// next — the call-back, the follow-up task — is created. The conclusion asks the model which of the outcomes it
/// was offered applies, and a model is free to answer with an outcome that was never on that list, or to end the
/// conversation with a goodbye and name none at all. A conversation that ends that way still has to close, and
/// simply run no outcome-specific work. What it must not do is hand "no outcome" to the subject action executor,
/// which refuses it outright and takes the rest of the conclusion down with it, leaving the conversation open on
/// the customer's record with no outcome, no notes and no follow-up, and nothing to show for it but an exception
/// logged out of a background scope that nobody is watching.
/// </summary>
/// <remarks>
/// The other two places that run subject actions — the automated voice call's conclusion and the agent-facing
/// disposition service — both choose an outcome and then run the actions only when they actually have one. The
/// SMS conclusion is the one that does not, which is what the scan below is about.
/// </remarks>
public sealed class SmsConclusionDispositionTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);

    private const string SubjectContentType = "Subject";

    /// <summary>
    /// The conclusion of an automated SMS conversation.
    /// </summary>
    private const string SmsConclusionFile =
        "src/Modules/CrestApps.OrchardCore.Omnichannel.Sms/Handlers/SmsOmnichannelEventHandler.cs";

    /// <summary>
    /// The places that already run subject actions only when an outcome was chosen. They are the known-positive
    /// control for the scan: whatever it reports about the SMS conclusion, it has to stay quiet about these.
    /// </summary>
    private static readonly string[] _guardedHandOffs =
    [
        "src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/VoiceAgentConversationLoop.Conclusion.cs",
        "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Services/DefaultActivityDispositionService.cs",
    ];

    [Fact]
    public async Task WhenNoDispositionWasChosen_TheExecutorRefusesAndTakesTheConclusionWithIt()
    {
        // Arrange
        // This is what "no outcome" costs. Handing the executor nothing does not quietly skip the follow-up
        // work — it throws, and everything the conclusion had staged but not yet committed (the completed
        // status, the notes, the outcome) goes with it.
        var session = new Mock<ISession>();
        var executor = CreateExecutor(session, CreateFollowUpAction("action-callback", "disposition-callback"));
        var context = CreateContext(disposition: null);

        // Act
        var refusal = await Assert.ThrowsAsync<ArgumentNullException>(
            () => executor.ExecuteAsync(context, TestContext.Current.CancellationToken));

        // Assert
        Assert.EndsWith("Disposition", refusal.ParamName, StringComparison.Ordinal);
        session.Verify(
            x => x.SaveAsync(It.IsAny<object>(), false, OmnichannelConstants.CollectionName, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenADispositionWasChosen_OnlyThatOutcomesFollowUpIsCreated()
    {
        // Arrange
        // The half of the conclusion that has to keep working: the outcome decides which of the subject's
        // actions fire, so a conversation that ended in a promised call-back leaves exactly one follow-up
        // behind — not one per configured action, and not one belonging to an outcome that did not happen.
        var session = new Mock<ISession>();
        var followUps = new List<OmnichannelActivity>();

        SetupSave(session, followUps.Add);

        var executor = CreateExecutor(
            session,
            CreateFollowUpAction("action-callback", "disposition-callback"),
            CreateFollowUpAction("action-not-interested", "disposition-not-interested"));

        var context = CreateContext(new OmnichannelDisposition
        {
            ItemId = "disposition-callback",
        });

        context.ActionPreparationNotes = new Dictionary<string, string>
        {
            ["action-callback"] = "  Customer asked to be called after lunch.  ",
        };

        // Act
        await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var followUp = Assert.Single(followUps);
        Assert.Equal("Customer asked to be called after lunch.", followUp.Instructions);
        Assert.Equal(_now.AddDays(1), followUp.ScheduledUtc);
    }

    [Fact]
    public void TheSmsConclusion_NeverHandsAnUnchosenDispositionToTheSubjectActionExecutor()
    {
        // Arrange
        // The conclusion looks the model's answer up in the list of outcomes it offered and takes whatever comes
        // back — which is nothing when the model named an outcome that was not on the list, when a goodbye ended
        // the conversation without one, or when the subject has no actions configured and so had no list to
        // offer. Only one of those is caught earlier (an empty answer on a subject that requires an outcome);
        // the rest arrive here. Reading the conclusion is the only way to check this: it runs inside a deferred
        // shell-scope callback, behind a live model call, so there is no seam to drive it through.
        var handOffs = DescribeHandOffs(SmsConclusionFile);

        // Act
        var unguarded = FindUnguardedHandOffs(SmsConclusionFile);

        // Assert
        Assert.NotEmpty(handOffs);
        Assert.True(
            unguarded.Count == 0,
            "A concluded SMS conversation must close without running subject actions when no offered disposition " +
            "was chosen. Fall back to one of the offered dispositions the way the voice channel does " +
            "(VoiceCallConclusionPolicy.ChooseDisposition), or skip the executor when there is none:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, unguarded));
    }

    [Fact]
    public void TheOtherChannels_AlreadyRunSubjectActionsOnlyWithADisposition()
    {
        // Arrange
        // The known-positive control. The scan above only reports what it recognizes as a hand-off, so a change
        // that stopped recognizing them would pass everything in silence. These two files do the same hand-off
        // and do guard it, so the scan has to both see them and stay quiet about them.
        var inspected = new List<string>();
        var unguarded = new List<string>();

        // Act
        foreach (var file in _guardedHandOffs)
        {
            inspected.AddRange(DescribeHandOffs(file));
            unguarded.AddRange(FindUnguardedHandOffs(file));
        }

        // Assert
        foreach (var file in _guardedHandOffs)
        {
            Assert.Contains(
                inspected,
                handOff => handOff.StartsWith(Path.GetFileName(file), StringComparison.Ordinal));
        }

        Assert.True(unguarded.Count == 0, string.Join(Environment.NewLine, unguarded));
    }

    /// <summary>
    /// Every place a file builds a subject-action context, as "file:line".
    /// </summary>
    private static List<string> DescribeHandOffs(string relativePath)
    {
        var file = Path.Combine(FindRepositoryRoot(), relativePath);
        var name = Path.GetFileName(file);

        return FindHandOffs(ParseFile(file))
            .Select(creation => $"{name}:{LineOf(creation)}")
            .ToList();
    }

    /// <summary>
    /// Reports every hand-off to the subject action executor that could be carrying no disposition.
    /// </summary>
    private static List<string> FindUnguardedHandOffs(string relativePath)
    {
        var file = Path.Combine(FindRepositoryRoot(), relativePath);
        var name = Path.GetFileName(file);
        var root = ParseFile(file);
        var findings = new List<string>();

        foreach (var creation in FindHandOffs(root))
        {
            var chosen = DispositionExpression(creation);

            if (chosen is null)
            {
                findings.Add($"{name}:{LineOf(creation)} runs subject actions without naming a disposition at all.");

                continue;
            }

            if (chosen is not IdentifierNameSyntax local)
            {
                // Anything that is not a plain local is past what reading the syntax can decide, so it is left
                // to the behavior tests rather than reported as a violation.
                continue;
            }

            var identifier = local.Identifier.ValueText;

            if (HasFallback(root, creation, identifier) || IsNullChecked(creation, identifier))
            {
                continue;
            }

            findings.Add(
                $"{name}:{LineOf(creation)} hands '{identifier}' to the subject action executor, and nothing " +
                "between choosing it and this call rules out that it is null.");
        }

        return findings;
    }

    private static IEnumerable<ObjectCreationExpressionSyntax> FindHandOffs(SyntaxNode root)
        => root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => string.Equals(
                TypeNameOf(creation.Type),
                nameof(SubjectActionExecutionContext),
                StringComparison.Ordinal));

    private static string TypeNameOf(TypeSyntax type)
        => type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null,
        };

    /// <summary>
    /// The expression a hand-off assigns to the context's disposition, or <see langword="null"/> when it assigns
    /// none at all.
    /// </summary>
    private static ExpressionSyntax DispositionExpression(ObjectCreationExpressionSyntax creation)
        => creation.Initializer?.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Left is IdentifierNameSyntax member && string.Equals(
                member.Identifier.ValueText,
                nameof(SubjectActionExecutionContext.Disposition),
                StringComparison.Ordinal))
            .Select(assignment => assignment.Right)
            .FirstOrDefault();

    /// <summary>
    /// Determines whether the hand-off is out of reach when the chosen disposition is null, either because it
    /// sits inside a null check or because an earlier check already left.
    /// </summary>
    private static bool IsNullChecked(SyntaxNode creation, string identifier)
    {
        foreach (var enclosing in creation.Ancestors().OfType<IfStatementSyntax>())
        {
            if (enclosing.Statement.Span.Contains(creation.Span) &&
                TestsNotNull(enclosing.Condition, identifier))
            {
                return true;
            }
        }

        for (var block = creation.FirstAncestorOrSelf<BlockSyntax>();
            block is not null;
            block = block.Parent?.FirstAncestorOrSelf<BlockSyntax>())
        {
            foreach (var statement in block.Statements)
            {
                if (statement.SpanStart > creation.SpanStart)
                {
                    break;
                }

                if (statement is IfStatementSyntax guard &&
                    TestsIsNull(guard.Condition, identifier) &&
                    LeavesEarly(guard.Statement))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the local was given a value that cannot be null, such as a fallback to another choice.
    /// </summary>
    private static bool HasFallback(SyntaxNode root, SyntaxNode creation, string identifier)
    {
        SyntaxNode scope = creation.FirstAncestorOrSelf<MemberDeclarationSyntax>() ?? root;

        var chosen = scope.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .LastOrDefault(declarator =>
                string.Equals(declarator.Identifier.ValueText, identifier, StringComparison.Ordinal) &&
                declarator.SpanStart < creation.SpanStart);

        var value = chosen?.Initializer?.Value;

        if (value is null)
        {
            return false;
        }

        return value is ObjectCreationExpressionSyntax ||
            value.DescendantNodesAndSelf().Any(node => node.IsKind(SyntaxKind.CoalesceExpression));
    }

    private static bool LeavesEarly(StatementSyntax body)
        => body.DescendantNodesAndSelf().Any(node => node is ReturnStatementSyntax or ThrowStatementSyntax);

    private static bool TestsNotNull(ExpressionSyntax condition, string identifier)
        => NullTests(condition, identifier).Contains(true);

    private static bool TestsIsNull(ExpressionSyntax condition, string identifier)
        => NullTests(condition, identifier).Contains(false);

    /// <summary>
    /// Every null test a condition makes about one local, as <see langword="true"/> for "is not null" and
    /// <see langword="false"/> for "is null".
    /// </summary>
    private static IEnumerable<bool> NullTests(ExpressionSyntax condition, string identifier)
    {
        foreach (var node in condition.DescendantNodesAndSelf())
        {
            if (node is IsPatternExpressionSyntax match && IsNamed(match.Expression, identifier))
            {
                if (match.Pattern is UnaryPatternSyntax negated &&
                    negated.Pattern is ConstantPatternSyntax negatedConstant &&
                    IsNullLiteral(negatedConstant.Expression))
                {
                    yield return true;
                }
                else if (match.Pattern is ConstantPatternSyntax constant && IsNullLiteral(constant.Expression))
                {
                    yield return false;
                }
            }
            else if (node is BinaryExpressionSyntax comparison &&
                (comparison.IsKind(SyntaxKind.NotEqualsExpression) || comparison.IsKind(SyntaxKind.EqualsExpression)))
            {
                var comparesToNull =
                    (IsNamed(comparison.Left, identifier) && IsNullLiteral(comparison.Right)) ||
                    (IsNamed(comparison.Right, identifier) && IsNullLiteral(comparison.Left));

                if (comparesToNull)
                {
                    yield return comparison.IsKind(SyntaxKind.NotEqualsExpression);
                }
            }
        }
    }

    private static bool IsNamed(ExpressionSyntax expression, string identifier)
        => expression is IdentifierNameSyntax name &&
            string.Equals(name.Identifier.ValueText, identifier, StringComparison.Ordinal);

    private static bool IsNullLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NullLiteralExpression);

    private static SyntaxNode ParseFile(string file)
        => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

    private static int LineOf(SyntaxNode node)
        => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }

    private static DefaultSubjectActionExecutor CreateExecutor(Mock<ISession> session, params SubjectAction[] actions)
    {
        var actionCatalog = new Mock<ISourceCatalog<SubjectAction>>();
        actionCatalog
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions);

        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync(It.IsAny<string>()))
            .ReturnsAsync((string contentType) => new ContentItem { ContentType = contentType });

        var clock = new FakeTimeProvider();
        clock.SetUtcNow(_now);

        var localClock = new Mock<ILocalClock>();
        localClock
            .Setup(x => x.ConvertToUtcAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(_now);

        return new DefaultSubjectActionExecutor(
            actionCatalog.Object,
            Mock.Of<ISubjectFlowSettingsService>(),
            contentManager.Object,
            new ContentItemOmnichannelContactWriter(contentManager.Object, Mock.Of<IPhoneNumberService>(), clock),
            session.Object,
            new FakeUserDirectory(),
            clock,
            localClock.Object,
            NullLogger<DefaultSubjectActionExecutor>.Instance);
    }

    private static SubjectAction CreateFollowUpAction(string actionId, string dispositionId)
    {
        var action = new SubjectAction
        {
            ItemId = actionId,
            Source = OmnichannelConstants.ActionTypes.TryAgain,
            SubjectContentType = SubjectContentType,
            DispositionId = dispositionId,
        };

        action.Put(new TryAgainActionMetadata
        {
            AssignmentType = SubjectActionOwnerAssignmentType.SameOwner,
        });

        return action;
    }

    private static SubjectActionExecutionContext CreateContext(OmnichannelDisposition disposition)
        => new()
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-id",
                SubjectContentType = SubjectContentType,
                ContactResolutionStatus = ContactResolutionStatus.Resolved,
                CompletedById = "completing-user-id",
                CompletedByUsername = "Completing User",
                Attempts = 1,
            },
            Disposition = disposition,
        };

    private static void SetupSave(Mock<ISession> session, Action<OmnichannelActivity> callback)
    {
        session
            .Setup(x => x.SaveAsync(It.IsAny<object>(), false, OmnichannelConstants.CollectionName, It.IsAny<CancellationToken>()))
            .Callback<object, bool, string, CancellationToken>((entity, _, _, _) => callback((OmnichannelActivity)entity))
            .Returns(Task.CompletedTask);
    }
}
