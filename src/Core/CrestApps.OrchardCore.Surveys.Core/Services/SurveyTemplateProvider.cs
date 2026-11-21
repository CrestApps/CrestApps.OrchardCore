using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// The built-in survey templates: blank, customer satisfaction, Net Promoter Score, employee engagement, event feedback,
/// course feedback, quiz and poll. Questions are prepared by their question type's handler or preset, so a template
/// never depends on a question type's settings.
/// </summary>
public sealed class SurveyTemplateProvider : ISurveyTemplateProvider
{
    /// <summary>
    /// The blank template.
    /// </summary>
    public const string Blank = "blank";

    /// <summary>
    /// The customer satisfaction template.
    /// </summary>
    public const string CustomerSatisfaction = "customer-satisfaction";

    /// <summary>
    /// The Net Promoter Score template.
    /// </summary>
    public const string NetPromoterScore = "nps";

    /// <summary>
    /// The employee engagement template.
    /// </summary>
    public const string EmployeeEngagement = "employee-engagement";

    /// <summary>
    /// The event feedback template.
    /// </summary>
    public const string EventFeedback = "event-feedback";

    /// <summary>
    /// The course feedback template.
    /// </summary>
    public const string CourseFeedback = "course-feedback";

    /// <summary>
    /// The quiz template.
    /// </summary>
    public const string Quiz = "quiz";

    /// <summary>
    /// The poll template.
    /// </summary>
    public const string Poll = "poll";

