# Website Requirements and Plan for DistroNexus

## 1. Overview
The goal is to establish a dedicated, static website for **DistroNexus** hosted on GitHub Pages. This website will serve as the project's official face, providing users with product information, installation guides, comprehensive usage documentation, and release history.

**Project Context**: DistroNexus is a GUI application for managing WSL distributions, involving Go, PowerShell, and generic documentation.

## 2. Requirements Analysis

### 2.1 Functional Requirements
1.  **Landing Page**: A modern, attractive home page highlighting key features (GUI Dashboard, Centralized Download, Custom Installation, etc.) and a clear "Download" call-to-action.
2.  **Documentation Station**: Structured navigation for:
    *   Getting Started / Installation
    *   User Guide (Managing Instances, Configuration)
    *   Troubleshooting / FAQ
3.  **Release Notes (Blog)**: A dedicated section to publish changelogs (e.g., v1.0.1, v1.0.2) and announcements.
4.  **Multilingual Support (Optional/Future)**: Structure should allow for future Chinese (CN) support, given the existence of `README_CN.md`.
5.  **Hosting**: Completely static, hosted on GitHub Pages (`<username>.github.io/DistroNexus`).

### 2.2 Technical Stack Recommendation
*   **Engine**: **Docusaurus v3** (Recommended) or **VitePress**.
    *   *Reason*: Docusaurus is the industry standard for open-source documentation. It provides a polished "Landing Page + Docs + Blog" structure out-of-the-box, supports Markdown/MDX, and handles versioning/i18n well.
    *   *Alternative*: MkDocs with Material theme (good, but Docusaurus is often preferred for more "product-like" sites).
*   **Content Format**: Markdown (`.md` or `.mdx`).
*   **Deployment**: GitHub Actions (automated build and deploy on push to `main`).

## 3. Proposed Site Structure

We will create a specific directory (e.g., `website/` or `docs-site/`) to contain the site source code, keeping it separate from the application logic (`src/`) and raw scripts.

```text
/ (Project Root)
├── ... (existing files)
├── website/                  # New folder for the static site project
│   ├── blog/                 # Release notes (migrated from docs/release_notes)
│   ├── docs/                 # Documentation (migrated from docs/)
│   │   ├── intro.md
│   │   ├── installation.md
│   │   └── usage.md
│   ├── src/
│   │   └── pages/index.js    # Landing page custom layout
│   ├── docusaurus.config.js  # Site configuration
│   └── package.json
└── .github/workflows/
    └── deploy_site.yml       # CI/CD for GitHub Pages
```

## 4. Content Migration Strategy

| Source Content | Destination Section | Notes |
| :--- | :--- | :--- |
| `README.md` (Features) | **Home Page** | Summarize features for the landing page hero instructions. |
| `README.md` (Config) | **Docs / Configuration** | specific technical details go to docs. |
| `docs/release_notes/*.md` | **Blog** | Convert v1.0.1.md, etc., into blog posts with dates. |
| `docs/requirements*.md` | **Docs / Development** | Keep as "Contributing" or "Design" docs if public, otherwise ignore. |
| `docs/promotion/` | **Blog (Maybe)** | Articles can be blog posts. |

## 5. Implementation Details

### 5.1 Prerequisites
- Node.js environment (for generating the site locally).
- GitHub Repository settings: Enable GitHub Pages (Source: GitHub Actions).

### 5.2 Theme & Design
- **Color Scheme**: Dark/Light mode support (built-in). Use a primary color effectively (power-shell blue or similar).
- **Navigation Bar**: Links to "Docs", "Blog", "GitHub Repo".

## 6. Task List

- [x] **Initialization**
    - [x] Create `website` directory.
    - [x] Initialize Docusaurus project scaffold.
    - [x] Clean up default template files.

- [x] **Configuration**
    - [x] Edit `docusaurus.config.js` (Title: DistroNexus, URL, GitHub links).
    - [x] Set up deployment config (organizationName, projectName).

- [x] **Content Migration**
    - [x] Create `docs/intro.md` (Introduction).
    - [x] Create `docs/install.md` (Installation Guide).
    - [x] Create `docs/user-guide.md` (Features & Usage).
    - [x] Migrate `config/settings.json` explanation to `docs/configuration.md`.
    - [x] Migrate `docs/release_notes/` files to `website/blog/`.

- [x] **Home Page Design**
    - [x] Customize `src/pages/index.js` header and features list to match `README.md`.

- [x] **Automation**
    - [x] Create `.github/workflows/deploy-site.yml` to build and deploy to `gh-pages` branch.

- [x] **Verification**
    - [x] Build locally to test links and images.
    - [x] Push and verify GitHub Pages deployment.
