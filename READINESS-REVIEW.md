# Implementation status

Updated September 15, 2026. The original assessment below is retained as the audit trail; statements there about missing features describe the pre-implementation application.

| Item | Implementation / verification |
| --- | --- |
| Behavioral framing | Contextual choice language, evidence counts with supporting answers, explicit limits on effectiveness claims |
| Expanded labs | 20 tests / 154 questions; creative revision plus connected learning, teaching, and leadership sequences |
| Storage reliability | Transactional writes, visible failures, unsaved-attempt export, damaged-data recovery, explicit legacy migration |
| Backup validation | Required fields, schema version, nulls, unique IDs, references, answer bounds, score consistency, atomic merge and pin union |
| Person ownership | Attempt owner captured at start, including Growth planning; header switches do not transfer the attempt |
| Historical meaning | Question snapshots and Growth baselines; incompatible legacy answers retained but excluded from current interpretation |
| Scoring contract | Weighted picks documented and regression-tested; original lifeboat score preserved |
| Catalog recovery | Runtime semantic validation and retry after failure; visible page-level recovery |
| Privacy | System fonts, no third-party font requests, path-specific storage key, About page explaining origin and profile limits |
| Repository | Git initialized, MIT license, contribution/security/attribution notes, formatting settings, SDK pin and lock files |
| Automation | PR/Release validation, C# checks, all-test browser smoke suite, axe checks, dependency audit, Dependabot, pinned Actions |
| Pages | Production subpath, custom 404 shell, published-only artifact, least-privilege deployment; owner enabled Actions source and HTTPS |
| Verification | 534 C# assertions passed; all 20 tests completed in Chromium; deep links, reload, history, owner switch, backups, mobile light/dark and axe checks passed; no known NuGet/npm vulnerabilities |

See [deployment instructions](docs/DEPLOYMENT.md), [content rules](docs/CONTENT.md), and [repeatable verification](CONTRIBUTING.md). Screenshots and synthetic backups are under ignored `artifacts/` locally, never committed.

Remaining owner/maintenance items: enable or verify branch/environment protection, secret scanning/push protection, Dependabot security alerts, and private vulnerability reporting in repository settings. Confirm inherited content/asset provenance in `THIRD-PARTY-NOTICES.md`. Automated accessibility checks and keyboard/browser smoke tests are not a full screen-reader or WCAG conformance audit. Pages deep links deliberately return HTTP 404 while loading the app shell. These hosting and interpretation limits are documented in the app and deployment guide.

---

# Original readiness review

Reviewed September 15, 2026. Scope: source review, Release publication, dependency vulnerability audit, and JSON catalog consistency checks. No application code was changed, no Git repository was initialized, and nothing was published. Browser interactions and a live GitHub Pages deployment have not been tested.

**Verdict:** The standalone .NET 10 Blazor WebAssembly architecture fits GitHub Pages. Source is ready to be placed under version control, but the current output needs deployment configuration before it will work reliably as a Pages project site. Address the data reliability findings before inviting people to rely on saved results.

## Verification results

| Check | Result |
| --- | --- |
| `dotnet publish -c Release --nologo` | Passed using SDK 10.0.300; no compiler errors reported |
| Publish output | `bin/Release/net10.0/publish/wwwroot`, approximately 15.83 MiB including compressed assets |
| WebAssembly tooling | Publish completed without the optional `wasm-tools` native optimization workload |
| Dependency audit | `dotnet list package --include-transitive --vulnerable --no-restore` reported no known vulnerable packages from NuGet |
| Catalog | 19 tests: 9 philosophy, 9 behavior, 1 growth; 144 questions; 18 dimensions; 8 contexts |
| Catalog consistency | One out-of-range effect; checked duplicate IDs, manifest files, question types, dimension/context references, scale ranges, pick counts, allocation configuration, and assignment effect option references |
| Git | This directory is not a Git repository |
| Automation | No CI/Pages workflow or automated test project found |
| Source credentials | No obvious embedded credentials found in the reviewed source; this is not a comprehensive secret scan |

