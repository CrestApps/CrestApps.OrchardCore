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
| `SurveyQuestionBanks` | Question banks (`QuestionBanks`) with their `Questions` | Bank id, then `Name`; questions by id, then `SourceIdentifier` |
| `SurveyResponses` | Responses (opt-in) | Id |
| `SurveyOverrides` | Accommodations, people by user name | Id |
| `SurveyInvitations` | Invitations, token hashes only | Id |

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
- The steps write the stores directly, so catalog handlers do not run on import.

## Responses, accommodations and invitations

The `SurveyResponses` deployment step is opt-in, with options for the surveys, a date range, test responses and responses in progress. It reads pages of 500, needs Export survey responses with respondent identity, and is recorded in the Audit Trail. People travel by user name; a user name the site does not have is a recipe error. Anonymous responses travel without identity.

## Sample recipe and schema

The module ships `Recipes/surveys-samples.recipe.json`: a customer satisfaction survey, a Net Promoter Score survey, a graded quiz and a question bank, each survey published with a public link.

The `SurveyDefinition` JSON schema (`SurveyDefinitionSchemaBuilder` in Surveys.Core) describes every member and each question type's settings, which `ISurveyControlSchemaProvider` gives (by default, by reflection over the type's settings class). Authors and AI tools use it to write survey recipes.
