using CrestApps.OrchardCore.Attachments;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;
using CrestApps.OrchardCore.Surveys.Core.Controls.Calculated;
using CrestApps.OrchardCore.Surveys.Core.Controls.ChoiceBlanks;
using CrestApps.OrchardCore.Surveys.Core.Controls.ConstantSum;
using CrestApps.OrchardCore.Surveys.Core.Controls.ContactInfo;
using CrestApps.OrchardCore.Surveys.Core.Controls.Date;
using CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;
using CrestApps.OrchardCore.Surveys.Core.Controls.FillInBlanks;
using CrestApps.OrchardCore.Surveys.Core.Controls.Hidden;
using CrestApps.OrchardCore.Surveys.Core.Controls.LongText;
using CrestApps.OrchardCore.Surveys.Core.Controls.Matching;
using CrestApps.OrchardCore.Surveys.Core.Controls.Matrix;
using CrestApps.OrchardCore.Surveys.Core.Controls.MatrixDropdown;
using CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;
using CrestApps.OrchardCore.Surveys.Core.Controls.MultipleText;
using CrestApps.OrchardCore.Surveys.Core.Controls.Number;
using CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;
using CrestApps.OrchardCore.Surveys.Core.Controls.Rating;
using CrestApps.OrchardCore.Surveys.Core.Controls.ShortText;
using CrestApps.OrchardCore.Surveys.Core.Controls.Signature;
using CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;
using CrestApps.OrchardCore.Surveys.Core.Controls.Slider;
using CrestApps.OrchardCore.Surveys.Core.Controls.TextBlock;
using CrestApps.OrchardCore.Surveys.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Core;

