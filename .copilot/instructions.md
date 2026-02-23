# GLOBAL COPILOT CLI RULES (MANDATORY)

## SOURCE CONTROL RULES
- NEVER run git commit
- NEVER run git push
- NEVER amend commits
- NEVER stage files unless explicitly instructed
- ALL changes must remain local and uncommitted
- Assume the user will manually review and commit

## CODE CHANGE RULES
- You MAY modify files locally
- You MUST clearly describe all changes made
- You MUST assume the user will review diffs before committing
- If unsure, prefer minimal and incremental changes

## DOCUMENTATION ENFORCEMENT (REQUIRED)
Whenever code is modified:

1. You MUST evaluate the documentation project located at:
   src\CrestApps.OrchardCore.Documentations

2. You MUST update documentation to reflect the changes:
   - Update the relevant **feature documentation first**
   - Ensure accuracy with the latest behavior
   - Use concise, developer-facing language

3. After feature docs are updated:
   - Add an entry to the **changelog** in the same documentation project
   - Clearly describe:
     - What changed
     - Why it changed
     - Any breaking or behavioral impact

4. Documentation changes are NOT optional
   - Code changes without documentation updates are considered incomplete

## QUALITY EXPECTATIONS
- Prefer clarity over cleverness
- Follow existing project conventions
- Do not introduce undocumented behavior
- Keep documentation and code in sync at all times

## ASSUMPTIONS
- The user evaluates all changes before committing
- The user controls all source control actions
