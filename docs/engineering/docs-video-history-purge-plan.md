# Purging the docs videos from git history

**Status:** proposal. Nothing here has been run against GitHub. Mike decides whether to rewrite history and runs every
step himself.

Since #925, every docs video is embedded from YouTube, and `check:manuals` fails on any video file under the docs. The
files are gone from `main`, but history still carries every version of them, so every clone still downloads them. Some
of the recordings also captured details that should not be public. Removing a file from `main` does not remove it from
the repository; only a history rewrite does.

## Decision in one paragraph

Rewrite only the refs that carry the videos, using `git filter-repo --refs`. That removes about 222 MiB of MP4s (and,
optionally, 98 MiB of retired GIFs) while leaving all 53 release tags, the 5 `release/*` branches and every commit
before #569 byte-for-byte unchanged. A normal clone shrinks from **321 MiB to 54 MiB**. The cost: 36 branches,
including `main` and every open PR except one, get new commit ids from 2026-08-05 onward; every local clone and worktree
has to be moved onto the new history; and to purge the data from GitHub itself (not only from new clones), GitHub
Support must dereference about 330 pull requests, whose "Files changed" and "Commits" tabs then stop rendering. Forks
that already copied the history keep it unless their owners clean them.

## What gets removed

Every version, on every ref, of:

| Pattern (under `src/CrestApps.Docs/static/img/docs/`) | Paths | Packed size | Notes |
| --- | --- | --- | --- |
| `*.mp4` | 133 | 222.6 MiB (278.7 MiB raw) | Section clips, overviews, silent screencasts, and Report Builder's clips on #814 |
| `*.vtt` | 38 | < 0.1 MiB | Captions |
| `*.jpg` | 29 | 1.1 MiB | Video posters; no `.jpg` is left on `main` |
| `*.webm` | 0 | 0 | None were ever committed; the glob is there as a guard |
| `*.gif` except 3 (optional, recommended) | 35 | 98.4 MiB | The GIF screencasts the MP4s replaced; only on the stale `ma/add-screencasts` branch and #551's refs |

Kept: the 7 `.png` screenshots, and `admin-ui-sample.gif`, `mcp-integration.gif` and `widget-ui-sample.gif`, which are
in the release tags. They are no longer on `main`: the versioned docs (1.2 and 2.1) embed chapters of the
`ai-walkthroughs` YouTube video instead. Their old versions stay in history, because purging them would mean rewriting
the release tags, and they hold no personal data (see the check below). Media under other
folders (`docs/images`, `src/docs`, `src/CrestApps.OrchardCore.Documentations`, `versioned_docs/version-1.2/images`)
is out of scope; together it is under 16 MiB.

Include the retired GIFs. They are the same recordings in an older format, they were never part of the OCR pass, and
they add no extra churn: every ref that holds them is already in the rewrite.

> **Checked:** `mcp-integration.gif` records the same demo as one of the MP4s that showed personal data, so its frames
> were reviewed on 2026-10-17 (34 frames from start to end). It shows only the demo site, the time MCP server and the
> maintainer's own username. It holds no personal data, so the 13 release tags from `v2.0.0-preview-0006` to `v2.1.5`
> can stay as they are.

## Measured sizes

Measured on 2026-10-17 on a mirror clone (`main` at 6084c50fb, 82 branches, 53 tags, 880 `refs/pull/*`), by running
the exact command below on throwaway local copies (nothing pushed).

| What | Today | Videos only | Videos + retired GIFs |
| --- | --- | --- | --- |
| Mirror, all refs including `refs/pull/*` | 386.7 MiB | 162.6 MiB | 67.9 MiB |
| `git clone` (all branches and tags) | 320.7 MiB | 148.8 MiB | **54.1 MiB** |
| `git clone --single-branch --branch main --no-tags` | 157.0 MiB | 37.7 MiB | 37.7 MiB |

A normal `git clone` never fetches `refs/pull/*`, so clones shrink as soon as the rewritten branches are pushed. GitHub's
own storage (318 MB today) only shrinks after Support runs garbage collection.

The rewrite took under 10 seconds; the slow parts are the clone and listing the affected refs (a couple of minutes).

The runbook below was also run end to end against a local bare copy standing in for GitHub: steps 2–4 as written, the
step 5 push (including a rejected push when a branch had moved since the clone), a fresh clone of the result (53.8 MiB,
no MP4 in history), and the worktree `remap` function on a fully pushed branch and on a branch with unpushed commits.

## Why a partial rewrite

