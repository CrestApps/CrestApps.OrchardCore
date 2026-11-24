---
sidebar_label: Feedback
title: Knowledge Base Feedback
description: "See what readers answered to \"Was this helpful?\": yes, no and the helpful rate for each article and category, and what readers said was missing."
technical_manual:
  - modules/knowledge-base/feedback
---

Each article ends with **Was this helpful?**. The **Knowledge Base Feedback** report shows what readers answered, so you know which articles to improve.

| | |
| --- | --- |
| **Menu** | **Reports** → **Knowledge Base Feedback** |
| **Permission** | View knowledge base feedback |
| **Feature** | Knowledge Base and Reports |

<AskYourAdmin />

## What readers answer

- A reader selects **Yes** or **No** under the article. Each browser answers once per article.
- After **No**, the reader may say what was missing. The comment is optional and is not answered.
- Each answer is kept with the date, the article and its category, and the reader's user name when they were signed in. Visitors stay anonymous.
- The answers also order the help center: the most helpful articles come first.

## The report

Go to **Reports** → **Knowledge Base Feedback**, choose the days, and select **Show**. The report shows:

| Section | Shows |
| --- | --- |
| **Summary** | All answers, how many said yes and no, the helpful rate, and how many readers said what was missing |
| **By article** | For each article: yes, no, the helpful rate and comments. The least helpful articles come first. |
| **By category** | The same for each category |
| **What was missing** | The comments, newest first, with the article, the reader and when, in your time zone |

Export it to CSV or Excel from the report's page. See [Reports](../reports.md) for running, filtering and exporting reports.

## Build your own

To build your own report or export the answers somewhere else, use the **Knowledge Base** data source in the [Report Builder](../report-builder/index.md) or in Data Pipelines. Its **Knowledge article feedback** data set has one row per answer.

## Personal data

A signed-in reader's answers are part of their personal data. An export of their data lists their answers and comments. When their data is erased, their name is removed from their answers and from their comments, and the answers stay in the counts.
