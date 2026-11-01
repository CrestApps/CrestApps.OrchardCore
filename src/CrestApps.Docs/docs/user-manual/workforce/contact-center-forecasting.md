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
| **Menu** | Workforce > Forecasting > Forecasts, Planning Groups, Forecast Events |
| **Permission** | Manage forecasts |
| **Feature** | Workforce Forecasting (it needs Contact Center Interval Statistics and Workforce Shift Planning) |

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
