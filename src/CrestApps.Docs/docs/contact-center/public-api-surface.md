---
title: Public API Surface
---

# Public API surface

Everything the Contact Center, Telephony, and Omnichannel assemblies expose publicly is a promise to whoever compiles against them. Other modules in this repository compile against them, and so does whatever a deployment builds beside them. A type turning public, a member changing shape, a class losing `sealed`, or a parameter changing type are all compatibility decisions, but in a diff they read like ordinary edits - and the first report that one of them was breaking usually arrives from a deployment that no longer builds.

The public surface of every governed assembly is therefore generated and checked in. A change to it fails the build and arrives as a diff that a reviewer has to accept on purpose.

## Which assemblies are governed

The governed set is derived from the project graph rather than listed by hand, because a list is something a contributor has to remember to add to, and the assembly nobody remembers is the one that breaks.

A project is governed when both of the following are true:

- Its name is in the Contact Center, Telephony, or Omnichannel families - that is, it matches `CrestApps.OrchardCore.(ContactCenter|Telephony|Omnichannel)` optionally followed by a suffix.
- Some project other than the packaging target (`CrestApps.OrchardCore.Cms.Core.Targets`) or the web host (`CrestApps.OrchardCore.Cms.Web`) references it. Those two reference every module in order to package or host it rather than in order to compile against it, so counting them would make every module look like a contract.

Adding a project reference from one module to another is therefore enough to bring the referenced assembly under the gate, and the build will ask for its baseline on the next run.

## The recorded baselines

Baselines live in `tests/CrestApps.OrchardCore.Tests/PublicApi/Baselines`, one `<AssemblyName>.approved.txt` per governed assembly, and are produced by `PublicApiGenerator` from the compiled assembly.

`PublicApiApprovalTests` compares each governed assembly against its baseline and fails in three directions, so the recorded set cannot drift away from the governed set:

| Situation | What happens |
| --- | --- |
| A governed assembly has no baseline | The test fails and writes the baseline. Read it, decide whether every member on it is meant to be public, then commit it. |
| A governed assembly's surface differs from its baseline | The test fails, prints the added and removed lines - each qualified by the type that encloses it, so a member removed from one of many look-alike types is still named - and writes `<AssemblyName>.received.txt` beside the approved file. |
| A baseline exists for an assembly nothing compiles against any more | The test fails as orphaned, so stale baselines are deleted rather than left to look like coverage. |

`*.received.txt` is ignored by Git and is only ever a build output.

Baselines must be reproducible on any machine, so a recorded surface that contains a filesystem path fails as well. Assembly attributes that embed the build directory - `ModuleAssetAttribute` and `RazorCompiledItemAttribute`, both emitted once per view - are excluded for that reason. If a future attribute starts carrying a path, that check fails rather than leaving every contributor with a baseline only its author can reproduce.

## Accepting a deliberate surface change

When a surface change is intended, accept it explicitly:

```bash
# 1. Run the gate. It writes the received file and prints what changed.
dotnet test tests/CrestApps.OrchardCore.Tests -c Release --filter "FullyQualifiedName~PublicApiApprovalTests"

# 2. Read the diff it printed, then replace the approved file with what was received.
cd tests/CrestApps.OrchardCore.Tests/PublicApi/Baselines
mv CrestApps.OrchardCore.Telephony.received.txt CrestApps.OrchardCore.Telephony.approved.txt

# 3. Re-run to confirm the surface and the baseline now agree.
dotnet test ../../../CrestApps.OrchardCore.Tests -c Release --filter "FullyQualifiedName~PublicApiApprovalTests"
```

Commit the updated `.approved.txt` in the same change as the code that moved the surface, and say in the pull request why the change is safe for callers. The point of the gate is not to prevent the surface from changing; it is to make sure somebody decided that it should.

## Rules the baseline does not enforce on its own

A recorded surface is still only text, and text is easy to accept in bulk. Two conclusions of the surface audit are therefore stated as rules, so that breaking one arrives as a named failure rather than as one more line in a diff:

- **A public class is sealed unless inheriting from it is the point.** A class is accepted when it is `sealed`, when it is `abstract`, when it *introduces* an overridable member, or when it lives in a `ViewModels` namespace - the display framework builds a runtime proxy from view models and cannot do that from a sealed type. Overriding an inherited member does not count: a driver that overrides one framework method has said nothing about whether anyone may derive from it. Members the compiler synthesizes do not count either, which is why a public `record` is not automatically treated as an extension point by virtue of its generated equality members.
- **No public type exposes mutable static state.** Public static state is shared by every tenant in the process and by every test in the run, so a value one of them changes is a value all of them see. Public static fields must be `const` or `readonly`, and public static properties must not have a public setter - but `readonly` only freezes a reference, so static collections are judged by the value they actually hold rather than by the type they are declared as. A `List<T>` handed out behind an `IReadOnlyList<T>` fails, because a caller can cast it back and rewrite it; an array fails, because it reports itself as read-only while still allowing element assignment; an immutable or frozen collection passes.

## Related

- [PR-to-test control matrix](pr-test-control-matrix.md) - gate `C010` tracks this control.
- [Supply chain security](../supply-chain.md) - the equivalent controls for dependencies.