## Requirements for GitHub Pages

1. **Create the repository and initial commit.** Choose the owner, repository name, visibility, and default branch. The existing `.gitignore` excludes `bin/`, `obj/`, `.vs/`, and `*.user`. Commit source, not build output. Inspect the staged files for credentials and personal backups before the first push.
2. **Configure the production base path.** `wwwroot/index.html:10` currently uses `<base href="/" />`. A project URL such as `https://OWNER.github.io/PhilosophyTester/` requires `/PhilosophyTester/`, including the trailing slash and exact case. Set this in the deployment build/output while retaining `/` for local development. Account-root sites and custom domains can have a different base path. Relative navigation and catalog requests already support a correctly configured base.
3. **Handle direct navigation and refresh.** The app routes include `/test/{TestId}`, `/profile`, `/growth`, `/saved`, and `/results/{ResultId}`. Pages does not provide the development server's SPA fallback. Supply a Pages-compatible `404.html` strategy, such as serving the correctly based app shell as the custom 404 page, and verify it. That approach still returns HTTP 404 for initial deep-link requests; a redirect-and-restore approach is another option. The Razor `NotFound` page alone cannot handle a request that never reaches Blazor.
4. **Add a GitHub Actions publishing workflow.** Install the .NET 10 SDK, restore, validate, publish Release, set the production base path, prepare route fallback, and upload **only the published `wwwroot`** using the Pages artifact action. Deploy that artifact using `actions/deploy-pages`. The deployment job needs `pages: write`, `id-token: write`, the `github-pages` environment, and a dependency on the successful build job; checkout needs `contents: read`. Restrict deployment to the intended branch and serialize deployments. Do not deploy from pull requests.
5. **Enable Pages in repository settings.** Set Settings → Pages → Source to GitHub Actions. Public repositories can use Pages on GitHub Free; private repository availability depends on the plan. Enable HTTPS. A private source repository does not automatically make the published website private.
6. **Verify the deployed artifact.** Check the project root, direct deep links, refresh, query strings, back/forward navigation, JSON requests, WebAssembly loading, and console errors. In particular, `_framework` assets must be included. The Actions artifact approach avoids Jekyll processing; branch-based publication would need `.nojekyll` for these assets.

GitHub Pages is static hosting, so it cannot supply a server-side database or protect API secrets embedded in this application. This app currently needs neither. Its output is comfortably below the documented 1 GB site limit. Results remain in each browser and are not synchronized through GitHub.

