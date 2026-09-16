# Security and data handling

The supported release is the current `main` branch. Keep .NET packages, GitHub Actions, and development dependencies updated through Dependabot and reviewed pull requests.

Please use the repository's private vulnerability reporting feature when it is enabled. Do not put secrets, actual backups, or personal answers in public issues. If private reporting is unavailable, request a private reporting channel without publishing exploit details or user data.

This static app has no server accounts or API keys. People within one browser are not access-controlled. Storage keys distinguish site paths but do not isolate applications sharing an origin. Use a separate custom domain when origin isolation is needed. Exported backups contain names, answers, and notes in plain JSON.

Import validates structure and known answer/scoring relationships; it does not authenticate who produced a file. An imported historical snapshot is not proof of an official test version. Review files from trusted sources only.
