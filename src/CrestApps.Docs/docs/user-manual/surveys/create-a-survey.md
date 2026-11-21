---
sidebar_label: Create a survey
title: Create a Survey
description: "Create a survey from a template, find it in the survey list, and set who may respond and when."
technical_manual:
  - modules/surveys/designing
  - modules/surveys/collecting
---

Create a survey from a template, then design its questions and set who may respond.

| | |
| --- | --- |
| **Menu** | Surveys > Surveys |
| **Permission** | Manage own surveys, or Manage all surveys |
| **Feature** | Surveys |

<AskYourAdmin />

## Create a survey

1. Go to **Surveys > Surveys** and select **Add survey**.
2. Enter a **Title**. You can enter a **Technical name** (lowercase letters, digits and dashes), or leave it empty to make one from the title.
3. Under **Start from**, pick a template: **Blank survey**, **Customer satisfaction**, **Net Promoter Score**, **Employee engagement**, **Event feedback**, **Course feedback**, **Quiz** or **Poll**.
4. Select **Create**. The designer opens.

## The survey list

Each row has **Design** and **Preview** buttons, and a menu with **Collect responses**, **Responses**, **Results**, **Versions**, **Access and settings**, **Logic overview**, **Accommodations**, **Regrade versions**, **Grading**, **Copy as a draft**, **Close** or **Open**, **Archive** or **Unarchive**, and **Delete**. Search by name, and filter the list by its filters above the rows.

You can delete only a survey that has no responses. Archive a survey with responses instead: it keeps its results and leaves the default list.

## Who may respond and when

Open the survey's menu and select **Access and settings**. These settings apply at once, without publishing again.

| Field | What it does |
| --- | --- |
| **Title**, **Technical name**, **Notes**, **Tags** | What staff see. Tags filter the survey list. |
| **Audience** | **Anyone with the link**, **Signed-in users**, **Members of chosen audiences** or **Only invited people**. |
| **Audiences** | The ids of the user audiences that may respond, separated by commas, when the audience is **Members of chosen audiences**. |
| **CAPTCHA** | Ask respondents who are not signed in to pass a CAPTCHA. |
| **Responses** | **Identified: record who responded**, or **Anonymous: record nobody**. A change applies to new responses only. |
| **IP address** | Record the IP address of identified responses. |
| **Minimum group size** | Reports of an anonymous survey hide any group with fewer responses. 0 turns this off. |
| **Opens**, **Closes** | When the survey takes responses, in your time zone. |
| **Weekly schedule** | Let responses start only on some days and hours, in the site time zone. |
| **Abandon after** | Idle hours after which an unfinished response is abandoned. |
| **Limits** | How many responses each person may give: **In total**, **A day**, **A week**, **A month**, and **Wait between responses**. **Count** chooses completed or started responses. |
| **Recognize respondents by** | **Their user account**, **Their invitation**, **A browser cookie (weak)** or **Not at all**. A cookie stops honest repeats only. |
| **Most responses** | The survey stops once this many are completed. |
| **Retention** | **Keep responses for** a number of days, **Then** delete them or remove who responded and keep the answers. |
