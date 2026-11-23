---
sidebar_label: Scoring and grading
title: Scoring and Grading
description: "Quiz scoring with points, partial credit, penalties, categories, pass marks and bands; the review policy; immediate feedback; manual grading, regrades, score changes and accommodations."
user_manual:
  - user-manual/surveys/quizzes
  - user-manual/surveys/grade-responses
---

Any survey can be scored; a quiz is a survey of kind `Quiz` with scoring on.

## Scoring settings

`SurveyDefinition.Scoring`: `Enabled`, `PassMarkPercent`, `DefaultPoints`, `NegativeMarking`, `ShowScoreToRespondent`, `FeedbackMode` (`Deferred` or `Immediate`), `RoundingDecimals` and `ScoreFormula` (a formula over category scores; the sum when empty).

A question's key (`SurveyQuestion.Scoring`) holds `Points` (empty uses the default; 0 leaves it unscored), `PartialCredit`, `PenaltyPerWrong`, `CategoryId`, `ChoicePoints`, `CorrectChoiceIds`, `AcceptedAnswers`, `CaseSensitive`, `NumberAnswer` and `Tolerance`, matching `Pairs`, feedback texts per answer and per choice, `GraderGuidance`, and `Key` for custom types. The key never reaches respondents.

## How a response is scored

`ISurveyScoringEngine` scores every question the respondent was shown with its type's scorer when the response is completed:

- an unanswered question scores 0; a question that logic hid, or that sits on a page a branch skipped, does not count toward the maximum, unless its answer is locked (see below);
- a scorer that returns no points (a long text, a file upload) leaves the response **Pending** a grader;
- category totals, the score (or the score formula), the percent, the pass mark and the score band are set;
- a grader's score always wins over the automatic one.

The completion scores the response as completed, so it counts the pages on the respondent's page path (`SurveyResponse.PagePath`), the same questions a later regrade and the review count; this holds for a response submitted at its deadline too. Scoring runs before the ending is chosen, so an ending condition on `score` or `score:{categoryId}` reads this response's score; a branch that goes to an ending names it directly. Reopening a response (edit after submit) clears its score, percent, pass and band, so rules never read an earlier completion's score.

A response that still waits for a grader never passes, and its score, right answers and feedback stay hidden from the respondent until it is graded.

## Review policy

`SurveyDefinition.Review` sets what respondents see (score, their answers, right answers, answer feedback, general feedback) at each moment: while answering (immediate feedback), right after finishing, later while the survey is open, and after it closes. `ResultsVisibility` decides when respondents see the totals of all responses (polls). `ISurveyReviewPolicy` applies these on the review page, the ending and immediate feedback.

A scored survey that lets respondents change a finished response (`Navigation.AllowEditAfterSubmit`) can't show the score (when `ShowScoreToRespondent` is on), the right answers or answer feedback right after finishing or later while the survey is open: reopens are unlimited, so the respondent could read the key, or change one answer at a time and watch the score, and resubmit. The validator refuses that combination (`edit-after-review`). Show them after the survey closes, when nothing can be reopened, or turn off changes after finishing.

With **One at a time, then locked** (`FeedbackMode.Immediate`), the respondent checks an answer (`/s/r/{responseId}/check/{questionId}`); it is scored and locked, and the feedback the review allows during the response is shown.

A locked answer (`SurveyAnswer.CheckedUtc` is set; `SurveyAnswer.IsLocked`) is never removed or changed. The respondent also can't take it off their path or hide it: a save, a page post (Next, Back or Submit), a check or an API page post whose answers would do that is refused with `SurveyErrorCode.InvalidAnswer`, nothing is saved, and the error ("You can't change this answer now...") goes on each posted answer that would do it. To decide, the runtime walks the response from its first page with its answers before and after the write, choosing each next page as a page post does (page visibility, branches, branch endings, the answers a branch skips dropped, calculated variables recomputed), and compares which locked answers end up on a page of that path and not hidden. Live evaluation (`/s/r/{responseId}/evaluate`) returns the same errors in `errors`, so the page says so as the respondent changes the answer, and the runner script never hides a locked question as other answers change. Only the respondent's own change is refused: a locked answer the author's logic already took away before the write (a rule on the date or time, say) is left alone, and when a page's time is up an invalid answer saved in time is kept as saved rather than dropped if dropping it would hide a locked answer (nothing posted is read then, so refusing would stop the respondent for good). A locked answer the author's logic hides keeps counting, since the respondent already saw it graded: the engine scores it with its points and adds its points to the maximum, so the percent stays consistent. `SurveyScoringEngine.CountedQuestions` lists the questions that count (the questions on the respondent's path that logic did not hide, then the locked ones that were hidden or skipped), and the review page shows the same questions. The grading page lists the questions with a maximum (`SurveyAnswer.MaxPoints`), which the engine sets for a locked hidden answer too, and the AI explanation of a quiz result reads each question's points from it. An answer that was not checked and is hidden later is dropped or left out, as before.

## Time limits

`SurveyDefinition.Timing`: `TimeLimitMinutes`, `GraceSeconds` (default 5) and `AutoSubmitAtDeadline`. The deadline is set when the response starts (plus any accommodation's extra minutes, capped at the close time). A page may also have its own time limit, counted from the respondent's first visit to the page; a page split into several screens by **Questions per page** gets its limit once, not once per screen. The server enforces both; the runner shows a timer.

A response whose deadline and grace passed is submitted with what was saved in time, by the respondent's next request or by the deadline task. Either way the completion counts toward the survey's per-person limits: the response keeps the key its start was counted under until it completes.

## Grading

`ISurveyGradingService`:

| Method | Does |
| --- | --- |
| `GradeAsync` | Scores questions by hand with comments and overall feedback |
| `RecordScoringHelpAsync` | Marks that a grader used AI help on a question |
| `RegradeAsync` | Scores a response again with its version's key; grader scores are kept |
| `RegradeVersionsAsync` | Scores responses of older versions with another version's key, only for questions whose type did not change |
| `ChangeScoreAsync` | Sets the total score; a reason is required |

Every change adds an entry to the response's score history (who, when, from, to, why) and raises `GradedAsync` or `ScoreChangedAsync`.

The grading queue (`admin/surveys/grading`) lists responses waiting for a grader in the surveys the user may grade: the global permission, `ISurveyGradingScope` implementations (Learning adds course staff) and `ISurveyAuthorizationService`.

Nothing is added to the queue by hand: a completed response appears when it has questions a person must score, such as essays or uploaded files, and leaves once it is graded. Each response waits in one queue only. A module that grades its own responses on its own page registers its context kind with `AddSurveyContextKindGradingQueue(kind)`, and the Surveys queue then leaves those responses out. Learning does this for enrollments, so course quiz attempts wait under **Learning > Grading** with coursework. A single survey's queue (`admin/surveys/grading?surveyId=...`) still lists all of its responses.

## Accommodations

An accommodation (`SurveyOverride`) gives a user or a user audience, optionally in one context (such as an enrollment), extra minutes, extra attempts, or another opening and closing time, with a required reason. `ISurveyOverrideService.FindEffectiveAsync` combines the ones that apply into the most generous. They are managed at `admin/surveys/{id}/overrides`.