A plain `git filter-repo --invert-paths ...` rewrites **4,770 of 4,778 commits**, starting from a 2024 Dependabot commit
that has nothing to do with videos. filter-repo strips commit signatures, and every commit GitHub merged through the web
UI is signed by GitHub, so the first signed commit and everything after it gets a new id. That would change every tag,
every release branch and every PR's commits back to 2024.

`--refs` limits the rewrite to the refs that actually reach a video, and the negative revisions stop it at the commit
before #569 (6f21a2903, the first commit on `main` that added an MP4) and at every tag. Measured result:

- **385 refs change:** 36 branches (including `main`), 331 `refs/pull/*/head`, 18 `refs/pull/*/merge`.
- **0 tags change.** No release tag contains an MP4, caption, poster or retired GIF, so tags, GitHub Releases and the
  NuGet packages built from them keep their commits and signatures.
- **`release/1.1` to `release/2.2` do not change**, and neither do 46 other branches that predate the videos.
- **`main`'s tree is identical** after the rewrite (same tree id); only commit ids from #569 onward change.
- The first changed commit on `main` is 6f21a2903; side branches that added videos on their own have their own first
  changed commits, and filter-repo prints them all.

`--refs` implies `--partial`, so filter-repo does not delete refs, remove `origin`, expire reflogs or gc on its own. The
runbook does those steps explicitly. `--replace-refs delete-no-add` keeps it from creating `refs/replace/*`, which would
otherwise be pushed by accident.

## What it does to GitHub

### Branches and the `main` ruleset

The ruleset "Protect main and releases" (id 1273162) blocks non-fast-forward pushes to `main` and `release/**`. It has to
be set to **Disabled** for the push and back to **Active** right after. `release/**` is not touched by the rewrite.

