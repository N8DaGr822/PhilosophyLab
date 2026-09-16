# GitHub Pages deployment

Repository: https://github.com/N8DaGr822/PhilosophyLab

Intended site after deployment: https://n8dagr822.github.io/PhilosophyLab/

The workflow builds and validates pull requests. Pushes to `main` additionally upload only `artifacts/publish/wwwroot` and deploy through the `github-pages` environment. Deployment has `pages: write` and `id-token: write`; validation has read-only repository permission. Concurrent deployments are serialized.

An administrator must enable Settings → Pages → Build and deployment → Source: **GitHub Actions**, allow the GitHub-maintained actions used in the workflow, and enforce HTTPS. The workflow can then be started manually or by pushing to `main`. No personal token belongs in source or workflow secrets for Pages deployment.

Repository administration checklist:

After the first successful deployment, the owner can apply these settings with `scripts/configure-github.ps1` while `gh` is authenticated as **N8DaGr822**. The script verifies administrator access first. It protects main last and limits the Pages environment to protected branches. Collaborator write access cannot perform these changes.

- Enable Dependabot alerts and security updates, secret scanning/push protection where available, and private vulnerability reporting.
- Protect `main` against force pushes and deletion. Require pull requests and the `Validate Release` status check. For a solo-maintained personal repository, do not require another person's approval unless a reviewer is available.
- Protect the `github-pages` environment so deployments are limited to `main`.
- Confirm the site is intended to be public and confirm rights to the inherited content/icon listed in `THIRD-PARTY-NOTICES.md`.

The default base path is `/PhilosophyLab/`. To use a custom domain or an account-root site, set the Actions repository variable `PAGES_BASE_PATH` to `/`, configure the domain in Pages, and redeploy. Do not change the development `wwwroot/index.html` base path.

`scripts/prepare-pages.py` updates the published base tag, copies the app shell to `404.html`, and adds `.nojekyll`. Deep links mount Blazor but retain an HTTP 404 response on their initial request. This is a deliberate Pages fallback limitation, including for search indexing. In-app navigation returns normally. `_framework` files must remain in the artifact.

Local production preview:

```sh
dotnet publish -c Release -o artifacts/publish
python scripts/prepare-pages.py artifacts/publish/wwwroot --base-path /PhilosophyLab/
python scripts/serve-pages.py artifacts/publish/wwwroot
```

Open http://127.0.0.1:5158/PhilosophyLab/. This preview preserves the Pages-like 404 behavior, unlike the development server's SPA fallback.

After live deployment, confirm root and deep URLs, reload, JSON and WebAssembly requests, back/forward navigation, and no unexpected third-party requests. A custom domain or path change does not move browser data; export before changing origins. The new path-specific storage key does not automatically read the older shared key: use **Saved → Import older browser data** on the same origin. The old copy is retained.

References: [GitHub custom workflows](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages), [Microsoft Blazor base paths](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/app-base-path?view=aspnetcore-10.0).
