"""Fail CI on known .NET or npm dependency vulnerabilities, or an unavailable audit."""
import json
import os
import subprocess

dotnet = subprocess.run(["dotnet", "list", "package", "--include-transitive", "--vulnerable", "--no-restore", "--format", "json"], check=True, capture_output=True, text=True)
report = json.loads(dotnet.stdout)
affected = []
for project in report.get("projects", []):
    for framework in project.get("frameworks", []):
        for kind in ("topLevelPackages", "transitivePackages"):
            affected.extend(p["id"] for p in framework.get(kind, []) if p.get("vulnerabilities"))
if affected:
    raise SystemExit("Known .NET vulnerabilities: " + ", ".join(sorted(set(affected))))
subprocess.run(["npm.cmd" if os.name == "nt" else "npm", "audit", "--audit-level=low"], check=True)
print("Dependency audits passed.")
