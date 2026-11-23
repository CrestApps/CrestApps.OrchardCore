---
sidebar_label: Recipes and deployment
title: Survey Recipes and Deployment
description: "The Surveys recipe and deployment steps, their JSON, secrets, the sample recipe and the definition schema."
user_manual:
  - user-manual/surveys/publish-and-versions
---

Every Surveys record has a recipe step and a deployment step with schemas (when CrestApps Recipes is on). Item ids are generated unique ids.

| Step | Records | Matched by |
| --- | --- | --- |
| `Surveys` | Surveys with their draft, access, retention, `Versions` and `Collectors` | Id, then `Name`; versions by survey and number |
| `SurveyTemplates` | Survey templates (`Templates`) with their `Definition` | Id, then `Name` |
| `SurveyQuestionBanks` | Question banks (`QuestionBanks`) with their `Questions` | Bank id, then `Name`; questions by id, then `SourceIdentifier` |
| `SurveyResponses` | Responses (opt-in); collector by `CollectorName` | Id |
| `SurveyOverrides` | Accommodations, people by user name | Id |
| `SurveyInvitations` | Invitations, token hashes only | Id |

Surveys and question banks carry their owner as `OwnerUserName`, set on the target when the record is created or has no owner yet. A response names its collector by `CollectorName` (matched by name, then by `CollectorId`, then the survey's default collector) and keeps its `InvitationId` only when an invitation of the same survey with that id is on the site, so import the `SurveyInvitations` step before `SurveyResponses`. Responses of anonymous surveys are exported without their `Number` (one sequence numbers every response of the site, so it could name the respondent next to identified responses), and the import gives them, and any response without a number, a new one from the target's sequence. The `SurveyResponses`, `SurveyInvitations` and `SurveyOverrides` deployment steps hold personal data (answers, e-mail addresses, names, reasons for accommodations): they write records only for a signed-in user with Export survey responses, and an empty list otherwise, a scheduled or remote export included.

Survey site settings travel in Orchard Core's `Settings` step as `SurveySettings`.

## The Surveys step

```json
{
  "name": "Surveys",
  "Surveys": [
    {
      "ItemId": "43cxfrt81g8mm52kvve7jp02cq",
      "Name": "net-promoter-score",
      "Title": "Net Promoter Score",
      "Kind": "Survey",
      "Status": "Open",
      "Draft": {
        "Title": "Would you recommend us?",
        "Pages": [
          {
            "Id": "4fna3nztjphx3vvktbfk2pa12v",
            "Name": "page_1",
            "Questions": [
              {
                "Id": "4dfhbt1zbets75mz7j6b1bhkz8",
                "Name": "q_nps",
                "ControlType": "Rating",
                "Title": "How likely are you to recommend us to a friend or colleague?",
                "Required": true,
                "Settings": { "Min": 0, "Max": 10, "Style": "Numbers" }
              }
            ]
          }
        ],
        "Endings": [
          { "Id": "4k2c3szmm1twwgd96qe16wnzr1", "Name": "thanks", "Title": "Thank you", "Outcome": "Completed", "IsDefault": true }
        ]
      },
      "PublishedVersionNumber": 1,
      "Access": { "Audience": "Anyone", "Anonymity": "Anonymous" },
      "Versions": [
        { "ItemId": "4znryzkaj914jzb6z1gk8zfxep", "Number": 1, "Definition": { "...": "the definition" }, "PublishedUtc": "2026-11-20T00:00:00Z" }
      ],
      "Collectors": [
        { "Name": "Public link", "Kind": "WebLink", "IsDefault": true }
      ]
    }
  ]
}
```

- A version number the site already has is skipped (versions are immutable); missing ones are created; `PublishedVersionNumber` is set last.
- Collector tokens and password hashes are exported only when the deployment step's **Include secrets** option is on, which needs Export survey responses with respondent identity; otherwise import makes new tokens.
- The survey's anonymity secret is never exported; import makes a new one.
- A survey `Name` must be lowercase letters, digits and dashes, as in the editor, and not used by another survey; otherwise the survey is skipped with a recipe error.
- The steps write the stores directly, so catalog handlers do not run on import.

## Responses, accommodations and invitations

The `SurveyResponses` deployment step is opt-in, with options for the surveys, a date range, test responses and responses in progress. It reads pages of 500, needs Export survey responses with respondent identity, and is recorded in the Audit Trail. People travel by user name; a user name the site does not have is a recipe error. A raw `UserId` without a `UserName` is kept only when it names a user of the site; otherwise the response is imported without a user and a recipe error says so. Anonymous responses travel without identity.

## Sample recipe and schema

The module ships `Recipes/surveys-samples.recipe.json`: a customer satisfaction survey, a Net Promoter Score survey, a graded quiz and a question bank, each survey published with a public link.

The `SurveyDefinition` JSON schema (`SurveyDefinitionSchemaBuilder` in Surveys.Core) describes every member and each question type's settings, which `ISurveyControlSchemaProvider` gives (by default, by reflection over the type's settings class). Authors and AI tools use it to write survey recipes.
