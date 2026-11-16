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

Task 3: Ruling: the PlayMode tests now destroy the DDOL `NetworkManager` in `[UnityTearDown]` — NGO marks
it DontDestroyOnLoad, so the second test otherwise reused the first test's shut-down singleton. Test-only
isolation; product code unchanged. Cost if wrong: a future multi-load bug stays uncovered, but
return-to-menu is explicitly out of scope.
Task 3: complete — `NetworkBootstrap.startSinglePlayer` auto-hosts, `HostJoinMenu` removed from `Main`,
`Main.unity` regenerated. Command: `Unity -runTests -testFilter AnimeFpsGame.Tests.PlayerSmokeTests` →
Passed 2/2, exit 0.

Task 4: Ruling: preserve the documented `-host` / `-join` dev flags by having `MainMenuController.Start`
auto-enter Range when either flag is present; added `HasSessionCommandLine` with a RED→GREEN test. This
was a plan gap — with the menu as build index 0, the flags would otherwise be ignored until a click.
Cost if wrong: a dev passing the flag skips the menu, which is the intended dev behaviour.
Task 4: complete — notes updated; full PlayMode suite: `Unity -runTests -testPlatform PlayMode` →
Passed 8/8 (6 menu + 2 range), exit 0; no compiler errors or unhandled log messages.

Review fix pass (after the fresh-context reviewer found 4 Important, 0 Critical):
- Click paths: tests now invoke RANGE (loads `Main`), SETTINGS (asserts the placeholder log via
  `LogAssert.Expect`), and EXIT (through a `QuitHandler` test seam). Deleting an `AddListener` now fails.
- Dimming: `UnavailableButtonsAreVisiblyDimmed` asserts Campaign/Multiplayer labels are darker than RANGE.
- Build order: `ProjectSetup.ApplyBuildOrder()` is shared by `SetupScene` and `SetupMainMenu`; verified by
  removing the MainMenu entry, running `SetupScene`, and confirming MainMenu returned at index 0.
- Host failure: new `RangeNotice` uGUI banner is shown from `NetworkBootstrap.Host()` when `StartHost()`
  fails, naming the port. `HostFailureShowsAVisibleNotice` blocks UDP 7777 with `UdpClient` and expects the
  three Unity Transport bind-failure error logs explicitly.
- Menu test teardown now also shuts down and destroys the DDOL `NetworkManager` (Range click test).
Full suite: `Unity -runTests -testPlatform PlayMode` → Passed 12/12, exit 0.






