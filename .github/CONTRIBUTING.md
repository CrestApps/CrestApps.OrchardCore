# Contributing to CrestApps Orchard Core Modules

Thank you for your interest in contributing! Whether you're fixing bugs, adding new features, or improving documentation, your help is appreciated.

Before getting started, please read through our [README](../README.md) to familiarize yourself with the project.

---

## Setting Up the Project Locally

Start by cloning the repository and switching to the `main` branch:

```bash
git clone https://github.com/CrestApps/CrestApps.OrchardCore.git
cd CrestApps.OrchardCore
git checkout main
```

You can work with the codebase using your preferred development environment.

### Command Line

1. Install the latest .NET SDK from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).
2. Navigate to the appropriate module or sample site.
3. Run the site using `dotnet run`.
4. Open `http://localhost:5000` in your browser.

### Visual Studio

1. Install Visual Studio 2022 or newer from [https://visualstudio.microsoft.com/downloads](https://visualstudio.microsoft.com/downloads).
2. Open the solution file (`.sln`) found at the root of the repo.
3. Wait for NuGet packages to restore.
4. Set the correct startup project (e.g., a demo or test website), then run with `Ctrl+F5`.

---

## Choosing What to Work On

We welcome contributions of all kinds! Here's how you can find something meaningful to contribute:

* Browse [open issues](https://github.com/CrestApps/CrestApps.OrchardCore/issues) to see what's currently being worked on or needs help.

If you have an idea or improvement that's not tracked yet, please open a new issue first and discuss it with the maintainers before starting work.

---

## Contribution Scope

* **Small Fixes (typos, minor bugs)**: Feel free to submit a pull request directly.
* **Features or Major Changes**: Please open an issue to discuss first. We want to make sure it aligns with the overall goals and doesn't duplicate existing efforts.

Recommended reading:

* [Open Source Contribution Etiquette](http://tirania.org/blog/archive/2010/Dec-31.html) by Miguel de Icaza
* [Don't "Push" Your Pull Requests](https://www.igvita.com/2011/12/19/dont-push-your-pull-requests/) by Ilya Grigorik

---

## Submitting a Pull Request (PR)

> New to pull requests? Check out [this guide](https://help.github.com/articles/using-pull-requests).

To submit a quality PR:

* Ensure your code follows our coding style and practices.
* Verify the project builds and all tests pass.
* If you make front-end changes (JS/CSS), be sure to run any relevant asset build tools.
* Link your PR to a relevant issue using `Fix #issue_number` in the description.
* If you're not finished yet, mark your PR as a **draft**. You can always switch it to "Ready for Review" later.
* Include screenshots or screen recordings for UI changes.
* Add tests for new behavior, especially for non-trivial logic.
* For major changes or new features, update the appropriate release notes (if available).
* Please [allow maintainers to edit your PR](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/working-with-forks/allowing-changes-to-a-pull-request-branch-created-from-a-fork) for easier collaboration.

> Not sure how to proceed? Just ask — we're happy to guide you!

---

## Review Process & Feedback

Every PR is reviewed by the core team. Here's how to keep the process smooth:

* Address feedback promptly and thoroughly.
* Apply [suggested changes](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/reviewing-changes-in-pull-requests/incorporating-feedback-in-your-pull-request#applying-suggested-changes) directly whenever possible.
* Don't manually resolve conversations — let the reviewer do that.
* You can mark addressed conversations with an emoji or comment for tracking.
* Keep all related discussions inside the PR to keep things organized.
* When you've addressed feedback, use "Re-request review" to notify reviewers.

---

## Thank You!

We deeply appreciate your contributions. All PRs are reviewed with care to ensure they fit the quality and goals of the project.

Following these guidelines helps make sure your contribution is merged quickly and smoothly — and makes the process pleasant for everyone involved.
