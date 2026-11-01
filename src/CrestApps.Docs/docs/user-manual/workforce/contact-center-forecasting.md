---
sidebar_label: Contact Center Forecasting
title: Contact Center Forecasting
description: "Forecast how much work your queues will get, work out how many agents each 15 minutes needs to meet your service goal, and turn the forecast into the team's coverage for scheduling."
technical_manual:
  - modules/workforce-contact-center
---

Forecasting looks at how much work your queues had in past weeks, forecasts the coming weeks in 15-minute intervals, and works out how many agents each interval needs to meet your service goal. Make the forecast the team's coverage, and the planner shows where the schedule falls short and the auto-scheduler fills the gaps.

| | |
| --- | --- |
| **Menu** | Workforce > Forecasting > Intraday, Forecasts, Planning Groups, Capacity Plans, Forecast Events |
| **Permission** | Manage forecasts |
| **Feature** | Workforce Forecasting (it needs Contact Center Interval Statistics and Workforce Shift Planning); Workforce Intraday for the day as it happens |

<AskYourAdmin />

## Set up a planning group

A planning group is the queues forecast and staffed together.

1. Go to **Workforce > Forecasting > Planning Groups** and choose **Add planning group**.
2. Choose the **queues**, optionally one **channel**, and the **team that staffs it**.
3. Set the **service goal**: the share of contacts to answer within a number of seconds (such as 80% within 20), an average answer time, or both, and the **highest occupancy** agents should run at.
4. Set **shrinkage**, the share of paid time agents can't answer (breaks, meetings, absence), **contacts at once** for chats and messages, and optionally the **customer patience** if customers give up waiting.

| Field | What it does |
| --- | --- |
| **Weeks of history** | How far back the forecast looks. |
| **Customer patience** | How long customers wait before giving up, on average. Leave it empty if nobody gives up. |

## Make a forecast

1. Go to **Workforce > Forecasting > Forecasts** and choose **Make a forecast**, or **Forecast** on a planning group.
2. Choose the first day, how many weeks, and the model. **The best on the last two weeks** tries both models on the last two weeks of history and keeps the closer one.
3. Select **Make forecast**. It opens as a draft.

The forecast page shows the total contacts, the most agents needed at once, the agent hours, and how close each model came on the last two weeks. Select a day to see its intervals: the contacts, the handle time, the agents needed, the agents to schedule (with shrinkage), and the service level and occupancy they give.

:::note[No history]
A forecast needs the interval statistics of the group's queues before the first day. Turn on **Contact Center Interval Statistics** a few weeks before forecasting.
:::

## Adjust a forecast

When you know something the history doesn't, such as a mailing, enter the days, the change in work (10 for a tenth more, -20 for a fifth less) and why, then select **Adjust**. The days are staffed again, and the adjustments are listed under the form.

## Plan for holidays and promotions

Under **Workforce > Forecasting > Forecast Events**, add the days whose work differs: a holiday, a closure (-100%), a promotion. Choose **Every year on the same dates** for a fixed holiday. Forecasts made after the event is saved use it.

## Use the forecast for scheduling

On a draft forecast, select **Make it the team's coverage**. The forecast becomes active, the group's previous forecast is superseded, and the team gets a coverage requirement named *Forecast: group name* for the forecast's days. The planner board shows where the schedule falls short of it, and **Auto-schedule** fills the gaps. Adjusting an active forecast updates the coverage.

## Watch the day as it happens

Go to **Workforce > Forecasting > Intraday** and choose the planning group and the day. Every 15 minutes shows the contacts (with the forecast in brackets), the handle time, the agents needed, the agents scheduled, with how many short, the agents who were available or working, and the service level reached. Today's actuals arrive as the interval statistics are computed, every 15 minutes.

- **Against the forecast** tells how far the work so far is from the forecast. **Reforecast the rest of the day** scales the rest of the day by half that difference and updates the team's coverage.
- **Best times for a meeting or training:** enter how many people and for how long, and select **Find**. The times that leave the queue least short come first.

## Offer extra hours or time off

On the intraday page, under **Offer extra hours or time off**, choose the offer, the times, how many people and a note, then select **Offer**. Workers in the team see it under **Offers** in My Work and select **Take it**, first come, first served:

- **Extra hours** lengthen the worker's shift that ends when the offer starts, or starts when it ends.
- **Time off** comes off the start or the end of the worker's shift.

The worker chose it, so it earns no predictability pay. The intraday page lists the day's offers with who took them; select **Close** to stop an offer early.

## Plan headcount for the months ahead

A capacity plan shows, week by week, the agents a planning group needs against the agents you expect to have, so you know when to hire.

1. Go to **Workforce > Forecasting > Capacity Plans** and select **Add capacity plan**.
2. Enter a name and choose the planning group, the first week and how many weeks to plan, up to 78.
3. Under **Work**, enter the handle hours of a week, or leave it empty to use the average week of the last four weeks, and the growth you expect in a year.
4. Under **Agents**, enter the agents you have today in full-time equivalents (leave it empty to count the team), a full-time agent's hours a week, and the share who leave each month.
5. Under **Hiring classes**, add each class you plan: its first day, how many agents, the weeks of training and the weeks to reach full productivity. Leave a row empty to skip it.
6. Select **Save**.

The plan's page shows the work and agents in the first and last weeks, how many weeks are short, and when you're first and most short, with the size of a class that would cover it and when it should start. The table shows every week: the work, the agents needed, the agents expected and in training, and how many over or short. The planning group's shrinkage and highest occupancy turn the work into agents.

## Report on forecasts

In **Reports > Report Builder**, the **Workforce** data source has **Forecast intervals**: one row per forecast and 15 minutes, with the contacts, the handle time and the agents needed. Filter on **Status** = Active to compare the active forecast with the **Queue intervals** of the Contact Center data source.

## Ask the AI assistant

When an AI profile has the **Forecast summary** tool, planners can ask for the day's forecast: "What's the forecast for Sales voice tomorrow?" With **Who's out of adherence**, supervisors can ask who's out of adherence now in their teams.

## Set it up with a recipe

The **Workforce for Contact Center sample** recipe, under **Configuration > Recipes**, sets up a team, a queue, a planning group, a holiday and a promotion, a capacity plan and adherence rules to try everything with. The `WorkforceForecasting` and `WorkforceAdherence` recipe steps set up your own.
