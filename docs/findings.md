# Findings: Built-in Template Expansion

## Baseline Observations
- Current catalog includes 5 templates: `.NET`, `Node.js`, `Python`, `Docker`, and `Fullstack`.
- Current metadata is single-version oriented (`latest`/`lts`) and does not expose explicit selectable SDK channels (for example: .NET 6/8/10, Node 18/20/22).
- Current compatible distro set is primarily Ubuntu/Debian.

## External Research Notes
- Microsoft WSL guidance consistently positions these as mainstream workflows: web/app development, containers, databases, systemd services, and GPU-enabled ML.
- Systemd on WSL is relevant for service-oriented templates (for example MicroK8s and local daemons).
- Docker on WSL2 has two practical modes that should be represented in templates:
	- Docker Desktop WSL integration
	- In-WSL Linux container runtime (Docker Engine/Podman)
- GPU/ML setup in WSL is now a first-class scenario (CUDA path and DirectML-related workflows), but requires explicit environment preflight checks.
- Multi-version SDK handling is best implemented with ecosystem-native managers:
	- .NET: package/version selection and `global.json` pinning behavior.
	- Node.js: `nvm` with `lts/*`, `current`, and `.nvmrc`.
	- Python: `pyenv` with global/local/shell selection and `.python-version`.
	- Java/JVM: `sdkman` with per-shell and per-project `.sdkmanrc`.
	- Rust: `rustup` channels (`stable`, `beta`, `nightly`).
	- Go: official multi-install guidance via dedicated management docs.

## Source References
- https://learn.microsoft.com/windows/wsl/setup/environment
- https://learn.microsoft.com/windows/wsl/tutorials/wsl-containers
- https://learn.microsoft.com/windows/wsl/systemd
- https://learn.microsoft.com/windows/wsl/tutorials/gpu-compute
- https://learn.microsoft.com/windows/ai/directml/gpu-cuda-in-wsl
- https://learn.microsoft.com/en-us/dotnet/core/versions/selection
- https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu
- https://github.com/nvm-sh/nvm
- https://github.com/pyenv/pyenv
- https://sdkman.io/usage
- https://rustup.rs/
- https://go.dev/doc/manage-install

## Conclusions
- The template catalog should expand from a language-only starter set into a scenario-first matrix that includes CloudNative, Data/AI, Database, DevOps, and Platform workflows.
- Multi-version SDK support must be a core metadata concept (channel + major/minor + exact pinning), not script-specific ad hoc logic.
- Project-level version pinning artifacts should be generated optionally by templates to improve reproducibility across teams.
- WSL2-sensitive templates (systemd/container/GPU) require mandatory preflight checks and explicit fallback guidance.
- A phased rollout is the safest path: metadata extension first, then language templates, then WSL2 scenario templates.