/// <summary>
/// Registers the built-in question types of Surveys.
/// </summary>
public static class SurveysControlsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the built-in question types, their scorers, analyzers and presets, and the respondent projector. The
    /// file upload type needs Attachments and is registered by <see cref="AddSurveysFileUploadControl"/>.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysControls(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SurveyRespondentProjector>();

        services.AddSurveyControl<SingleChoiceControlHandler>(SurveyControlNames.SingleChoice, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<SingleChoiceControlHandler>("Single choice");
            descriptor.Description = LocalizationSource.Create<SingleChoiceControlHandler>("Pick one option from a list.");
            descriptor.Category = SurveyControlCategories.Choice;
            descriptor.Icon = "fa-solid fa-circle-dot";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.SupportsOther | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(SingleChoiceSettings);
        });
        services.AddSurveyControlScorer<SingleChoiceScorer>();
        services.AddSurveyControlAnalyzer<SingleChoiceAnalyzer>();

        services.AddSurveyControl<MultipleChoiceControlHandler>(SurveyControlNames.MultipleChoice, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<MultipleChoiceControlHandler>("Multiple choice");
            descriptor.Description = LocalizationSource.Create<MultipleChoiceControlHandler>("Pick several options from a list.");
            descriptor.Category = SurveyControlCategories.Choice;
            descriptor.Icon = "fa-solid fa-square-check";
            descriptor.Position = 20;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.SupportsOther | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(MultipleChoiceSettings);
        });
        services.AddSurveyControlScorer<MultipleChoiceScorer>();
        services.AddSurveyControlAnalyzer<MultipleChoiceAnalyzer>();

        services.AddSurveyControl<BooleanControlHandler>(SurveyControlNames.Boolean, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<BooleanControlHandler>("Yes or no");
            descriptor.Description = LocalizationSource.Create<BooleanControlHandler>("Yes or no, true or false, or a consent box.");
            descriptor.Category = SurveyControlCategories.Choice;
            descriptor.Icon = "fa-solid fa-toggle-on";
            descriptor.Position = 30;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(BooleanSettings);
        });
        services.AddSurveyControlScorer<BooleanScorer>();
        services.AddSurveyControlAnalyzer<BooleanAnalyzer>();

        services.AddSurveyControl<ShortTextControlHandler>(SurveyControlNames.ShortText, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<ShortTextControlHandler>("Short text");
            descriptor.Description = LocalizationSource.Create<ShortTextControlHandler>("A short answer on one line.");
            descriptor.Category = SurveyControlCategories.Text;
            descriptor.Icon = "fa-solid fa-font";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.SupportsValidation | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(ShortTextSettings);
        });
        services.AddSurveyControlScorer<ShortTextScorer>();
        services.AddSurveyControlAnalyzer<ShortTextAnalyzer>();

        services.AddSurveyControl<LongTextControlHandler>(SurveyControlNames.LongText, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<LongTextControlHandler>("Long text");
            descriptor.Description = LocalizationSource.Create<LongTextControlHandler>("A long answer or essay, graded by hand in quizzes.");
            descriptor.Category = SurveyControlCategories.Text;
            descriptor.Icon = "fa-solid fa-align-left";
            descriptor.Position = 20;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ManuallyGraded | SurveyControlCapabilities.SupportsValidation | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(LongTextSettings);
        });
        services.AddSurveyControlScorer<LongTextScorer>();
        services.AddSurveyControlAnalyzer<LongTextAnalyzer>();

        services.AddSurveyControl<MultipleTextControlHandler>(SurveyControlNames.MultipleText, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<MultipleTextControlHandler>("Multiple text");
            descriptor.Description = LocalizationSource.Create<MultipleTextControlHandler>("Several short answers, one per item.");
            descriptor.Category = SurveyControlCategories.Text;
            descriptor.Icon = "fa-solid fa-list";
            descriptor.Position = 30;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesRows;
            descriptor.SettingsType = typeof(MultipleTextSettings);
        });

        services.AddSurveyControl<ContactInfoControlHandler>(SurveyControlNames.ContactInfo, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<ContactInfoControlHandler>("Contact information");
            descriptor.Description = LocalizationSource.Create<ContactInfoControlHandler>("Name, e-mail, phone, company and address.");
            descriptor.Category = SurveyControlCategories.Text;
            descriptor.Icon = "fa-solid fa-address-card";
            descriptor.Position = 40;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable;
            descriptor.SettingsType = typeof(ContactInfoSettings);
        });

        services.AddSurveyControl<NumberControlHandler>(SurveyControlNames.Number, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<NumberControlHandler>("Number");
            descriptor.Description = LocalizationSource.Create<NumberControlHandler>("A number, with an optional range and unit.");
            descriptor.Category = SurveyControlCategories.Number;
            descriptor.Icon = "fa-solid fa-hashtag";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.SupportsValidation | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(NumberSettings);
        });
        services.AddSurveyControlScorer<NumberScorer>();
        services.AddSurveyControlAnalyzer<NumberAnalyzer>();

        services.AddSurveyControl<SliderControlHandler>(SurveyControlNames.Slider, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<SliderControlHandler>("Slider");
            descriptor.Description = LocalizationSource.Create<SliderControlHandler>("A number picked on a slider.");
            descriptor.Category = SurveyControlCategories.Number;
            descriptor.Icon = "fa-solid fa-sliders";
            descriptor.Position = 20;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.Aggregatable | SurveyControlCapabilities.ClientScript;
            descriptor.SettingsType = typeof(SliderSettings);
        });
        services.AddSurveyControlScorer<SliderScorer>();
        services.AddSurveyControlAnalyzer<SliderAnalyzer>();

        services.AddSurveyControl<ConstantSumControlHandler>(SurveyControlNames.ConstantSum, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<ConstantSumControlHandler>("Constant sum");
            descriptor.Description = LocalizationSource.Create<ConstantSumControlHandler>("Numbers for each item that add up to a total.");
            descriptor.Category = SurveyControlCategories.Number;
            descriptor.Icon = "fa-solid fa-calculator";
            descriptor.Position = 30;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesRows | SurveyControlCapabilities.Aggregatable | SurveyControlCapabilities.ClientScript;
            descriptor.SettingsType = typeof(ConstantSumSettings);
        });
        services.AddSurveyControlAnalyzer<ConstantSumAnalyzer>();

        services.AddSurveyControl<DateControlHandler>(SurveyControlNames.Date, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<DateControlHandler>("Date and time");
            descriptor.Description = LocalizationSource.Create<DateControlHandler>("A date, a time or both.");
            descriptor.Category = SurveyControlCategories.Date;
            descriptor.Icon = "fa-solid fa-calendar-days";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ConditionSource;
            descriptor.SettingsType = typeof(DateSettings);
        });

        services.AddSurveyControl<RatingControlHandler>(SurveyControlNames.Rating, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<RatingControlHandler>("Rating");
            descriptor.Description = LocalizationSource.Create<RatingControlHandler>("A rating on a scale, such as stars.");
            descriptor.Category = SurveyControlCategories.Rating;
            descriptor.Icon = "fa-solid fa-star";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(RatingSettings);
        });
        services.AddSurveyControlAnalyzer<RatingAnalyzer>();

        services.AddSurveyControl<MatrixControlHandler>(SurveyControlNames.Matrix, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<MatrixControlHandler>("Matrix");
            descriptor.Description = LocalizationSource.Create<MatrixControlHandler>("A grid of rows rated on the same columns.");
            descriptor.Category = SurveyControlCategories.Matrix;
            descriptor.Icon = "fa-solid fa-table-cells";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.UsesRows | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(MatrixSettings);
        });
        services.AddSurveyControlScorer<MatrixScorer>();
        services.AddSurveyControlAnalyzer<MatrixAnalyzer>();

        services.AddSurveyControl<MatrixDropdownControlHandler>(SurveyControlNames.MatrixDropdown, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<MatrixDropdownControlHandler>("Drop-down matrix");
            descriptor.Description = LocalizationSource.Create<MatrixDropdownControlHandler>("A grid with a drop-down in each cell.");
            descriptor.Category = SurveyControlCategories.Matrix;
            descriptor.Icon = "fa-solid fa-table-list";
            descriptor.Position = 20;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.UsesRows | SurveyControlCapabilities.ConditionSource;
            descriptor.SettingsType = typeof(MatrixDropdownSettings);
        });

        services.AddSurveyControl<RankingControlHandler>(SurveyControlNames.Ranking, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<RankingControlHandler>("Ranking");
            descriptor.Description = LocalizationSource.Create<RankingControlHandler>("Put items in order.");
            descriptor.Category = SurveyControlCategories.Choice;
            descriptor.Icon = "fa-solid fa-arrow-down-short-wide";
            descriptor.Position = 40;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.Aggregatable | SurveyControlCapabilities.ClientScript;
            descriptor.SettingsType = typeof(RankingSettings);
        });
        services.AddSurveyControlScorer<RankingScorer>();
        services.AddSurveyControlAnalyzer<RankingAnalyzer>();

        services.AddSurveyControl<MatchingControlHandler>(SurveyControlNames.Matching, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<MatchingControlHandler>("Matching");
            descriptor.Description = LocalizationSource.Create<MatchingControlHandler>("Match each prompt with an answer.");
            descriptor.Category = SurveyControlCategories.Assessment;
            descriptor.Icon = "fa-solid fa-link";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.UsesRows | SurveyControlCapabilities.Scorable;
            descriptor.SettingsType = typeof(MatchingSettings);
        });
        services.AddSurveyControlScorer<MatchingScorer>();

        services.AddSurveyControl<FillInBlanksControlHandler>(SurveyControlNames.FillInBlanks, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<FillInBlanksControlHandler>("Fill in the blanks");
            descriptor.Description = LocalizationSource.Create<FillInBlanksControlHandler>("Type the missing words of a text.");
            descriptor.Category = SurveyControlCategories.Assessment;
            descriptor.Icon = "fa-solid fa-i-cursor";
            descriptor.Position = 20;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable;
            descriptor.SettingsType = typeof(FillInBlanksSettings);
        });
        services.AddSurveyControlScorer<FillInBlanksScorer>();

        services.AddSurveyControl<ChoiceBlanksControlHandler>(SurveyControlNames.ChoiceBlanks, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<ChoiceBlanksControlHandler>("Choose the missing words");
            descriptor.Description = LocalizationSource.Create<ChoiceBlanksControlHandler>("Pick the missing words of a text from a pool.");
            descriptor.Category = SurveyControlCategories.Assessment;
            descriptor.Icon = "fa-solid fa-puzzle-piece";
            descriptor.Position = 30;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable;
            descriptor.SettingsType = typeof(ChoiceBlanksSettings);
        });
        services.AddSurveyControlScorer<ChoiceBlanksScorer>();

        services.AddSurveyControl<TextBlockControlHandler>(SurveyControlNames.TextBlock, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<TextBlockControlHandler>("Text block");
            descriptor.Description = LocalizationSource.Create<TextBlockControlHandler>("Text and images that need no answer.");
            descriptor.Category = SurveyControlCategories.Content;
            descriptor.Icon = "fa-solid fa-paragraph";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.None;
            descriptor.SettingsType = typeof(TextBlockSettings);
        });

        services.AddSurveyControl<HiddenControlHandler>(SurveyControlNames.Hidden, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<HiddenControlHandler>("Hidden value");
            descriptor.Description = LocalizationSource.Create<HiddenControlHandler>("Records a hidden variable, such as a value from the link.");
            descriptor.Category = SurveyControlCategories.Advanced;
            descriptor.Icon = "fa-solid fa-eye-slash";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(HiddenSettings);
        });
        services.AddSurveyControlAnalyzer<HiddenAnalyzer>();

        services.AddSurveyControl<CalculatedControlHandler>(SurveyControlNames.Calculated, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<CalculatedControlHandler>("Calculated value");
            descriptor.Description = LocalizationSource.Create<CalculatedControlHandler>("A value calculated by a formula.");
            descriptor.Category = SurveyControlCategories.Advanced;
            descriptor.Icon = "fa-solid fa-square-root-variable";
            descriptor.Position = 20;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ConditionSource | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(CalculatedSettings);
        });
        services.AddSurveyControlAnalyzer<CalculatedAnalyzer>();

        services.AddSurveyControl<SignatureControlHandler>(SurveyControlNames.Signature, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<SignatureControlHandler>("Signature");
            descriptor.Description = LocalizationSource.Create<SignatureControlHandler>("A signature drawn with a finger, a pen or the mouse.");
            descriptor.Category = SurveyControlCategories.Advanced;
            descriptor.Icon = "fa-solid fa-signature";
            descriptor.Position = 30;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.StoresFiles | SurveyControlCapabilities.ClientScript;
            descriptor.SettingsType = typeof(SignatureSettings);
        });

        services.AddSurveyControlPreset(SurveyControlNames.Nps, SurveyControlNames.Rating, preset =>
        {
            preset.DisplayName = LocalizationSource.Create<RatingControlHandler>("Net Promoter Score");
            preset.Description = LocalizationSource.Create<RatingControlHandler>("How likely respondents are to recommend you, from 0 to 10.");
            preset.Category = SurveyControlCategories.Rating;
            preset.Icon = "fa-solid fa-gauge-high";
            preset.Position = 20;
            preset.Initialize = question => question.SetSettings(new RatingSettings
            {
                Min = 0,
                Max = 10,
                Style = RatingStyle.Numbers,
                Metric = RatingMetric.Nps,
            });
        });

        services.AddSurveyControlPreset(SurveyControlNames.Csat, SurveyControlNames.Rating, preset =>
        {
            preset.DisplayName = LocalizationSource.Create<RatingControlHandler>("Customer satisfaction");
            preset.Description = LocalizationSource.Create<RatingControlHandler>("How satisfied respondents are, from 1 to 5.");
            preset.Category = SurveyControlCategories.Rating;
            preset.Icon = "fa-solid fa-face-smile";
            preset.Position = 30;
            preset.Initialize = question => question.SetSettings(new RatingSettings
            {
                Min = 1,
                Max = 5,
                Style = RatingStyle.Smileys,
                Metric = RatingMetric.Csat,
            });
        });

        return services;
    }

    /// <summary>
    /// Registers the file upload question type and the access rules of response files. Call it only when Attachments is
    /// enabled.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysFileUploadControl(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSurveyControl<FileUploadControlHandler>(SurveyControlNames.FileUpload, descriptor =>
        {
            descriptor.DisplayName = LocalizationSource.Create<FileUploadControlHandler>("File upload");
            descriptor.Description = LocalizationSource.Create<FileUploadControlHandler>("One or more files, kept with the response.");
            descriptor.Category = SurveyControlCategories.Files;
            descriptor.Icon = "fa-solid fa-file-arrow-up";
            descriptor.Position = 10;
            descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ManuallyGraded | SurveyControlCapabilities.StoresFiles | SurveyControlCapabilities.Aggregatable;
            descriptor.SettingsType = typeof(FileUploadSettings);
        });
        services.AddSurveyControlScorer<FileUploadScorer>();
        services.AddSurveyControlAnalyzer<FileUploadAnalyzer>();
        services.AddAttachmentAccessProvider<SurveyResponseAttachmentAccessProvider>();

        return services;
    }
}
