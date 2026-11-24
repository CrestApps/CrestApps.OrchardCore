---
sidebar_label: Translations
title: Survey Translations
description: "Translated survey texts per culture, how a response picks its culture, the translations page and the translation file."
user_manual:
  - user-manual/surveys/translations
---

A definition keeps translated texts in `Translations`: culture, then text key, then text. The keys (`SurveyTextKeys` in Surveys.Core) are stable:

| Key | Text |
| --- | --- |
| `survey:title`, `survey:introduction` | The survey's title and introduction |
| `page-title:{pageId}`, `page-description:{pageId}` | A page's title and description |
| `title:{questionId}`, `description:{questionId}`, `placeholder:{questionId}` | A question's texts |
| `choice:{questionId}:{choiceId}` | A choice |
| `row:{questionId}:{rowId}` | A row |
| `ending-title:{endingId}`, `ending-message:{endingId}` | An ending |

A response's culture comes from `?culture=` on the link, then the collector's culture, then the request culture; a culture without a translation falls back to its parent (fr-CA uses French), then to the original text. The `culture` query key is never read as a survey variable.

The **Translations** page (`admin/surveys/{id}/translations`) lists the site's cultures, shows the original and the translation side by side, and exports and imports a JSON file with every original text. Translations are part of the draft, so respondents see them after the survey is published. Publishing warns about translations of items that no longer exist. Each translation keeps a fingerprint of the original text it was made from (`SurveyDefinition.TranslationSources`): when the original changes, the page marks the translation **Out of date** and stops counting it as translated, and publishing warns (`outdated-translation`). Saving the same translation again keeps it out of date; changing it marks it current, and so does ticking **Mark current** (`SurveyTranslationEntryViewModel.Confirmed`, passed to `SurveyTranslations.Merge` as the confirmed keys), which confirms it as it is. Line breaks are normalized (CRLF and CR become LF, `SurveyTranslations.Normalize`) before a translation is compared, stored or fingerprinted, so a multi-line text that only went through a browser's text box is not taken as changed; a fingerprint stored before that still matches. A translation saved before fingerprints were kept has nothing to compare with, so it is not taken as current: the page marks it **Check** (`SurveyTranslations.UncheckedKeys`) and publishing warns (`unchecked-translation`), once. Changing it or confirming it records the original it now stands for; saving the page leaves the others as they are. An import leaves out every entry whose `Original` no longer matches the survey's text, and says how many; an entry whose `Original` matches confirms its translation. The page's draft check posts the draft's stamp as ticks (`SurveyDraftStamp`), and `ISurveyManager.SaveDraftAsync` compares it to the tick; every save moves the stamp forward, even within one clock tick. Copying a survey, or creating one from a template, carries every translation over to the new item ids.
