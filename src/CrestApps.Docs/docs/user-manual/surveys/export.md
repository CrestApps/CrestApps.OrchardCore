---
sidebar_label: Export
title: Export Responses
description: "Export responses to CSV or Excel, and use the survey data sets in data pipelines and the Report Builder."
technical_manual:
  - modules/surveys/data-pipelines
  - modules/surveys/results-and-reports
---

Export responses from the **Survey Responses** report, or read them in data pipelines and the Report Builder.

| | |
| --- | --- |
| **Menu** | **Reports** → **Surveys** → **Survey Responses** |
| **Permission** | View responses of own surveys, or View responses of all surveys |
| **Feature** | Surveys and Reports; Data Pipelines for pipelines |

<AskYourAdmin />

## Export to CSV or Excel

1. Go to **Reports** → **Surveys** → **Survey Responses**, or select **Results** in the survey's menu and switch to that report.
2. Pick the survey and the filters.
3. Export to CSV or Excel. Each response is a row and each question a column, across every version. The export holds every response that matches the filters, not only the page on screen. On an identified survey it names who responded; on an anonymous survey it names nobody, shows days only, and has no rows until enough responses match (the survey's minimum group size).

## Data pipelines and the Report Builder

The **Surveys** data source offers surveys, versions, questions, responses, answers, category scores, page visits, invitations, and one data set per published survey with a column per question. Use it to send a survey's responses to Excel every week, or to build your own report.