The push updates only the 36 changed branches, each with `--force-with-lease` against the id the mirror saw, and
`--atomic`, so a branch someone pushed to after the clone makes the whole push fail instead of being overwritten. A
`--mirror` push (what GitHub's guide shows) is avoided on purpose: it would also delete any branch created after the
clone, and it tries to push all 880 read-only `refs/pull/*` refs.

Expect GitHub Actions to start: `deploy_docs` (push to `main`), possibly `main_ci` (its `paths-ignore` filter is
evaluated against a forced update, which is not a normal diff), and `pr_ci` once per open PR whose head was rewritten.

### Open pull requests

Every open PR except the 2.2.0 release-notes PR has a rewritten head branch (13 at the time of writing). GitHub picks up
the new head and base: the diff stays the same because only media files disappear, but every commit id changes, review
comments become "outdated", and CI re-runs. Merge or close whatever is ready first. Any PR that still adds a video file
loses that file from all of its commits; #814 (Report Builder) must finish its switch to YouTube embeds before the purge,
or its pages lose their videos.

PRs from forks are not affected. None are open today.

### Merged and closed pull requests (`refs/pull/*`)

`refs/pull/*` are read-only. After the push, about 330 PRs (#569 onward that touched or were based on video history)
still point at the old commits, so the videos stay reachable on GitHub through those PRs, through commit URLs and through
raw-file URLs by commit id. Clones are unaffected (they don't fetch these refs). Only GitHub Support can fix this, and
it means those PRs lose their diff views (next section). Restricting the purge to only the clips with personal data
would not reduce this: 330 of the 331 affected PR heads reach at least one of them.

## GitHub's cached views and the Support request

GitHub keeps the old objects, and its cached views of commits, files and PR diffs, until Support removes them. Support
does this only for sensitive data (it qualifies here because of the personal data; size alone would not), and only once
every branch is clean and **no fork still references the commits**.

What Support does: dereferences or deletes the affected PRs, removes cached views, and runs garbage collection. Pull
requests keep their conversation, but their "Files changed" and "Commits" tabs stop rendering. That applies to roughly
330 PRs, most of them merged.

Open the request at <https://support.github.com/> ("Removing sensitive data"), with:

- Repository: `CrestApps/CrestApps.OrchardCore`.
- Number of affected PRs: `grep -c '^refs/pull/.*/head$' filter-repo/changed-refs` (331 in the test run).
- The "First Changed Commit(s)" filter-repo printed (also in `filter-repo/first-changed-commits`).
- "LFS is not used" (filter-repo reports `LFS object orphaning not checked (LFS not in use)`).
- A note that the release tags were deliberately left unchanged and contain none of the removed files.

Draft:

> We removed sensitive data from CrestApps/CrestApps.OrchardCore with `git filter-repo --sensitive-data-removal` and have
> force-pushed every affected branch. Please dereference the affected pull requests, remove cached views and run garbage
> collection. Affected PRs: N (count of `refs/pull/*/head` in `changed-refs`). First changed commits: (paste). LFS is not
> in use. Tags were intentionally not rewritten; none of them contains the removed paths.

Out of anyone's control: copies already made by other people, search-engine or archive caches, and CDN caches such as
jsDelivr's `gh/` mirror.

## Forks

The repository has 19 forks. On 2026-10-17, the default branch of 4 of them contains the commits that added the videos;
2 of those 4 also contain the commits with the contact-center clips. (Which forks: see the private hand-over note, not
repeated here.) Only default branches were checked.

GitHub does not clean forks, and Support will not purge while a fork still references the data, because forks share
object storage with the parent and old commits stay reachable through it. Before opening the Support request, ask
those owners to either delete and re-fork, or on their fork use **Sync fork → Discard commits** (this resets their
`main` to the new history and drops any of their own commits). GitHub does not give out fork owners' contact details.

Re-check right before the request:

```bash
gh api repos/CrestApps/CrestApps.OrchardCore/forks --paginate --jq '.[] | "\(.owner.login) \(.default_branch)"' | while read -r o b; do echo "$o:$b $(gh api "repos/CrestApps/CrestApps.OrchardCore/compare/6f21a29032dce5148f1352e2dc5a12c5b3587871...$o:$b" --jq .status 2>/dev/null)"; done
```

`ahead` or `identical` means that fork still holds the old history. After the push the old commit lives only in forks
and in `refs/pull/*`, so this check keeps working.

## Release tags and release branches

No tag contains any removed path, so the partial rewrite leaves all 53 tags, their GitHub Releases, their signatures and
the NuGet packages built from them untouched. Same for `release/1.1` to `release/2.2`. Release notes and issues that
quote commit ids from before #569 stay valid; links to commits on `main` from #569 onward will 404 once Support
garbage-collects (until then they still resolve, on the old history). Nothing in the build embeds commit ids
(no SourceLink).

## Local clones and worktrees

Every clone made before the push still has the videos and can put them back: pushing a branch that was cut from the old
history re-uploads the whole old history with it. That is the main way a purge gets undone.

The main checkout at `C:\Code\CrestApps\CrestApps.OrchardCore` has (on 2026-10-17) 24 worktrees, 128 local branches and 6
stash entries. All 24 worktrees sit on the rewritten part of history; 15 have unpushed commits and 4 have uncommitted
changes. Other clones (other folders, other machines, Codespaces, CI caches) need the same treatment or must be
deleted.

Two options:

- **Re-clone** and recreate the worktrees you still need. Simplest and leaves nothing behind, but every Claude session
  bound to a worktree path loses its folder.
- **Move each worktree in place** (below). It uses the `commit-map` filter-repo writes, which maps every old commit id to
  its new one, so each branch is moved onto exactly the rewritten version of its own base: no new upstream changes get
  pulled in, and nothing is merged.

For each worktree, after `git fetch origin --prune` (run from the main checkout once):

```bash
MAP=/c/purge/commit-map   # copied from oc.git/filter-repo/commit-map
ZERO=0000000000000000000000000000000000000000
remap() {
  local c new old=
  for c in $(git rev-list HEAD); do
    new=$(awk -v c="$c" '$1 == c { print $2; exit }' "$MAP")
    if [ -n "$new" ] && [ "$new" != "$ZERO" ]; then old=$c; break; fi
  done
  if [ -z "$old" ]; then echo "untouched: $(git rev-parse --abbrev-ref HEAD)"; return; fi
  if [ "$old" = "$(git rev-parse HEAD)" ]; then
    git reset --keep "$new"                    # fully pushed: jump to its rewritten commit
  else
    git rebase --autostash --onto "$new" "$old" # unpushed commits: replay them on the rewritten base
  fi
}
git worktree list --porcelain | sed -n 's/^worktree //p' | while read -r w; do (cd "$w" && echo "== $w" && remap); done
```

The walk stops at the newest commit that existed on GitHub, so stacked branches move onto their rewritten parent branch.
A commit that only added videos maps to all zeros (filter-repo dropped it), and the walk skips it. `reset --keep` keeps
uncommitted changes, which is safe here because the old and new tips differ only in deleted media.

Then:

- Branches not checked out in any worktree: for each one whose tip is in `commit-map`, `git branch -f <name> <new>`;
  rebase or delete the rest. Delete stale branches rather than moving them.
- Stashes: `git stash show -p` each of the 6 entries into a patch file, drop them, re-apply later if needed. (The stash
  stack is shared by every worktree and every session.)
- Only when every worktree and branch is moved: `git reflog expire --expire=now --all && git gc --prune=now` in the main
  checkout. Until then the old objects stay on disk, which is harmless but keeps the size.
- Never `git pull` (merge) an old branch into a new one; a merge commit brings the old history back. Rebase only.

Stop every Claude session and other automation that pushes to this repository before the push, and do not restart any
until its worktree has been moved: a session that pushes a new branch from an old base re-uploads the videos.

## Prerequisites

1. **#925 is merged** (done) and **#814 has switched to YouTube embeds**, so no open work still needs a video file.
2. **CrestApps.Videos no longer reads OC history.** The `reel.json` files of the walkthrough reels and the
   `commerce`/`tenant-hierarchy` READMEs point at OC commit `ef4a3177a`, which disappears. Copy the source screencasts
   they need somewhere private (not a public repo: the raw files are the unredacted ones) and repoint them first.
3. **The `mcp-integration.gif` check** above is done (done).
4. **Open PRs merged or closed** as far as practical.
5. A **quiet window**: no one else pushing, no sessions running.

## Runbook

Run in Git Bash on a short path (`C:\purge`): the repository has paths long enough to fail on Windows' 260-character
limit otherwise.

### 1. Tools and a fresh mirror

```bash
python -m pip install --user git-filter-repo==2.47.0
git filter-repo --version   # 2.47+ is required for --sensitive-data-removal
mkdir /c/purge && cd /c/purge
git -c core.longpaths=true clone --mirror https://github.com/CrestApps/CrestApps.OrchardCore.git oc.git
git -C oc.git config core.longpaths true   # before the copy, so the backup can open its own pack too
cp -r oc.git oc-backup.git   # the rollback copy; it holds the personal data, keep it local and delete it at the end
cd oc.git
git remote set-url --push origin NO-PUSH-UNTIL-STEP-5   # nothing can be pushed by accident
```

If `git filter-repo` is not found, call the script directly: `python -I <site-packages>/git_filter_repo.py ...`.

### 2. List the paths, the affected refs and the boundary

```bash
D=src/CrestApps.Docs/static/img/docs
git log --all --format= --name-only -- "$D" | sort -u \
  | grep -E "^$D/[^/]+\.(mp4|webm|vtt|jpg|gif)$" \
  | grep -vE "/(admin-ui-sample|mcp-integration|widget-ui-sample)\.gif$" > ../purge-paths.txt
wc -l < ../purge-paths.txt          # about 235

git for-each-ref --format='%(refname)' | while read -r r; do
  [ -n "$(git log -1 --format=%h "$r" -- $(cat ../purge-paths.txt))" ] && echo "$r"
done > ../purge-refs.txt
wc -l < ../purge-refs.txt           # about 385; no refs/tags/ and no refs/heads/release/ lines expected
grep -E '^refs/(tags|heads/release)/' ../purge-refs.txt   # must print nothing; stop if it does

FIRST=$(git log main --reverse --format=%H -- $(cat ../purge-paths.txt) | head -1)
git log -1 --oneline "$FIRST"       # expect 6f21a2903 Add Screencasts (#569)
{ git for-each-ref --format='^%(refname)' refs/tags; echo "^$FIRST~1"; } > ../keep.txt

git log --all --format= --raw --no-abbrev -- $(cat ../purge-paths.txt) \
  | awk '{ print $4 }' | grep -v '^0\{40\}$' | sort -u > ../purge-blobs.txt   # for the check in step 4
```

### 3. Rewrite

```bash
git filter-repo --sensitive-data-removal --replace-refs delete-no-add --invert-paths \
  --path-glob "$D/*.mp4" --path-glob "$D/*.webm" --path-glob "$D/*.vtt" --path-glob "$D/*.jpg" \
  --path-regex '^src/CrestApps\.Docs/static/img/docs/(?!(admin-ui-sample|mcp-integration|widget-ui-sample)\.gif$)[^/]+\.gif$' \
  --refs $(cat ../purge-refs.txt) $(cat ../keep.txt)
git reflog expire --expire=now --all && git gc --prune=now
```

For videos only (keeping the retired GIFs), drop the `--path-regex` line. Save the output: it prints the First Changed
Commit(s) for the Support request.

### 4. Verify (still nothing pushed)

```bash
git count-objects -vH | grep size-pack                       # about 68 MiB
while read -r b; do git cat-file -e "$b" 2>/dev/null && echo "STILL PRESENT $b"; done < ../purge-blobs.txt   # must print nothing
git for-each-ref refs/replace | wc -l                        # 0
[ "$(git -C ../oc-backup.git rev-parse 'main^{tree}')" = "$(git rev-parse 'main^{tree}')" ] && echo "main tree unchanged"
diff <(git -C ../oc-backup.git for-each-ref --format='%(objectname) %(refname)' refs/tags) \
     <(git for-each-ref --format='%(objectname) %(refname)' refs/tags) && echo "tags unchanged"
grep -c '^refs/pull/.*/head$' filter-repo/changed-refs      # PR count for Support; stop if far above ~330
grep -vc '^refs/pull/' filter-repo/changed-refs             # branches to push, about 36
git -c core.longpaths=true clone -q --bare --no-local . ../check.git && git -C ../check.git count-objects -vH | grep size-pack   # about 54 MiB
```

Also spot-check that `git log --oneline -3 main` shows the latest merges with their titles, and that a recent branch
still builds.

### 5. Push the changed branches

Disable the ruleset "Protect main and releases" (Settings → Rules → Rulesets → Enforcement status: Disabled), then:

```bash
git remote set-url --push origin https://github.com/CrestApps/CrestApps.OrchardCore.git
git config --unset remote.origin.mirror   # a mirror remote refuses explicit refspecs
awk 'NR > 1 && $1 != $2 && $3 ~ /^refs\/heads\// { print "--force-with-lease=" $3 ":" $1 }' filter-repo/ref-map > ../leases.txt
awk 'NR > 1 && $1 != $2 && $3 ~ /^refs\/heads\// { print $3 ":" $3 }' filter-repo/ref-map > ../refspecs.txt
wc -l < ../refspecs.txt              # about 36
git push --dry-run --atomic origin $(cat ../leases.txt) $(cat ../refspecs.txt)
git push --atomic origin $(cat ../leases.txt) $(cat ../refspecs.txt)
```

Re-enable the ruleset immediately. If the push is rejected because a lease failed, someone pushed since the clone: start
again from step 1 rather than forcing.

### 6. After the push

1. `git clone` into a new folder and confirm the size (about 54 MiB) and that
   `git log --all --format= --name-only -- 'src/CrestApps.Docs/static/img/docs/*.mp4'` prints nothing.
2. Move every local worktree and clone (section above). Copy `oc.git/filter-repo/commit-map` to `C:\purge` first.
3. Contact fork owners, re-check forks, then open the Support request.
4. When Support confirms, check that an old video URL by commit id (from `oc-backup.git`) returns 404, then delete
   `oc-backup.git`, `oc.git` and `C:\purge`.

## Rollback

Until Support has garbage-collected, the old commits still exist on GitHub. To undo, re-disable the ruleset and push the
old ids back from `oc-backup.git` with the same lease-and-refspec pattern (leases on the new ids, refspecs from the
backup). After garbage collection there is no rollback, which is why the backup stays local until the very end.

## Checklist for Mike

- [ ] Decide: videos only, or videos + retired GIFs (recommended).
- [ ] Decide: accept that ~330 merged PRs lose their diff views when Support purges (required to remove the personal
      data from GitHub; skipping Support only shrinks clones).
- [x] Check `mcp-integration.gif` for personal data (none found).
- [ ] #814 switched to YouTube embeds; ready PRs merged; the rest noted for re-review.
- [ ] CrestApps.Videos sources moved off OC commit `ef4a3177a` to private storage, and repointed.
- [ ] Every Claude session and other automation that pushes to this repo stopped.
- [ ] Steps 1–4 run; every check passes; tag check prints "tags unchanged".
- [ ] Ruleset disabled → step 5 push → ruleset re-enabled (check its status in Settings).
- [ ] Fresh clone is about 54 MiB with no MP4 in history.
- [ ] Every worktree, local branch and stash moved or dropped; old reflogs expired and gc run.
- [ ] Other clones (other machines, Codespaces) deleted or moved.
- [ ] Fork owners contacted; fork check shows none `ahead`/`identical`.
- [ ] Support request sent with PR count and First Changed Commit(s).
- [ ] After Support: old URLs 404; backup and `C:\purge` deleted.
- [ ] For a few weeks: a fresh clone still has no MP4 in history (a push from an old base would bring them back).

## Follow-up

`check:manuals` only checks the files in the tree. A guard that fails a PR when any commit in its range adds a video
file would catch a branch pushed from an old base before it is merged.
