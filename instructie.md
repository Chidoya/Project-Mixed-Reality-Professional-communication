# Claude Code Workflow — Professional Communication VR

> Project: Professional Communication VR (Unity)  
> Developers: Jalil and Liam  
> Current long-lived branches: `main`, `jalil`, `liam`  
> This file defines the expected Git workflow for Claude Code. Repository-specific rules and Unity version must be verified before applying commands.

## 1. Branch roles and ownership

- `main`: stable shared integration branch. **Only humans approve and merge work into `main` through Pull Requests.** Claude must never commit, push or merge directly into `main`.
- `jalil`: Jalil's long-lived personal integration/baseline branch. Jalil can prepare scenes, assets, prefabs and other baseline changes here.
- `liam`: Liam's long-lived personal integration/baseline branch, serving the same purpose for Liam.
- `feature/<owner>/<short-kebab-case-name>`: short-lived feature branch created **from the current owner's personal branch**, e.g. `feature/jalil/dialogue-system` or `feature/liam/vr-interaction`. The owner segment avoids collisions when both developers work on similarly named features. If the team insists on `feature/<name>`, first check that the branch name is globally unique.
- `fix/<owner>/<name>`, `docs/<owner>/<name>`, `chore/<owner>/<name>`: similarly scoped temporary branches.

**Normal flow:** `origin/jalil` -> `feature/jalil/<feature>` -> PR targeting `jalil` -> human-approved merge into `jalil` -> later human-created/reviewed PR `jalil` -> `main`. The same applies to `liam`. The personal branch is the source of truth for that developer's current baseline, not `main`.

**Important:** A feature branch based on `jalil` must **not** open a PR directly into `main`, because it may include unrelated changes from `jalil`. First integrate into its own personal branch. Human developers decide what is ready to promote to `main` and handle any overlapping or conflicting work.

## 2. Mandatory start-of-task procedure

**At the beginning of every new task/session, Claude MUST ask:**

> Who is working right now: `jalil` or `liam`? Which feature or change should I work on?

Do not infer the user from the operating-system username, Git author, current branch, previous chat or account. Even if the current branch is `jalil`, explicitly confirm who is working. If the user already answered **in this task**, do not ask repeatedly. If neither developer is selected, do not modify repository files.

After the developer identifies themselves:

1. Confirm the selected personal integration branch (`jalil` or `liam`), task scope, likely affected files, and acceptance criteria.
2. Inspect `git status --short --branch`, `git branch -vv`, `git remote -v`, and `git branch --show-current`. Verify the expected Git remote; never assume it is correct.
3. Protect any existing work: if the worktree has uncommitted/untracked changes, stop and ask how to preserve them. Never silently reset, discard, overwrite or stash work.
4. With network permission, run `git fetch origin --prune`. Verify `origin/<owner>` exists. If fetching fails, explain that the remote baseline cannot be verified and ask whether to continue offline.
5. Check whether local `<owner>` and `origin/<owner>` have diverged. Do **not** blindly `git pull`. If the local branch is clean and merely behind, a fast-forward-only update (`git switch <owner>` then `git merge --ff-only origin/<owner>`) is allowed after confirming no unrelated work is at risk. If diverged, stop and ask how to reconcile it.
6. Use `origin/<owner>` (not `origin/main`) as the new feature branch's base. Example: `git switch -c feature/jalil/dialogue-system origin/jalil`. If the name already exists, inspect it and ask whether to resume it or choose a new unique name; never overwrite a branch.
7. Confirm `git branch --show-current` reports the intended feature/fix/docs/chore branch before editing.

**Baseline changes:** If the developer wants to add assets or adjust their baseline first, discuss whether these belong in a separate `chore/<owner>/baseline-assets` branch with a PR into `<owner>`, or whether the developer will commit them manually to their own personal branch. Claude must not silently change the baseline. Start feature work only after the required baseline is committed and available on the correct personal branch.

Use a separate Git worktree when necessary to avoid disturbing an existing Unity working tree, but first explain its path and check it is safe.

## 3. Development rules

