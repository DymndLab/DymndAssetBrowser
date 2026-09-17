# Development workspace

Current release: **DYM&D Asset Browser 2.9.0**, packaging the accepted 2.8.5.3 behavior. Open this repository folder directly in VS Code.

## Build and test

Requires Windows and the .NET 10 SDK.

```powershell
dotnet restore .\DymndAssetBrowser.slnx
dotnet build .\DymndAssetBrowser.slnx -c Release --no-restore
dotnet run --project .\tests\DymndAssetBrowser.SmokeTests -c Release --no-build
dotnet run --project .\tests\DymndAssetBrowser.Tests -c Release --no-build
dotnet run --project .\tests\DymndAssetBrowser.Tests -c Release --no-build -- --theme
```

For an owner-local WPF fixture:

```powershell
dotnet run --project .\tests\DymndAssetBrowser.Tests -c Release --no-build -- --ui .\work\v1-test-state .\outputs\local-verification
```

Never point this harness at live state: it changes filters, tags, mappings and the copied index. Its artwork references are external/read-only. The fixture is not distributed. Native CSP drag acceptance is a separate manual check. Close test processes before rebuilding their binaries.

## Approved guide

The owner completed the illustrated guide in Google Docs. `docs/Dymnd-Asset-Browser-Feature-Guide.pdf` is the approved release copy, hash-pinned by `docs/release-documents.json`. Earlier local Markdown/HTML/JSON drafts and their generator are not authoritative and are not shipped. Do not regenerate over the approved PDF. Review replacement PDFs visually and for privacy, then update the approval hash deliberately. The README and release notes cover subsequent Planner refinements without rewriting the owner's guide.

## Package without publication

```powershell
.\scripts\build-release.ps1 -Version 2.9.0
# Portable-only:
.\scripts\build-release.ps1 -Version 2.9.0 -SkipInstaller
# Only after explicit owner release approval:
.\scripts\build-release.ps1 -Version 2.9.0 -PublicRelease
```

Windows x64, a self-contained runtime and the bundled WebP codec are included. Inno Setup 6 is required unless explicitly skipped. The script checks the project version, runs smoke/core/security/theme checks, and creates a fresh `artifacts/releases/2.9.0-<run-id>` directory. The manifest lists that run's ZIP, installer, and PDF with checksums, without absolute build paths.

Reviewed documentation is included by default from a narrow allowlist. The gate checks the release version, approved PDF hash, and file presence. `-Documentation Deferred` remains available for private development but cannot be combined with `-PublicRelease`. Building does not push, tag, sign, or publish anything. The GitHub workflow retains artifacts without publishing releases.

## State and boundaries

- Installed executable: `%LOCALAPPDATA%\Programs\DYM&D Asset Browser\DYM&D Asset Browser.exe`.
- Live state: `%LOCALAPPDATA%\DymndAssetBrowser`; source artwork stays external and read-only.
- Owner-local handoffs and test evidence remain under `outputs/`; these are not shipped.
- `outputs/` and `work/` may contain private paths, licensed previews, state copies, and recovery backups. Do not force-add them to Git.
- The experimental Builder under `experiments/` remains separate and is not shipped with the Browser.
- Legacy naming is intentional only in compatibility/obsolete-file handling or actual Forgotten Adventures integrations. Do not change source IDs or state locations merely to rename labels.
