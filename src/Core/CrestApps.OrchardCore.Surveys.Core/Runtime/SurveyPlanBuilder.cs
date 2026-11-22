using System.Security.Cryptography;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Draws the plan of a response when it starts: page order, pool draws, question order and choice order, and the
/// screens when pages are split. The plan is stored, so refresh, back and resume show the same.
/// </summary>
public sealed class SurveyPlanBuilder
{
    /// <summary>
    /// The separator between a page id and its screen number in a screen id.
    /// </summary>
    public const char ScreenSeparator = '#';

    private readonly ISurveyControlRegistry _registry;
    private readonly ISurveyPoolCounter _poolCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPlanBuilder"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    /// <param name="poolCounter">The pool counter.</param>
    public SurveyPlanBuilder(ISurveyControlRegistry registry, ISurveyPoolCounter poolCounter)
    {
        _registry = registry;
        _poolCounter = poolCounter;
    }

    /// <summary>
    /// Builds a plan with a new random seed.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="definition">The definition.</param>
    /// <returns>The plan.</returns>
    public SurveyResponsePlan Build(string surveyId, SurveyDefinition definition)
        => Build(surveyId, definition, RandomNumberGenerator.GetInt32(int.MaxValue));

    /// <summary>
    /// Builds a plan from a seed; the same seed gives the same plan.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The plan.</returns>
    public SurveyResponsePlan Build(string surveyId, SurveyDefinition definition, int seed)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var plan = new SurveyResponsePlan { Seed = seed };
        var random = new SurveyRandom(seed);
        var pages = (definition.Pages ?? []).Where(page => !string.IsNullOrEmpty(page?.Id)).ToList();

        if (definition.Presentation?.PageOrder == QuestionOrder.Shuffle && pages.Count > 3)
        {
            // The first and the last page keep their place (an introduction and a closing page).
            var middle = pages.GetRange(1, pages.Count - 2);
            random.Shuffle(middle);
            pages = [pages[0], .. middle, pages[pages.Count - 1]];
        }

        var context = new SurveyPresentationContext(definition, plan, random);
        var perPage = Math.Max(definition.Presentation?.QuestionsPerPage ?? 0, 0);

        foreach (var page in pages)
        {
            var questions = DrawQuestions(surveyId, page, random);

            foreach (var question in questions)
            {
                PrepareChoices(question, context);
            }

            var ids = questions.Select(question => question.Id).ToList();

            if (perPage > 0 && ids.Count > perPage)
            {
                var screen = 0;

                for (var index = 0; index < ids.Count; index += perPage)
                {
                    screen++;
                    var screenId = screen == 1 ? page.Id : page.Id + ScreenSeparator + screen.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    plan.PageOrder.Add(screenId);
                    plan.QuestionOrder[screenId] = ids.Skip(index).Take(perPage).ToList();
                    plan.ScreenPages[screenId] = page.Id;
                }
            }
            else
            {
                plan.PageOrder.Add(page.Id);
                plan.QuestionOrder[page.Id] = ids;
                plan.ScreenPages[page.Id] = page.Id;
            }
        }

        return plan;
    }

    /// <summary>
    /// Gets the questions drawn from pools that present evenly, to record them.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>The question ids.</returns>
    public static IReadOnlyCollection<string> EvenlyPresented(SurveyDefinition definition, SurveyResponsePlan plan)
    {
        var shown = plan.QuestionOrder.Values.SelectMany(ids => ids).ToHashSet(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var page in definition.Pages ?? [])
        {
            var even = (page.Pools ?? []).Where(pool => pool.EvenlyPresent).Select(pool => pool.Id).ToHashSet(StringComparer.Ordinal);

            if (even.Count == 0)
            {
                continue;
            }

            result.AddRange((page.Questions ?? []).Where(question => question.PoolId is not null && even.Contains(question.PoolId) && shown.Contains(question.Id)).Select(question => question.Id));
        }

        return result;
    }

    private List<SurveyQuestion> DrawQuestions(string surveyId, SurveyPage page, SurveyRandom random)
    {
        var designed = (page.Questions ?? []).Where(question => !string.IsNullOrEmpty(question?.Id)).ToList();
        var pools = (page.Pools ?? []).Where(pool => !string.IsNullOrEmpty(pool?.Id)).ToDictionary(pool => pool.Id, StringComparer.Ordinal);
        var drawn = new Dictionary<string, List<SurveyQuestion>>(StringComparer.Ordinal);

        foreach (var pool in pools.Values)
        {
            var candidates = designed.Where(question => string.Equals(question.PoolId, pool.Id, StringComparison.Ordinal)).ToList();
            random.Shuffle(candidates);

            IReadOnlyList<SurveyQuestion> ordered = pool.EvenlyPresent ? _poolCounter.Order(surveyId, pool, candidates) : candidates;
            var take = pool.DrawCount > 0 ? Math.Min(pool.DrawCount, ordered.Count) : ordered.Count;
            var selection = ordered.Take(take).ToList();

            if (!pool.Shuffle)
            {
                // Without shuffling, the drawn questions keep their designed order.
                selection = candidates.Count == 0 ? selection : designed.Where(selection.Contains).ToList();
            }

            drawn[pool.Id] = selection;
        }

        var questions = new List<SurveyQuestion>();
        var placed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var question in designed)
        {
            if (question.PoolId is not null && pools.ContainsKey(question.PoolId))
            {
                // The pool's draw takes the place of its first member.
                if (placed.Add(question.PoolId))
                {
                    questions.AddRange(drawn[question.PoolId]);
                }

                continue;
            }

            questions.Add(question);
        }

        if (page.QuestionOrder == QuestionOrder.Shuffle)
        {
            var free = Enumerable.Range(0, questions.Count).Where(index => !questions[index].Pinned).ToList();
            var movable = free.Select(index => questions[index]).ToList();
            random.Shuffle(movable);

            for (var i = 0; i < free.Count; i++)
            {
                questions[free[i]] = movable[i];
            }
        }

        return questions;
    }

    private void PrepareChoices(SurveyQuestion question, SurveyPresentationContext context)
    {
        var handler = _registry.GetHandler(question.ControlType);

        if (handler is null)
        {
            return;
        }

        handler.PreparePresentation(question, context);

        if (!context.Plan.ChoiceOrder.ContainsKey(question.Id) && SurveyChoiceSource.GetChoices(question, context.Definition).Count > 0)
        {
            // A type whose answer key is an order always starts shuffled, so the designed order is not shown.
            var shuffle = question.ShuffleChoices || _registry.GetScorer(question.ControlType)?.AnswerKeyIsOrder == true;

            context.Plan.ChoiceOrder[question.Id] = SurveyChoiceSource.OrderChoices(SurveyChoiceSource.GetChoices(question, context.Definition), shuffle, context.Random);
        }
    }
}