- Keep each branch focused on one coherent change; do not mix unrelated features.
- Prefer small, reviewable commits. Use commit messages such as `feat: add dialogue choice UI`, `fix: prevent duplicate dialogue trigger`, or `docs: document headset setup`.
- Explain architectural changes before implementing them.
- Do not introduce dependencies, packages, paid services, or Unity version upgrades without approval.
- Avoid editing the same Unity scene, prefab, or asset simultaneously on multiple branches; coordinate ownership in Notion.
- Do not commit secrets, credentials, tokens, personal information, or real patient data.
- Do not change Git remotes, repository settings, protection rules, or Git hooks without permission.

## 4. Unity repository hygiene

- Use a Unity-specific `.gitignore`. Track `Assets/`, `Packages/`, `ProjectSettings/`, and relevant `.meta` files.
- Exclude generated folders such as `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, and `Logs/` unless a reviewed project-specific exception exists.
- Preserve `.meta` files and their GUIDs; never regenerate or delete them casually.
- Use Unity's **Visible Meta Files** and **Force Text** serialization where appropriate for collaboration.
- Consider Git LFS for large binary files (for example audio, videos, textures, models and packaged builds) according to repository limits and team agreement. Do not automatically migrate existing history into LFS.
- Unity scenes, prefabs, and project settings can conflict even when text-based. Resolve conflicts carefully; do not blindly accept one side or automatically rewrite YAML.
- Avoid committing exported APKs, large builds, or third-party asset bundles unless explicitly agreed.

## 5. Commit policy

Before every commit:

1. Review `git diff` and `git status`.
2. Stage only files belonging to the current task (`git add <specific paths>` preferred over `git add .`).
3. Run relevant available checks, such as Unity compilation, Edit Mode tests, Play Mode tests, or project-specific validation. If Unity cannot be run in the agent environment, explicitly state that validation is incomplete.
4. Summarize the changes and any known risks.
5. Commit to the current feature/fix/docs/chore branch only.

Never use `git commit --amend` on already-pushed commits unless the user explicitly approves.

## 6. Push policy

- Claude **must request explicit approval before every push**, stating the local source branch and remote destination.
- Claude may push **only its own temporary task branch** after approval, e.g. `git push -u origin feature/jalil/dialogue-system`.
- Claude must never push directly to `main`, `jalil`, or `liam`. Only human developers promote work to personal branches and from personal branches to `main` through reviewed PRs.
- Never force-push (`--force` or `--force-with-lease`) or delete remote branches automatically.
- Never push secrets, unexpected large binaries, generated Unity folders or unrelated files.
- If a push is rejected, inspect and report the cause; do not rewrite history to bypass it.

## 7. Pull / sync policy

- `git fetch origin` is the preferred safe synchronization operation; it does not modify the current working tree.
- **Never blindly `git pull`**, especially with local modifications. Check status and whether the branches diverged first.
- The owner's personal branch is the feature's baseline. Before creating a feature, ensure `origin/<owner>` is up to date and use it as the base.
- Before opening a PR to the personal branch, fetch and compare against `origin/<owner>` (not `origin/main`). If needed, merge the latest `origin/<owner>` into the feature branch, with approval and after reviewing potential Unity conflicts.
- For published feature branches, avoid rebasing or rewriting shared history. For unpublished local branches, discuss rebase before using it.
- When a personal branch needs updates from `main`, the **human owner** decides when and how to integrate `main` into their personal branch. Claude may advise and prepare a plan, but must not silently merge `main` into the baseline.
- Never auto-resolve Unity scene, prefab, asset GUID or project settings conflicts; report them for human review and revalidate afterward.

## 8. Pull Request and promotion policy

**Stage A — Feature to personal branch**

1. Claude completes and validates the feature on `feature/<owner>/<name>`.
2. Claude requests approval before pushing that temporary branch.
3. With authorization, Claude may draft/open a PR whose **base is `<owner>`**, never `main`.
4. The human owner (preferably with a review from the other developer) reviews and merges the PR into their own branch. Claude must not merge it.
5. The personal branch now contains the new feature plus the owner's baseline and assets.

**Stage B — Personal branch to `main`**

1. Jalil or Liam decides when their personal branch is ready to integrate into `main`.
2. A human opens/reviews a PR from `jalil` or `liam` to `main`, checks its **entire diff**, and resolves conflicts. Be aware that personal branches may contain several features and unfinished baseline work; do not promote them until all included changes are intended for `main`.
3. At least one human review and all configured checks should pass before a human merges into `main`.
4. Claude may assist with a diff summary, tests and PR description, but must not merge or push to `main`.

Each PR description should state purpose, source/base branches, affected Unity scenes/assets, linked Notion task or GitHub issue, tests performed, screenshots/demo if relevant, and known limitations. Do not claim checks passed unless they actually ran.

**Optional safer release alternative:** If a personal branch contains unfinished work, create a dedicated human-reviewed release branch or cherry-pick only approved commits into a PR targeting `main`, rather than merging the whole personal branch. Cherry-picking Unity asset changes can require special care with `.meta` files and dependencies.

## 9. Suggested GitHub repository settings (human setup)

Configure a ruleset or branch protection for `main`, and preferably for `jalil` and `liam` too:

- Require pull requests before merging into `main`; ideally also require PRs into `jalil` and `liam` for agent-developed features.
- Require at least one approval from a reviewer other than the author, where available.
- Require status checks that actually exist in the repository (do not enable nonexistent checks).
- Block force pushes and branch deletion.
- Keep bypass permissions restricted.
- Optionally require conversations to be resolved before merging.

These are recommended settings, **not** claims that they are already enabled. Some protections depend on repository visibility, plan, and permissions.

## 10. Notion coordination

Project Hub: https://app.notion.com/p/3f7b4e82942c814299f4c9f681a5b10c

For each feature, maintain a Notion `Project Tasks` entry with:

- Task name and short acceptance criteria.
- Owner (`jalil`, `liam`, or another team member), the personal baseline branch, and the temporary feature branch.
- Status (`Not started`, `In progress`, `Done`).
- Priority, workstream, schedule, and link to the relevant GitHub issue/PR.
- A brief testing outcome or demo link when completed.

Notion is for team coordination, meeting notes, storyboards, deliverables, and progress. GitHub is the source of truth for Unity code and version history. Do not claim to have updated Notion unless the connected tool successfully did so.

## 11. End-of-task report

Claude must report:

1. Confirmed current developer, temporary branch name, and personal base branch.
2. Files changed and what was implemented.
3. Commits created (if any).
4. Tests run and results, including anything not testable.
5. Whether anything was pushed or a PR was opened (only with approval).
6. Any conflicts, blockers, or remaining actions.

**Default safety rule:** If uncertain about branch state, remote changes, destructive Git commands, credentials, or Unity asset conflicts, stop and ask instead of guessing.

## 12. Example conversation and commands

**Claude:** "Who is working right now: `jalil` or `liam`? What should I implement?"

**Developer:** "jalil — add a dialogue choice system using the assets already on my branch."

**Claude:** "I will check the repository, update my view of `origin/jalil`, create `feature/jalil/dialogue-choice-system` from it, and work only there. I will ask before pushing and will target `jalil` with the PR."

```bash
git status --short --branch
git fetch origin --prune
git branch -vv
# Only when the worktree is safe and the remote personal branch exists:
git switch -c feature/jalil/dialogue-choice-system origin/jalil
# Implement, validate and commit scoped changes on this branch.
# Push ONLY after explicit approval:
git push -u origin feature/jalil/dialogue-choice-system
```

The human developer reviews and merges the feature PR into `jalil`. At a later agreed integration point, the human developer reviews a separate PR from `jalil` to `main`.

## 13. Session startup checklist (mandatory)

- [ ] Ask whether the active developer is `jalil` or `liam` and confirm the task.
- [ ] Inspect current branch, working tree, remote and divergence.
- [ ] Fetch and confirm the correct personal remote baseline.
- [ ] Create a uniquely named temporary branch **from the personal branch**.
- [ ] Implement and validate only the requested scope.
- [ ] Request permission before pushing the temporary branch.
- [ ] Open a PR targeting the **personal branch**, if authorized.
- [ ] Leave the final merge to the human developer.
- [ ] Never autonomously promote work into `main`.
