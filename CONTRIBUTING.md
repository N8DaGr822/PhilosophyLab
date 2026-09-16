# Contributing

Use the SDK selected by `global.json`. Run these from the repository root:

```sh
dotnet restore --locked-mode
dotnet run --project tests/PhilosophyTester.Checks.csproj -c Release
dotnet publish -c Release --no-restore -warnaserror -o artifacts/publish
python scripts/prepare-pages.py artifacts/publish/wwwroot --base-path /PhilosophyLab/
npm ci
npx playwright install chromium
npm run test:browser
```

On Windows PowerShell, use `npm.cmd` and `npx.cmd` if the execution policy blocks the `.ps1` wrappers. Do not weaken the machine's execution policy.

The browser suite starts its own local static server, uses a fresh browser context and synthetic answers, and saves screenshots under ignored `artifacts/`. The C# checks are a dependency-free executable that returns a nonzero exit code on failure; use `dotnet run` above, not `dotnet test`.

For new content, follow `wwwroot/data/test.schema.json` and `docs/CONTENT.md`. Add the file to the manifest. Keep IDs stable, increment the test version when meaning, options, scoring, or sequence changes, and provide expected scoring examples. Changes to dimension meaning require a new dimension ID.

Describe fictional consequences as possibilities, never predictions. Report selected approaches and contextual evidence rather than personality types or claims of effectiveness. A suggested Growth response is an authored rubric, not a validated outcome.

Do not commit browser backups, personal answers, tokens, or generated build output. Submit changes through a pull request with the `Validate Release` check passing. Dependency updates should update the appropriate lock file and be reviewed alongside their release notes.
