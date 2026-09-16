# Thought Experiments (PhilosophyTester)

A Blazor WebAssembly (.NET 10) app of philosophy thought experiments. People pick the tests
they want, answer them, and build a profile of how they reason. Everything is stored in the
browser's `localStorage` on that device — no server, no accounts.

## Run and verify

Requires the .NET SDK selected in `global.json`.

```sh
dotnet restore --locked-mode
dotnet run
dotnet run --project tests/PhilosophyTester.Checks.csproj -c Release
dotnet publish -c Release -warnaserror -o artifacts/publish
```

Repository: [N8DaGr822/PhilosophyLab](https://github.com/N8DaGr822/PhilosophyLab).
Intended Pages address: https://n8dagr822.github.io/PhilosophyLab/ (available after successful deployment).
See [deployment instructions](docs/DEPLOYMENT.md) for the production base path, 404 fallback, and repository settings.
See [contribution checks](CONTRIBUTING.md) for browser and accessibility verification, and [content rules](docs/CONTENT.md) for schema, scoring, and historical compatibility.

This is a reflection tool for fictional scenario choices, not a personality test or a validated measure of behavior or ability. Reports include selected approaches and their supporting answers. Suggested alternatives in Growth are authored comparisons, not proof of success.

## Three layers

| Layer | Question | Where |
|-------|----------|-------|
| Beliefs lab | What do you believe? | Tests → Beliefs, Profile → Beliefs |
| Behavior lab | What would you choose? | Tests → Behavior, Profile → Behavior |
| Growth | Can you adapt? | Growth |

## What's in it

- **Beliefs lab, 9 tests**: family priority, trolley variations, sacrifice curve, loyalty, fair shares,
  lifeboat, freedom and surveillance, the happiness machine, robot rights.
- **Behavior lab, 10 tests**: how you learn, how you teach, team roles, leading a team,
  conflict, feedback, priorities and ownership, problem solving, mentor or manager, creative revision.
- **Behavior profile**: strong tendencies, situational tendencies ("you get more directive when
  time is short"), evidence counts, suggested alternatives, and preferences on 10 axes.
- **Growth**: a personal challenge set of situations that call for the opposite of your usual style,
  with a history of which suggested alternatives you selected.
- **Belief vs. behavior checks**, e.g. choosing honesty in the beliefs lab but going along with a
  misleading client update under deadline.
- **Test library** grouped by category, with a per-person "My list" of saved tests.
- **Several people per device** (Saved → People), each with their own results and profile.
- **Profile**: 8 axes, plain-language observations, and "where your answers pull apart"
  (cross-test consistency checks).
- **Result pages** with every answer and the optional "Why?" notes.
- **Backup**: export to JSON, import (merges, never overwrites).

## Project layout

```
Models/            TestDefinition, Question, Option, Answer, TestResult, DeviceData
Services/
  TestCatalog      loads wwwroot/data/*.json
  Scoring          answers -> per-dimension tallies
  DeviceStore      localStorage persistence, people, pins, export/import
  ProfileBuilder   beliefs profile, observations, tensions
  BehaviorAnalyzer usual style, situational shifts, adaptability
  ChallengePlanner picks the personal Growth set
  ConsistencyRules cross-test tension checks
  AppJsonContext   source-generated JSON (trim-safe)
Components/
  AxisBar, PersonSwitcher
  Questions/       QuestionView + one input per question type
Pages/             Home, TakeTest, ResultView, Profile, Growth, SavedResults
wwwroot/data/
  dimensions.json  the axes for both labs
  contexts.json    situation tags for the behavior lab
  tests/index.json list of test files to load
  tests/*.json     one file per test
```

## Storage and history

Names, answers, and optional notes stay in browser storage. Failed writes report an error and preserve existing data. Unsaved completed attempts can be exported without leaving the test. Imports validate the entire backup before committing, keep existing results, and merge saved-test pins. Use Saved to export, recover unreadable storage, or explicitly import older browser data. The older shared storage copy is retained.

New results contain a snapshot of their questions; Growth runs also retain their comparison baseline. Legacy results whose question version is unavailable are retained as raw answers and excluded from current interpretation. Keep dimension meanings stable or use new IDs.

People on one device are not private accounts. No fonts or analytics are fetched from third parties. See the app's About page for origin isolation and backup privacy limits.

## How the behavior lab reads your answers

Behavior questions can carry two extra fields:

- `contexts`: situation tags from `wwwroot/data/contexts.json` (`pressure`, `danger`, `novice`,
  `experts`, `ambiguity`, `authority`, `public`, `emotion`).
- `demands` + `need`: what the situation calls for, e.g. `{"teaching": -1}` with
  `"need": "a student needed direct, step-by-step instruction"`.

`BehaviorAnalyzer` then works in three steps:

1. **Usual style** = scores from every answered question that has no `demands`.
2. **Situational tendencies** = for each context, compare questions tagged with it to untagged ones.
   At least two responses per group and a difference of 0.5 are needed before reporting a contextual difference. These comparisons are tentative, not causal.
3. **Suggested alternatives** = for each `demands` question whose direction runs against your usual style
   (by 0.2 or more), did your answer score at least 0.3 in the suggested direction?
   Reports describe agreement with the suggested approach, not effectiveness.

`ChallengePlanner` builds the Growth set from `challenge.json` (`"lab": "development"`,
`"dynamic": true`): it prefers situations that oppose your usual style and haven't appeared in your
last run, and takes 6.

## Adding a test (no C# needed)

1. Create `wwwroot/data/tests/my-test.json`.
2. Add `"my-test.json"` to `wwwroot/data/tests/index.json`.

Each option or question can carry `effects`: dimension weights normally between -1 and 1. Pick option weights can have magnitude up to `pickCount`; final contributions are clamped to [-1, 1].
Negative pushes toward the dimension's `lowPole`, positive toward `highPole`.

| type         | What the user does                          | How it scores |
|--------------|---------------------------------------------|---------------|
| `choice`     | Picks one option                            | Chosen option's `effects` |
| `pick`       | Picks exactly `pickCount` options           | Sum of picked `effects` ÷ `pickCount` |
| `allocation` | Splits `points` across options (`step`)     | Each option's `effects` × how far above/below an equal share it got |
| `scale`      | Slider `min`–`max` with `minLabel`/`maxLabel` | Question `effects` × position (-1 at min, +1 at max) |
| `ladder`     | Picks the highest rung they'd accept (`options` ordered low → high), or `noneLabel` | Question `effects` × height (-1 none, +1 top rung) |
| `assign`     | Gives each of `rows` (tasks) to one of `options` (people) | Average of `rows[].effects[personId]` |

Set `"lab": "behavior"` on a test to put it in the behavior lab (the default is `philosophy`).

Set `"askWhy": true` on a question to show an optional "Why?" box.
Bump `version` when you change question meaning, options, effects, or sequences. New results retain their original snapshot; incompatible legacy results remain available as raw answers.

## Adding a dimension

Add an entry to `wwwroot/data/dimensions.json`. `lowSummary` / `highSummary` should read as
the end of a sentence starting "Often, " or "Consistently, ". Behavior dimensions also need
`"lab": "behavior"`, `lowShift` / `highShift` ("you get more directive"), and
`lowStrength` / `highStrength` ("Taking charge").

## Adding a consistency rule

Implement `IConsistencyRule` in `Services/ConsistencyRules.cs` and register it in `Program.cs`.
Give the `Tension` a lab: `Labs.Philosophy`, `Labs.Behavior`, or `Labs.Cross` (shown in both).
Rules receive the latest result per test id and look up answers by question id, e.g.
`r.Get("trolley", "spouse")?.ChoiceId`. Keep test and question ids stable once published,
since rules and saved results refer to them.

## Ideas for next steps

- More connected experiments using the creative-revision sequence format.
- Chart adaptability over time on the Growth page.
- More tests from the first doc: veil of ignorance (with random assignment at the end), desert island,
  $1 million, moral luck, Ship of Theseus, future generations.
- Compare two people's profiles side by side (the data is already per-person on the device).
- PWA manifest + service worker for offline use on tablets.
