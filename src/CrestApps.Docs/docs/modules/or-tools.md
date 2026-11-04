---
sidebar_label: OR-Tools
title: OR-Tools
description: "Google OR-Tools solvers for auto-scheduling shifts (CP-SAT) and optimizing a dispatch day (routing), each starting from the built-in solver's answer and keeping the better one."
user_manual:
  - user-manual/workforce/shift-planning
  - user-manual/field-service/dispatch
---

The **OR-Tools** module (`CrestApps.OrchardCore.OrTools`) adds [Google OR-Tools](https://developers.google.com/optimization) solvers (Apache 2.0) to Workforce and Dispatch. It is an add-on: OR-Tools brings native libraries for each platform (Windows, Linux and macOS, x64 and Arm64), so it isn't in the CrestApps CMS targets. Add the `CrestApps.OrchardCore.OrTools` package to your application, then turn on its features.

## Features

| Feature | Id | Depends on |
| --- | --- | --- |
| OR-Tools Shift Scheduling | `CrestApps.OrchardCore.OrTools.ShiftScheduling` | Workforce Shift Planning |
| OR-Tools Dispatch | `CrestApps.OrchardCore.OrTools.Dispatch` | Dispatch |

## How the solvers work

Each solver first runs the built-in one, gives its answer to OR-Tools as the starting point, and keeps the better of the two. So turning a feature on never makes the schedule or the day worse, and when OR-Tools finds nothing in the time left (or can't load), the built-in answer is used. Results name the solver: the auto-schedule message ("The OR-Tools solver made this schedule in 1.3 seconds.") and the board's proposal ("Proposed by the OR-Tools solver.").

### Shift scheduling

`OrToolsShiftScheduleSolver` is an `IShiftScheduleSolver` named `OR-Tools`; turning the feature on makes it `ShiftPlanningOptions.DefaultSolver`, so **Auto-schedule** uses it. It builds a CP-SAT model:

- a yes-or-no choice per worker, day and template, only where the worker works that day, holds the template's skills, is available for the whole shift, has no fixed shift that day or too close to it, and stays within their daily hours;
- one shift a day, rest between shifts on nearby days, weekly hours under the maximum, and days in a row, as hard limits;
- for each interval and skill, the people counted up to its minimum (weighted 100) and its target (weighted 30), going over its maximum (−40), an hour short of a worker's weekly minimum (−15), each interval paid for (−1), and a shift inside a preferred window (+5).

It uses the problem's time limit less what the built-in solver took, up to eight threads, and the problem's seed. `TimedOut` is set when it didn't prove its schedule optimal.

### Dispatch

`OrToolsDispatchOptimizer` is an `IDispatchOptimizer` named `OR-Tools`, registered after the built-in one, so **Optimize the day** uses it. It builds a routing model:

- a route per technician from their home base; no trip back home is counted, as with the built-in optimizer;
- a time dimension of travel, the buffer between visits and each visit's length, with each visit's start on a five-minute step that fits one of the technician's open stretches and the visit's window, and the gaps between stretches (breaks) as fixed breaks;
- only the technicians a visit allows, fixed visits on their technician at their start, and each other visit optional at its penalty;
- the cost: travel minutes, plus the penalty of every visit left out.

It starts from the built-in plan with guided local search for the problem's time limit, less what the built-in optimizer took.

On the test fixtures, the shift solver covered 97.9% of the target on the two-week clinic sample (the built-in 97.7%), with every minimum met, proved optimal in about a second; on a day of 20 visits for three technicians with a lunch break, windows and a skill, the routing solver cut the cost from 2,288 to 2,205.
