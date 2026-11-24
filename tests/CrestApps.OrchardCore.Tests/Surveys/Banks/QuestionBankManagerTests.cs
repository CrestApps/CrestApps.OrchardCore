using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Banks;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Banks;

/// <summary>
/// The question bank manager, queries and change detector on a real SQLite store.
/// </summary>
public sealed class QuestionBankManagerTests : IAsyncLifetime
{
    private BanksTestStore _store;

    public async ValueTask InitializeAsync()
        => _store = await BanksTestStore.CreateAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Theory]
    [InlineData("", "Title", false)]
    [InlineData("Has Spaces", "Title", false)]
    [InlineData("good-name", "", false)]
    [InlineData("good-name-2", "Title", true)]
    public async Task SaveBankAsync_ChecksTheNameAndTitle(string name, string title, bool succeeds)
    {
        // Arrange
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await manager.NewBankAsync(TestContext.Current.CancellationToken);
        bank.Name = name;
        bank.Title = title;

        // Act
        var result = await manager.SaveBankAsync(bank, isNew: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(succeeds, result.Succeeded);
    }

    [Fact]
    public async Task SaveBankAsync_RefusesANameAnotherBankUses()
    {
        // Arrange
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        await CreateBankAsync(manager, "shared");
        var second = await manager.NewBankAsync(TestContext.Current.CancellationToken);
        second.Name = "shared";
        second.Title = "Second";

        // Act
        var result = await manager.SaveBankAsync(second, isNew: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SaveQuestionAsync_IncreasesTheRevisionAndKeepsTheQuestionIdEqualToTheBankQuestionId()
    {
        // Arrange
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "revisions");
        var question = await manager.NewQuestionAsync(bank.ItemId, SurveyControlNames.ShortText, TestContext.Current.CancellationToken);
        question.Question.Title = "Capital of France?";
        question.Question.Id = "something-else";

        // Act
        var first = await manager.SaveQuestionAsync(question, isNew: true, TestContext.Current.CancellationToken);
        question.Question.Title = "Capital city of France?";
        var second = await manager.SaveQuestionAsync(question, isNew: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(2, question.Revision);
        Assert.Equal(question.ItemId, question.Question.Id);
        Assert.False(string.IsNullOrEmpty(question.Question.Name));
    }

    [Fact]
    public async Task SaveQuestionAsync_RefusesAnUnknownTypeAnEmptyTextAndABadDifficulty()
    {
        // Arrange
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "invalid");
        var question = await manager.NewQuestionAsync(bank.ItemId, "Acme.Unknown", TestContext.Current.CancellationToken);
        question.Difficulty = 9;

        // Act
        var result = await manager.SaveQuestionAsync(question, isNew: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Errors.Count);
        Assert.Equal(0, question.Revision);
    }

    [Fact]
    public async Task RetireMoveAndDelete_ChangeOnlyTheQuestionsThatNeedIt()
    {
        // Arrange
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "source");
        var target = await CreateBankAsync(manager, "target");
        var one = await CreateQuestionAsync(manager, bank.ItemId, "One");
        var two = await CreateQuestionAsync(manager, bank.ItemId, "Two");

        // Act
        var retired = await manager.RetireQuestionsAsync([one.ItemId, two.ItemId], retired: true, TestContext.Current.CancellationToken);
        var retiredAgain = await manager.RetireQuestionsAsync([one.ItemId], retired: true, TestContext.Current.CancellationToken);
        var moved = await manager.MoveQuestionsAsync([one.ItemId], target.ItemId, TestContext.Current.CancellationToken);
        var deleted = await manager.DeleteQuestionsAsync([two.ItemId], TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, retired);
        Assert.Equal(0, retiredAgain);
        Assert.Equal(1, moved);
        Assert.Equal(1, deleted);
        Assert.Equal(3, one.Revision);

        var queries = new QuestionBankQueries(session);
        Assert.Equal(1, await queries.CountQuestionsAsync(target.ItemId, TestContext.Current.CancellationToken));
        Assert.Equal(0, await queries.CountQuestionsAsync(bank.ItemId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteBankAsync_DeletesItsQuestions()
    {
        // Arrange
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "doomed");
        await CreateQuestionAsync(manager, bank.ItemId, "One");

        // Act
        var deleted = await manager.DeleteBankAsync(bank.ItemId, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deleted);
        Assert.Equal(0, await new QuestionBankQueries(session).CountQuestionsAsync(bank.ItemId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ImportAsync_UpdatesAQuestionWithAKnownSourceIdentifierInsteadOfAddingACopy()
    {
        // Arrange
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "imports");

        var first = await manager.ImportAsync(bank.ItemId, [Imported("src-1", "Old text"), Imported("src-2", "Other")], TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        var original = await new BankQuestionStore(session).FindBySourceAsync(bank.ItemId, "src-1", TestContext.Current.CancellationToken);
        var originalId = original.ItemId;

        // Act
        var second = await manager.ImportAsync(bank.ItemId, [Imported("src-1", "New text")], TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, first.Created);
        Assert.Equal(1, second.Updated);
        Assert.Equal(0, second.Created);

        var updated = await new BankQuestionStore(session).FindBySourceAsync(bank.ItemId, "src-1", TestContext.Current.CancellationToken);
        Assert.Equal(originalId, updated.ItemId);
        Assert.Equal(originalId, updated.Question.Id);
        Assert.Equal("New text", updated.Question.Title);
        Assert.Equal(2, updated.Revision);
        Assert.Equal(2, await new QuestionBankQueries(session).CountQuestionsAsync(bank.ItemId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListQuestionsAsync_FiltersByTypeTagDifficultyRetiredAndText()
    {
        // Arrange
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "filters");
        var algebra = await CreateQuestionAsync(manager, bank.ItemId, "Solve for x", difficulty: 4, tags: ["algebra", "math"]);
        await CreateQuestionAsync(manager, bank.ItemId, "Name a prime", difficulty: 2, tags: ["math"]);
        var retired = await CreateQuestionAsync(manager, bank.ItemId, "Old question", tags: ["algebra"]);
        await manager.RetireQuestionsAsync([retired.ItemId], retired: true, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        var queries = new QuestionBankQueries(session);

        // Act
        var byTag = await queries.ListQuestionsAsync(new BankQuestionQuery { BankId = bank.ItemId, Tag = "algebra" }, TestContext.Current.CancellationToken);
        var byDifficulty = await queries.ListQuestionsAsync(new BankQuestionQuery { BankId = bank.ItemId, Difficulty = 2 }, TestContext.Current.CancellationToken);
        var retiredOnly = await queries.ListQuestionsAsync(new BankQuestionQuery { BankId = bank.ItemId, Retired = true }, TestContext.Current.CancellationToken);
        var everything = await queries.ListQuestionsAsync(new BankQuestionQuery { BankId = bank.ItemId, Retired = null }, TestContext.Current.CancellationToken);
        var bySearch = await queries.ListQuestionsAsync(new BankQuestionQuery { BankId = bank.ItemId, Search = "prime" }, TestContext.Current.CancellationToken);
        var byType = await queries.ListQuestionsAsync(new BankQuestionQuery { BankId = bank.ItemId, ControlType = SurveyControlNames.Number }, TestContext.Current.CancellationToken);
        var tags = await queries.ListTagsAsync(bank.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(algebra.ItemId, Assert.Single(byTag.Items).ItemId);
        Assert.Equal("Name a prime", Assert.Single(byDifficulty.Items).Question.Title);
        Assert.Equal(retired.ItemId, Assert.Single(retiredOnly.Items).ItemId);
        Assert.Equal(3, everything.Count);
        Assert.Equal("Name a prime", Assert.Single(bySearch.Items).Question.Title);
        Assert.Equal(0, byType.Count);
        Assert.Equal(["algebra", "math"], tags);
    }

    [Fact]
    public async Task ChangeDetector_ReportsEditedAndRetiredBankQuestionsSincePublishing()
    {
        // Arrange
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "detector");
        var edited = await CreateQuestionAsync(manager, bank.ItemId, "Edited");
        var removed = await CreateQuestionAsync(manager, bank.ItemId, "Removed");
        var untouched = await CreateQuestionAsync(manager, bank.ItemId, "Untouched");

        var versions = new BanksVersionCache();
        var survey = new Survey { ItemId = "survey1", PublishedVersionId = "version1", PublishedVersionNumber = 1 };
        versions.Versions["version1"] = new SurveyVersion
        {
            ItemId = "version1",
            SurveyId = survey.ItemId,
            Number = 1,
            Definition = new SurveyDefinition
            {
                Pages =
                [
                    new SurveyPage
                    {
                        Id = "p1",
                        Questions = [Copy(edited), Copy(removed), Copy(untouched), new SurveyQuestion { Id = "inline" }],
                    },
                ],
            },
        };

        var detector = new QuestionBankChangeDetector(versions, new BankQuestionStore(session), session);
        var before = await detector.GetChangeAsync(survey, TestContext.Current.CancellationToken);

        edited.Question.Title = "Edited again";
        await manager.SaveQuestionAsync(edited, isNew: false, TestContext.Current.CancellationToken);
        await manager.RetireQuestionsAsync([removed.ItemId], retired: true, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var after = await detector.GetChangeAsync(survey, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(before);
        Assert.NotNull(after);
        Assert.Equal(1, after.VersionNumber);
        Assert.Equal([edited.ItemId], after.ChangedQuestionIds);
        Assert.Equal([removed.ItemId], after.RemovedQuestionIds);
    }

    [Fact]
    public async Task ImportAsync_AgainKeepsTheChoiceIdsRemapsTheKeyAndKeepsARetiredQuestionRetired()
    {
        // Arrange: a quiz question imported once, then retired by the author.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "quiz");

        await manager.ImportAsync(bank.ItemId, [ImportedChoice("src-q", ("a", "Paris"), ("b", "Lyon"), ("c", "Nice"), correct: "b")], cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var first = await new BankQuestionStore(session).FindBySourceAsync(bank.ItemId, "src-q", cancellationToken);
        var firstIds = first.Question.Choices.ToDictionary(choice => choice.Value, choice => choice.Id);
        await manager.RetireQuestionsAsync([first.ItemId], retired: true, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act: the file is fixed (the key is now "a", "c" is dropped, "d" is new) and imported again. Every choice of the
        // file read again has a new id, as the QTI and spreadsheet readers give.
        var summary = await manager.ImportAsync(bank.ItemId, [ImportedChoice("src-q", ("a", "Paris"), ("b", "Lyon"), ("d", "Lille"), correct: "a")], cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.Equal(1, summary.Updated);

        var updated = await new BankQuestionStore(session).FindBySourceAsync(bank.ItemId, "src-q", cancellationToken);
        var choices = updated.Question.Choices.ToDictionary(choice => choice.Value);
        Assert.Equal(firstIds["a"], choices["a"].Id);
        Assert.Equal(firstIds["b"], choices["b"].Id);
        Assert.DoesNotContain(choices["d"].Id, firstIds.Values);

        // The key follows the kept ids, so a regrade of earlier answers with this key still matches them.
        Assert.Equal([firstIds["a"]], updated.Question.Scoring.CorrectChoiceIds);
        Assert.Equal(firstIds["a"], updated.Question.Scoring.ChoicePoints.Single(pair => pair.Value == 1).Key);

        // The dropped choice stays, retired, so answers given to it still read; the retired question stays retired.
        Assert.Equal(firstIds["c"], choices["c"].Id);
        Assert.True(choices["c"].Retired);
        Assert.True(updated.Retired);
    }

    [Fact]
    public async Task R6M9_ImportAsync_AgainWithTheChoicesReordered_KeepsEachChoicesIdAndTheKey()
    {
        // Arrange: a spreadsheet gives every choice its place as its value. Paris is right, and answers chose Paris.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "capitals");

        await manager.ImportAsync(bank.ItemId, [Positional("src-q", "Paris", "London", "Paris", "Rome")], cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var first = await new BankQuestionStore(session).FindBySourceAsync(bank.ItemId, "src-q", cancellationToken);
        var firstIds = first.Question.Choices.ToDictionary(choice => choice.Text, choice => choice.Id);

        // Act: the author puts Paris first.
        await manager.ImportAsync(bank.ItemId, [Positional("src-q", "Paris", "Paris", "London", "Rome")], cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Assert: every choice keeps its id, so earlier answers still read Paris and the key still scores them.
        var updated = await new BankQuestionStore(session).FindBySourceAsync(bank.ItemId, "src-q", cancellationToken);
        Assert.Equal(["Paris", "London", "Rome"], updated.Question.Choices.Select(choice => choice.Text));
        Assert.All(updated.Question.Choices, choice => Assert.Equal(firstIds[choice.Text], choice.Id));
        Assert.DoesNotContain(updated.Question.Choices, choice => choice.Retired);
        Assert.Equal([firstIds["Paris"]], updated.Question.Scoring.CorrectChoiceIds);
    }

    [Fact]
    public async Task R6M9_ImportAsync_AgainWithADistractorAddedAtTheTop_KeepsTheIdsOfTheOthers()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _store.Store.CreateSession();
        var manager = BanksTestStore.Manager(session);
        var bank = await CreateBankAsync(manager, "capitals-distractor");

        await manager.ImportAsync(bank.ItemId, [Positional("src-q", "Paris", "London", "Paris", "Rome")], cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var first = await new BankQuestionStore(session).FindBySourceAsync(bank.ItemId, "src-q", cancellationToken);
        var firstIds = first.Question.Choices.ToDictionary(choice => choice.Text, choice => choice.Id);

        // Act: a new choice at the top moves every place (and so every value) down by one.
        await manager.ImportAsync(bank.ItemId, [Positional("src-q", "Paris", "Berlin", "London", "Paris", "Rome")], cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Assert
        var updated = await new BankQuestionStore(session).FindBySourceAsync(bank.ItemId, "src-q", cancellationToken);
        var choices = updated.Question.Choices.ToDictionary(choice => choice.Text);
        Assert.Equal(firstIds["London"], choices["London"].Id);
        Assert.Equal(firstIds["Paris"], choices["Paris"].Id);
        Assert.Equal(firstIds["Rome"], choices["Rome"].Id);
        Assert.DoesNotContain(choices["Berlin"].Id, firstIds.Values);
        Assert.DoesNotContain(updated.Question.Choices, choice => choice.Retired);
        Assert.Equal([firstIds["Paris"]], updated.Question.Scoring.CorrectChoiceIds);
    }

    [Fact]
    public void R6M9_KeepIds_MatchesRealValuesWhenTheTextChanged_ButNeverAPlaceNumber()
    {
        // Arrange: QTI identifiers are real; a reworded choice keeps its id by them. Numbered values are only places.
        var previous = Question(("ans_a", "Paris"), ("ans_b", "Lyon"));
        var reworded = Question(("ans_b", "Lyon (Rhone)"), ("ans_a", "Paris"));
        var numbered = Question(("1", "London"), ("2", "Paris"));
        var renumbered = Question(("1", "Madrid"), ("2", "London"), ("3", "Paris"));

        // Act
        SurveyDefinitionCloner.KeepIds(reworded, previous);
        SurveyDefinitionCloner.KeepIds(renumbered, numbered);

        // Assert
        Assert.Equal(previous.Choices[1].Id, reworded.Choices[0].Id);
        Assert.Equal(previous.Choices[0].Id, reworded.Choices[1].Id);
        Assert.Equal(numbered.Choices[0].Id, renumbered.Choices[1].Id);
        Assert.Equal(numbered.Choices[1].Id, renumbered.Choices[2].Id);
        Assert.DoesNotContain(renumbered.Choices[0].Id, numbered.Choices.Select(choice => choice.Id));
    }

    private static SurveyQuestion Question(params (string Value, string Text)[] choices)
        => new()
        {
            ControlType = SurveyControlNames.SingleChoice,
            Title = "Capital?",
            Choices = choices.Select(item => new SurveyChoice { Id = global::OrchardCore.IdGenerator.GenerateId(), Value = item.Value, Text = item.Text }).ToList(),
        };

    // A question as the spreadsheet reader gives it: every choice's value is its place, from 1.
    private static BankQuestion Positional(string source, string correct, params string[] texts)
    {
        var choices = texts
            .Select((text, index) => new SurveyChoice { Id = global::OrchardCore.IdGenerator.GenerateId(), Value = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Text = text })
            .ToList();
        var key = choices.Single(choice => choice.Text == correct).Id;

        return new BankQuestion
        {
            SourceIdentifier = source,
            Question = new SurveyQuestion
            {
                ControlType = SurveyControlNames.SingleChoice,
                Title = "Capital of France?",
                Choices = choices,
                Scoring = new SurveyQuestionScoring { CorrectChoiceIds = [key] },
            },
        };
    }

    private static BankQuestion ImportedChoice(string source, (string Value, string Text) first, (string Value, string Text) second, (string Value, string Text) third, string correct)
    {
        var choices = new[] { first, second, third }
            .Select(item => new SurveyChoice { Id = global::OrchardCore.IdGenerator.GenerateId(), Value = item.Value, Text = item.Text })
            .ToList();
        var key = choices.Single(choice => choice.Value == correct).Id;

        return new BankQuestion
        {
            SourceIdentifier = source,
            Question = new SurveyQuestion
            {
                ControlType = SurveyControlNames.SingleChoice,
                Title = "Capital of France?",
                Choices = choices,
                Scoring = new SurveyQuestionScoring
                {
                    CorrectChoiceIds = [key],
                    ChoicePoints = choices.ToDictionary(choice => choice.Id, choice => choice.Id == key ? 1m : 0m),
                },
            },
        };
    }

    private static SurveyQuestion Copy(BankQuestion question)
    {
        var copy = new SurveyQuestion { Id = question.ItemId, PoolId = "pool1" };
        BankQuestionRevisions.Set(copy, question.Revision);

        return copy;
    }

    private static BankQuestion Imported(string source, string text)
        => new()
        {
            SourceIdentifier = source,
            Question = new SurveyQuestion { ControlType = SurveyControlNames.ShortText, Title = text },
        };

    private static async Task<QuestionBank> CreateBankAsync(QuestionBankManager manager, string name)
    {
        var bank = await manager.NewBankAsync(TestContext.Current.CancellationToken);
        bank.Name = name;
        bank.Title = name;
        Assert.True((await manager.SaveBankAsync(bank, isNew: true, TestContext.Current.CancellationToken)).Succeeded);

        return bank;
    }

    private static async Task<BankQuestion> CreateQuestionAsync(QuestionBankManager manager, string bankId, string text, int difficulty = 3, List<string> tags = null)
    {
        var question = await manager.NewQuestionAsync(bankId, SurveyControlNames.ShortText, TestContext.Current.CancellationToken);
        question.Question.Title = text;
        question.Difficulty = difficulty;
        question.Tags = tags ?? [];
        Assert.True((await manager.SaveQuestionAsync(question, isNew: true, TestContext.Current.CancellationToken)).Succeeded);

        return question;
    }
}
