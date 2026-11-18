---
sidebar_label: Feedback
title: Knowledge Base Feedback
description: "How readers' \"Was this helpful?\" answers are stored, the Knowledge Base Feedback report, the feedback data set for Data Pipelines and the Report Builder, its permission and personal data."
user_manual: user-manual/knowledge-base/feedback
---

Each answer to **Was this helpful?** is kept as a dated record, and the article's counters still sort the help center.

## Data model

`KnowledgeArticleFeedback` documents live in the Knowledge Base's own YesSql collection, `KnowledgeBase` (`KnowledgeBaseConstants.CollectionName`, registered through `StoreCollectionOptions`).

| Property | Holds |
| --- | --- |
| `ItemId` | The answer's id |
| `ContentItemId`, `ContentItemVersionId` | The article and the version the reader read |
| `Title`, `CategoryId` | The article's title and category term when the reader answered |
| `Helpful` | Whether it helped |
| `Comment` | What was missing, after a No (at most 1,000 characters) |
| `CreatedUtc` | When the reader answered |
| `UserId`, `UserName` | The signed-in reader, empty for a visitor |

`KnowledgeArticleFeedbackIndex` (in the same collection, migration step 6 to 7) holds `ItemId`, `ContentItemId`, `CategoryId`, `Helpful`, `HasComment`, `CreatedUtc` and `UserId`, with indexes on the date, the article and date, the id and the reader. It declares `ExclusiveToDocumentType()` and `SingleRecordPerDocument()`.

## Answering

`HelpCenterController.Feedback` (`POST help/articles/{id}/feedback`) checks the reader may view the published article, then, under a distributed lock on the article, `KnowledgeArticleFeedbackService.RecordAsync` saves the record and adds one to `HelpfulCount` or `NotHelpfulCount`. A cookie per article keeps the answer's id, so a browser answers once. After a No, the page asks what was missing; `POST help/articles/{id}/feedback/comment` (`AddCommentAsync`) keeps the comment only on that browser's own No, only once. Both posts share the `knowledge-base-feedback` rate-limit group: 20 answers in 10 minutes per visitor address when Orchard Core's Rate Limits feature is on.

## Report

With the Reports feature on, `KnowledgeBaseFeedbackReport` (`knowledge-base-feedback`, **Knowledge Base Feedback**, under **General**) reads the answers of the period, the last 30 days by default:

| Section | Holds |
| --- | --- |
| **Summary** | Answers, helpful, not helpful, the helpful rate and comments |
| **By article** | Helpful, not helpful, rate and comments per article, the least helpful first |
| **By category** | The same per category, by the category's name with those above it |
| **What was missing** | The newest 200 comments, with the time in the reader's time zone (`ILocalClock`), the article and the reader |

It needs **View knowledge base feedback**, is listed under **Reports** with the other reports, and exports through the Reports framework (CSV, Excel and PDF when those features are on).

## Data set

With Data Pipelines on, the **Knowledge Base** data source (`KnowledgeBase`) has the **Knowledge article feedback** data set (`KnowledgeArticleFeedback`): one row per answer, with the answer id, the article id (referencing the Knowledge Article content item), title, category id, helpful, what was missing, when, and the reader (referencing the user). Its date is `CreatedUtc`, and the id, article, category and reader are key columns of the index. It needs **View knowledge base feedback**.

## Permission

| Permission | Allows | Default roles |
| --- | --- | --- |
| `ViewKnowledgeBaseFeedback` (**View knowledge base feedback**) | The feedback report and data set | Administrator, Editor |

## Personal data

`KnowledgeArticleFeedbackPersonalDataContributor` takes part in personal data requests for a signed-in reader. An export lists each answer (`KnowledgeArticleFeedback`) with the article, whether it helped and the comment. An erasure clears `UserId` and `UserName` and scrubs the person's details from the comment; the answers stay, so the article's counts don't change.
