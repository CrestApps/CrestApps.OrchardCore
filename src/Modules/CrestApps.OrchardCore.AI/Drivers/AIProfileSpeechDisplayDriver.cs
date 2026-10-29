using System.Globalization;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Edits how a profile sounds and listens on calls: speaking rate, emotion, pronunciations, a fallback voice, boosted
/// keywords, recognition languages, interruption sensitivity, backchanneling, and the languages it may speak.
/// </summary>
public sealed class AIProfileSpeechDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIDeploymentCapabilityService _capabilityService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileSpeechDisplayDriver"/> class.
    /// </summary>
    /// <param name="capabilityService">Lists the text-to-speech deployments.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileSpeechDisplayDriver(IAIDeploymentCapabilityService capabilityService, IStringLocalizer<AIProfileSpeechDisplayDriver> stringLocalizer)
    {
        _capabilityService = capabilityService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<AIProfileSpeechViewModel>("AIProfileSpeech_Edit", async model =>
        {
            var speech = profile.GetSettings<AIProfileSpeechSettings>();
            var languages = profile.GetSettings<AIProfileLanguageSettings>();

            model.SpeakingRate = speech.SpeakingRate;
            model.SpeakingEmotion = speech.SpeakingEmotion;
            model.Pronunciations = FormatPronunciations(speech.PronunciationEntries);
            model.FallbackTextToSpeechDeploymentName = speech.FallbackTextToSpeechDeploymentName;
            model.BoostedKeywords = string.Join(Environment.NewLine, speech.BoostedKeywords ?? []);
            model.RecognitionLanguages = string.Join(", ", speech.RecognitionLanguages ?? []);
            model.InterruptionSensitivity = speech.InterruptionSensitivity;
            model.EnableBackchanneling = speech.EnableBackchanneling;
            model.BackchannelPhrases = string.Join(Environment.NewLine, speech.BackchannelPhrases ?? []);
            model.AllowedLanguages = string.Join(", ", languages.AllowedLanguages ?? []);
            model.StartingLanguage = languages.StartingLanguage;
            model.SpeechDeployments = (await _capabilityService.GetDeploymentsWithFeatureAsync(AIDeploymentFeatureNames.TextToSpeech))
                .Select(deployment => new SelectListItem(deployment.Name, deployment.Name, deployment.Name == speech.FallbackTextToSpeechDeploymentName))
                .ToList();
        }).Location("Content:1.8%Voice & Speech;3");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AIProfileSpeechViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.SpeakingRate is < 0.5 or > 2)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SpeakingRate), S["The speaking rate is from 0.5 to 2."]);
        }

        if (model.InterruptionSensitivity is < 0 or > 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.InterruptionSensitivity), S["Interruption sensitivity is from 0 to 1."]);
        }

        profile.AlterSettings<AIProfileSpeechSettings>(speech =>
        {
            speech.SpeakingRate = model.SpeakingRate;
            speech.SpeakingEmotion = string.IsNullOrWhiteSpace(model.SpeakingEmotion) ? null : model.SpeakingEmotion.Trim();
            speech.PronunciationEntries = ParsePronunciations(model.Pronunciations);
            speech.FallbackTextToSpeechDeploymentName = string.IsNullOrWhiteSpace(model.FallbackTextToSpeechDeploymentName) ? null : model.FallbackTextToSpeechDeploymentName;
            speech.BoostedKeywords = Split(model.BoostedKeywords, '\n');
            speech.RecognitionLanguages = Split(model.RecognitionLanguages, ',');
            speech.InterruptionSensitivity = model.InterruptionSensitivity;
            speech.EnableBackchanneling = model.EnableBackchanneling;
            speech.BackchannelPhrases = Split(model.BackchannelPhrases, '\n');
        });

        profile.AlterSettings<AIProfileLanguageSettings>(languages =>
        {
            languages.AllowedLanguages = Split(model.AllowedLanguages, ',');
            languages.StartingLanguage = string.IsNullOrWhiteSpace(model.StartingLanguage) ? null : model.StartingLanguage.Trim();
        });

        return Edit(profile, context);
    }

    /// <summary>
    /// Reads <c>word = how to say it</c> lines; a line that starts with <c>!</c> matches case.
    /// </summary>
    /// <param name="text">The lines.</param>
    /// <returns>The pronunciations.</returns>
    internal static List<PronunciationEntry> ParsePronunciations(string text)
    {
        var entries = new List<PronunciationEntry>();

        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            var separator = line.IndexOf('=');

            if (separator <= 0)
            {
                continue;
            }

            var word = line[..separator].Trim();
            var alias = line[(separator + 1)..].Trim();
            var matchCase = word.StartsWith('!');

            if (matchCase)
            {
                word = word[1..].Trim();
            }

            if (word.Length > 0 && alias.Length > 0)
            {
                entries.Add(new PronunciationEntry { Text = word, Alias = alias, MatchCase = matchCase });
            }
        }

        return entries;
    }

    /// <summary>
    /// Writes pronunciations as lines.
    /// </summary>
    /// <param name="entries">The pronunciations.</param>
    /// <returns>The lines.</returns>
    internal static string FormatPronunciations(IEnumerable<PronunciationEntry> entries)
        => string.Join(Environment.NewLine, (entries ?? []).Select(entry => $"{(entry.MatchCase ? "!" : string.Empty)}{entry.Text} = {entry.Alias}"));

    private static List<string> Split(string text, char separator)
        => (text ?? string.Empty)
            .Split([separator, '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
