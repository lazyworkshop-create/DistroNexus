# Partner Center Short Submit Flow (Click Order)

Date: 2026-02-16
Scope: Remaining manual steps for Microsoft Store submission.

## 0) Prepare Artifacts (local)
1. Confirm artifact exists:
   - `release/store/DistroNexus.Package_2.0.1.0_Test/DistroNexus.Package_2.0.1.0_x64_ARM64_bundle.msixupload`
2. Keep these fields ready:
   - Identity Name: `LazyWorkshopCreate.DistroNexus`
   - Publisher: `CN=C4B6BD9D-352C-4CE3-82BD-5A54506C898B`
   - Version: `2.0.1.0`

## 1) Partner Center Entry
1. Open **Apps and games** → select **DistroNexus**.
2. Go to **Submissions** → **Create new submission**.

## 2) Packages (first)
1. Open **Packages**.
2. Upload `.msixupload` file.
3. Wait for processing to finish.
4. Verify detected package identity/version/architectures are correct.

## 3) Properties
1. Open **Properties**.
2. Confirm:
   - Category: `Utilities & tools` → `Developer tools`
   - Pricing: `Free`
   - Markets: target markets as planned
3. Save.

## 4) Store Listings
1. Open **Store listings** → edit default language (and zh-CN if used).
2. Fill:
   - Short description
   - Full description
   - What's new
3. Upload required screenshots (desktop, PNG, >=1366x768, >=1).
4. Upload/store 1:1 icon if requested in listing assets.
5. Save.

## 5) Privacy / Support
1. Open listing/support related section.
2. Fill support URL/email.
3. Fill public HTTPS privacy policy URL.
4. Save.

## 6) Submission Options / Certification Notes
1. Open **Submission options** (or equivalent certification notes area).
2. Fill restricted capability justification for `runFullTrust`:
   - App invokes local WSL/Windows tooling (`wsl.exe`) for user-initiated operations.
   - No in-app purchase.
   - Include reproducible test steps.
3. Save.

## 7) Product Declarations / Age Rating / System Requirements
1. Open **Product declarations** and answer all required items.
2. Open **Age ratings** and complete questionnaire.
3. Open **System requirements** and declare WSL-related prerequisite if applicable.
4. Save each page.

## 8) Final Review and Submit
1. Open **Review and publish**.
2. Ensure no blocking validation errors remain.
3. Submit to Store certification.

## 9) After Submit (for checklist closure)
1. Record submission ID and timestamp.
2. Save screenshots of completed sections.
3. Update checklist statuses:
   - `docs/development/store-publish-implementation-checklist.md`
   - `docs/development/store-publish-test-checklist.md`
   - `docs/development/store-publish-acceptance-checklist.md`