    private readonly SurveyDraftEditor _draftEditor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplateProvider"/> class.
    /// </summary>
    /// <param name="draftEditor">The draft editor that prepares new questions.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyTemplateProvider(
        SurveyDraftEditor draftEditor,
        IStringLocalizer<SurveyTemplateProvider> stringLocalizer)
    {
        _draftEditor = draftEditor;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public IEnumerable<SurveyTemplateDescriptor> GetTemplates()
        =>
        [
            new() { Name = Blank, DisplayName = LocalizationSource.Create<SurveyTemplateProvider>("Blank survey"), Description = LocalizationSource.Create<SurveyTemplateProvider>("Start from an empty page."), Icon = "fa-regular fa-file", Position = 0 },
            new() { Name = CustomerSatisfaction, DisplayName = LocalizationSource.Create<SurveyTemplateProvider>("Customer satisfaction"), Description = LocalizationSource.Create<SurveyTemplateProvider>("Measure how satisfied customers are with a CSAT question and follow-ups."), Icon = "fa-regular fa-face-smile", Position = 10 },
            new() { Name = NetPromoterScore, DisplayName = LocalizationSource.Create<SurveyTemplateProvider>("Net Promoter Score"), Description = LocalizationSource.Create<SurveyTemplateProvider>("Ask how likely people are to recommend you, and why."), Icon = "fa-solid fa-gauge", Position = 20 },
            new() { Name = EmployeeEngagement, DisplayName = LocalizationSource.Create<SurveyTemplateProvider>("Employee engagement"), Description = LocalizationSource.Create<SurveyTemplateProvider>("A grid of agreement statements about work and the team."), Icon = "fa-solid fa-people-group", Position = 30 },
            new() { Name = EventFeedback, DisplayName = LocalizationSource.Create<SurveyTemplateProvider>("Event feedback"), Description = LocalizationSource.Create<SurveyTemplateProvider>("Rate an event and say what to keep and change."), Icon = "fa-regular fa-calendar-check", Position = 40 },
            new() { Name = CourseFeedback, DisplayName = LocalizationSource.Create<SurveyTemplateProvider>("Course feedback"), Description = LocalizationSource.Create<SurveyTemplateProvider>("Rate a course, its content and its instructor."), Icon = "fa-solid fa-graduation-cap", Position = 50 },
            new() { Name = Quiz, Kind = SurveyKinds.Quiz, DisplayName = LocalizationSource.Create<SurveyTemplateProvider>("Quiz"), Description = LocalizationSource.Create<SurveyTemplateProvider>("A scored quiz with a pass mark and right answers."), Icon = "fa-solid fa-list-check", Position = 60 },
            new() { Name = Poll, Kind = SurveyKinds.Poll, DisplayName = LocalizationSource.Create<SurveyTemplateProvider>("Poll"), Description = LocalizationSource.Create<SurveyTemplateProvider>("One question whose totals respondents see after they answer."), Icon = "fa-solid fa-square-poll-vertical", Position = 70 },
        ];

    /// <inheritdoc />
    public SurveyDefinition Build(string name)
        => name switch
        {
            Blank => BuildBlank(),
            CustomerSatisfaction => BuildCustomerSatisfaction(),
            NetPromoterScore => BuildNetPromoterScore(),
            EmployeeEngagement => BuildEmployeeEngagement(),
            EventFeedback => BuildEventFeedback(),
            CourseFeedback => BuildCourseFeedback(),
            Quiz => BuildQuiz(),
            Poll => BuildPoll(),
            _ => null,
        };

    private SurveyDefinition BuildBlank()
        => Start(S["Untitled survey"]);

    private SurveyDefinition BuildCustomerSatisfaction()
    {
        var definition = Start(S["Customer satisfaction"]);
        var page = definition.Pages[0];

        Add(definition, page, null, SurveyControlNames.Csat, S["How satisfied are you with our service?"], required: true);
        Add(definition, page, SurveyControlNames.SingleChoice, null, S["How likely are you to use our service again?"], choices: [S["Very likely"], S["Likely"], S["Not sure"], S["Unlikely"], S["Very unlikely"]]);
        Add(definition, page, SurveyControlNames.LongText, null, S["What could we do better?"]);

        return definition;
    }

    private SurveyDefinition BuildNetPromoterScore()
    {
        var definition = Start(S["Net Promoter Score"]);
        var page = definition.Pages[0];

        Add(definition, page, null, SurveyControlNames.Nps, S["How likely are you to recommend us to a friend or colleague?"], required: true);
        Add(definition, page, SurveyControlNames.LongText, null, S["What is the main reason for your score?"]);

        return definition;
    }

    private SurveyDefinition BuildEmployeeEngagement()
    {
        var definition = Start(S["Employee engagement"]);
        var page = definition.Pages[0];
        var matrix = Add(
            definition,
            page,
            SurveyControlNames.Matrix,
            null,
            S["How much do you agree with each statement?"],
            choices: [S["Strongly disagree"], S["Disagree"], S["Neutral"], S["Agree"], S["Strongly agree"]]);

        if (matrix is not null)
        {
            matrix.Rows.Clear();
            AddRow(matrix, "proud", S["I am proud to work here."]);
            AddRow(matrix, "growth", S["I have opportunities to learn and grow."]);
            AddRow(matrix, "recognition", S["I receive recognition for good work."]);
            AddRow(matrix, "manager", S["My manager supports me."]);
        }

        Add(definition, page, SurveyControlNames.LongText, null, S["What one change would make this a better place to work?"]);

        return definition;
    }

    private SurveyDefinition BuildEventFeedback()
    {
        var definition = Start(S["Event feedback"]);
        var page = definition.Pages[0];

        Add(definition, page, SurveyControlNames.Rating, null, S["How would you rate the event overall?"], required: true);
        Add(definition, page, SurveyControlNames.MultipleChoice, null, S["What did you enjoy?"], choices: [S["Speakers"], S["Content"], S["Networking"], S["Venue"], S["Organization"]]);
        Add(definition, page, SurveyControlNames.LongText, null, S["What should we change next time?"]);

        return definition;
    }

    private SurveyDefinition BuildCourseFeedback()
    {
        var definition = Start(S["Course feedback"]);
        var page = definition.Pages[0];

        Add(definition, page, SurveyControlNames.Rating, null, S["How would you rate the course overall?"], required: true);
        Add(definition, page, SurveyControlNames.Rating, null, S["How clear was the content?"]);
        Add(definition, page, SurveyControlNames.Rating, null, S["How helpful was the instructor?"]);
        Add(definition, page, SurveyControlNames.LongText, null, S["What would you improve?"]);

        return definition;
    }

    private SurveyDefinition BuildQuiz()
    {
        var definition = Start(S["Quiz"]);
        var page = definition.Pages[0];

        definition.Scoring.Enabled = true;
        definition.Scoring.PassMarkPercent = 70;

        var single = Add(definition, page, SurveyControlNames.SingleChoice, null, S["Which planet is known as the red planet?"], required: true, choices: [S["Venus"], S["Mars"], S["Jupiter"], S["Saturn"]]);

        if (single is not null && single.Choices.Count > 1)
        {
            single.Scoring.CorrectChoiceIds = [single.Choices[1].Id];
        }

        var multiple = Add(definition, page, SurveyControlNames.MultipleChoice, null, S["Which of these are prime numbers?"], required: true, choices: ["2", "4", "7", "9"]);

        if (multiple is not null && multiple.Choices.Count > 2)
        {
            multiple.Scoring.CorrectChoiceIds = [multiple.Choices[0].Id, multiple.Choices[2].Id];
            multiple.Scoring.PartialCredit = true;
        }

        return definition;
    }

    private SurveyDefinition BuildPoll()
    {
        var definition = Start(S["Poll"]);
        var page = definition.Pages[0];

        definition.Review.ResultsVisibility = ResultsVisibility.AfterAnswer;
        Add(definition, page, SurveyControlNames.SingleChoice, null, S["What should we work on next?"], required: true, choices: [S["Option A"], S["Option B"], S["Option C"]]);

        return definition;
    }

    private SurveyDefinition Start(string title)
    {
        var definition = new SurveyDefinition { Title = title };
        definition.Pages.Add(SurveyDefinitions.NewPage(1));
        definition.Endings.Add(SurveyDefinitions.NewDefaultEnding(S["<p>Thank you for your response.</p>"]));

        return definition;
    }

    private SurveyQuestion Add(SurveyDefinition definition, SurveyPage page, string controlType, string preset, string title, bool required = false, IEnumerable<string> choices = null)
    {
        var question = _draftEditor.NewQuestion(definition, controlType, preset);

        if (question is null)
        {
            return null;
        }

        question.Title = title;
        question.Required = required;

        if (choices is not null)
        {
            question.Choices.Clear();
            var position = 1;

            foreach (var text in choices)
            {
                question.Choices.Add(new SurveyChoice
                {
                    Id = IdGenerator.GenerateId(),
                    Value = position.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Text = text,
                });

                position++;
            }
        }

        page.Questions.Add(question);

        return question;
    }

    private static void AddRow(SurveyQuestion question, string name, string text)
        => question.Rows.Add(new SurveySubQuestion
        {
            Id = IdGenerator.GenerateId(),
            Name = name,
            Text = text,
        });
}
