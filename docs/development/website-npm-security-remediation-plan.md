# Website NPM Security Remediation Plan

Date: 2026-02-20

## Scope
- Workspace: `website`
- Goal: reduce security risk without forcing breaking dependency upgrades.
- Constraint: keep current Docusaurus major/minor if possible.

## Current Status
- `npm audit fix` was executed successfully.
- Vulnerabilities reduced from **29** to **28**.
- Remaining vulnerabilities: **11 moderate**, **17 high**, **0 critical**.

## Key Findings
1. `@docusaurus/core` and `@docusaurus/preset-classic` are already at latest stable version (`3.9.2`).
2. Most remaining findings are transitive vulnerabilities under the Docusaurus webpack loader chain:
   - `file-loader`, `url-loader`, `null-loader`
   - `schema-utils` -> `ajv` / `ajv-keywords`
3. Another high-severity chain comes from:
   - `@docusaurus/core` -> `serve-handler@6.1.6` -> `minimatch@3.1.2`
4. `npm audit` reports `fixAvailable: false` for the core remaining chains, meaning no non-breaking direct fix is available in the current dependency graph.

## Safe Actions Completed
- Ran `npm audit fix` (non-force).
- Re-ran `npm audit` and captured current baseline.
- Confirmed dependency tree paths with `npm ls`.

## Recommended Next Steps
1. **Keep current lockfile update** from `npm audit fix` (already applied).
2. **Do not run `npm audit fix --force`** in this repository right now.
   - Reason: likely to cause framework-level breakages without guaranteed remediation.
3. **Track upstream Docusaurus releases** for dependency-chain fixes.
   - Trigger condition: first stable release after `3.9.2`.
4. **Add periodic security check** in CI or local workflow:
   - `npm audit --omit=dev`
   - `npm outdated`
5. **Re-evaluate when Docusaurus updates**:
   - Upgrade to next stable version.
   - Run `npm install` + `npm audit` + site build validation.

## Optional Hardening (No Dependency Break Risk)
- Keep website deployment behind a static hosting/CDN policy that blocks suspicious requests.
- Ensure generated static output is the only deployed artifact (no dev server in production).

## Command Reference
```powershell
Push-Location website
npm install
npm audit
npm outdated
npm run build
Pop-Location
```
