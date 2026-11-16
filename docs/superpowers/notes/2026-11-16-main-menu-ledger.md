# SDD ledger — plan: docs/superpowers/plans/2026-11-16-main-menu.md

Setup: project is not a git repository (confirmed). No worktree/commits; executing in place.
Ruling: use a manual ledger in `docs/superpowers/notes/` instead of `.superpowers/sdd/` scripts — the
scripts require a git repo, and the plan itself says verification checkpoints replace commits.
Pre-flight: shared interfaces are consistent (MainMenuController only in Tasks 1–2; build order in
Task 2; startSinglePlayer only in Task 3).

Setup update: user asked for git mid-task. Added `.gitignore` and committed the baseline at `3cdb86d`.
Ruling: the Task 1 RED files (MainMenuTests, test asmdef) are inside that baseline commit — the user
asked for git after they were written. From here each task gets its own commit.

Task 1: complete — MainMenuController + 3 PlayMode tests (`MenuBuildsTheFiveExpectedButtons`,
`SettingsClickKeepsTheCurrentScene`, `MenuReusesAnExistingEventSystem`). Command:
`Unity -runTests -testPlatform PlayMode -testFilter AnimeFpsGame.Tests.MainMenuTests` → Passed 3/3, exit 0.

Task 2: complete — `ProjectSetup.SetupMainMenu()`, generated `MainMenu.unity`, build order
`MainMenu` (0) / `Main` (1). Same test command after generation → Passed 5/5, exit 0.