References: [Microsoft's Blazor Pages deployment guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/github-pages?view=aspnetcore-10.0), [Blazor base paths](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/app-base-path?view=aspnetcore-10.0), [GitHub Pages workflows and permissions](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages), [Pages limits](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits).

## Application findings

### High priority: saving can silently lose data

`Services/DeviceStore.cs:137` catches storage write exceptions and continues as though persistence succeeded. A full or blocked `localStorage` can therefore leave the user seeing saved results that disappear on reload. Expose an explicit temporary-storage state and a save-failure message, with an export option. Read failures also silently fall back to memory.

### High priority: backup JSON is deserialized without validating its structure

`Services/DeviceStore.cs:120` accepts any JSON that deserializes to `DeviceData`. Examples from code inspection: `{}` is accepted as an empty backup, and `{"people":null}` reaches a null dereference. Missing references, unsupported schema versions, null nested collections, and invalid answers/tallies are not rejected. Duplicate result IDs within one import can be added together. `Pages/SavedResults.razor` only catches format and I/O errors, so structural failures can escape into the UI.

Validate the entire backup before mutating existing data: schema version, required collections, unique IDs, person/result references, answer values, and finite/ranged tallies. Preserve the original data if validation or persistence fails. On loading storage, repair or reject an active person ID that does not exist; `EnsurePerson` currently handles a null ID but not a stale non-null ID.

### High priority: changing the active person can misattribute a test

`Pages/TakeTest.razor:87` captures the displayed person's name when the test loads, but `Services/DeviceStore.cs:55` assigns the result to whoever is active at save time. The layout's person switcher remains available during a test. Start as person A, switch to B, and finish: answers are saved to B despite the test having begun for A. Bind the attempt to a person ID and define how switching during an attempt behaves. Personalized Growth questions also belong to the original person's baseline.

### Medium priority: scoring data conflicts with its documented contract

`wwwroot/data/tests/lifeboat.json:24` gives the friend option `"partiality": 3`, while the README specifies effects between -1 and 1. The pick calculation averages effects and clamps the final contribution, so this may be intentional weighting, but it is an undocumented exception. Resolve the scoring contract before enforcing schema rules or adding more tests; do not automatically change it to 1 without considering the intended score.

### Medium priority: existing answers can change interpretation after test updates

`Services/BehaviorAnalyzer.cs:50` scores historical answers against the current question definitions without checking `TestVersion`. Meanwhile belief profiles use stored tallies, and result pages display current question/option text with a version notice. Changing question meanings or effects can therefore reinterpret old behavior results and make the two labs inconsistent. Establish a policy: immutable versioned definitions, answer/question snapshots, or explicitly excluding incompatible old results. Keep IDs stable. Growth history also uses the current baseline, so historical adaptability is a current reassessment, not a frozen measurement.

### Medium priority: catalog loading has no recovery path

`Services/TestCatalog.cs` caches its load task. A failed JSON request faults the task for the rest of the app session, and the pages do not provide a catalog-specific retry experience. Validate every catalog file in CI and add a clear load failure/retry path.

### Before public use: clarify storage, privacy, and interpretation

The source uses local browser storage for answers, but Google Fonts introduces third-party requests. State that distinction clearly or self-host the fonts. People on one browser are organizational profiles, not access-controlled accounts. Browser storage is scoped to the origin, not the repository path: sibling project sites under the same `OWNER.github.io` share that origin, and this app uses a fixed storage key. Moving domains does not move saved data; export/import is needed.

Present profiles as reflection tools. No validation study or calibration evidence was found in the repository to support treating these scores as validated psychological assessments.

## Repository quality recommendations

These are useful release practices, not mandatory GitHub upload requirements:

- Choose a license if reuse is intended; add attribution for any third-party content/assets. GitHub does not require a license simply to host a repository.
- Add `global.json` and a dependency lock/restore policy for reproducible builds. Keep framework patches current and enable dependency update/security tooling.
- Add PR validation that builds/publishes and validates the catalog. Add targeted automated coverage for scoring and persistence as the app grows.
- Expand README deployment instructions with the actual site URL, Pages base path, fallback behavior, backup limitations, and content-versioning policy.
- Use consistent formatting/analyzers and branch protection once contributions begin. CONTRIBUTING/SECURITY files are optional at this stage.
- Accessibility has good foundations: labels, fieldsets, focus transfer to questions, skip navigation, and reduced-motion handling. Keyboard behavior, focus visibility, contrast, zoom, and mobile layouts still need browser verification; this review does not establish WCAG conformance.

## Acceptance checklist before launch or additional content

- Complete each of the six question types; verify expected scores and navigation back to previous answers.
- Verify person switching during a test, result ownership, removal, and persistence after reload.
- Exercise blocked/full storage, malformed backups, duplicate imports, stale person IDs, and unsupported backup versions.
- Export/import a real backup and confirm people, notes, pins, and results survive as intended. Imports currently keep an existing person's record rather than merging incoming pins.
- Test updated test versions against historical results and choose the intended interpretation policy.
- Test the Release artifact under the actual repository subpath, including direct URLs and refresh.
- Test keyboard-only usage, mobile widths, screen-reader announcements, and both color schemes.

For the planned new thought experiments, JSON-based content is a useful extension point. Establish a catalog schema, scoring examples with expected outcomes, and the versioning policy first so new tests can be checked consistently.
