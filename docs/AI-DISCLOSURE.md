# AI Disclosure

Dym&D Asset Companion has been built with substantial assistance from OpenAI Codex. This page explains what that means, what information the application handles, and what has and has not been independently verified.

> **The short version:** AI helped design, write, classify, test, document, and package Dym&D Asset Companion. No AI runs inside the application. The application does not send your assets, filenames, searches, settings, or usage to OpenAI or another AI service.

## No AI Runs in the Application

Dym&D Asset Companion is a local Windows desktop application. AI is part of the development process, not a runtime feature.

- The application contains no language model or generative-AI component.
- The application source contains no runtime HTTP client, socket client, telemetry, analytics, update-check, or phone-home implementation.
- It does not require an OpenAI account, API key, or internet connection.
- It does not start a shell or launch an external process.
- Registered artwork is read in place. The application writes its own index, settings, thumbnail cache, and transformed drag copies under `%LOCALAPPDATA%\DymndAssetBrowser`.
- The source artwork in a registered library is treated as read-only. Removing a library removes it from Dymnd's index; it does not delete the library.

Those statements describe the application code as reviewed for the 2.5.1 release candidate. Windows, .NET, an installer, or software you drag files into may have their own behavior outside Dymnd's control.

## How the Application Was Built

The owner supplied the product goals, workflows, asset-library knowledge, screenshots, test scenarios, and repeated hands-on feedback. In particular, the taxonomy and Planner behavior were developed by testing the application against real map-building tasks and correcting results that did not match how a human would look for the assets.

Codex performed a large share of the implementation work. That has included:

- designing and writing application code;
- examining the Forgotten Adventures folder and filename conventions the owner explicitly placed in scope;
- deriving and revising taxonomy rules;
- creating automated tests and audit tools;
- running builds, tests, full-library scans, and release checks;
- drafting and revising documentation and packaging scripts; and
- diagnosing issues from screenshots and the owner's field testing.

This has been an iterative collaboration rather than a conventional team process with independent human code review and pull-request approval. **The owner has not read the application source code line by line and has generally trusted Codex to implement, inspect, and test it.** That fact is disclosed plainly because it materially describes the review model.

The owner decides what the application should do, tests the user experience, accepts or rejects behavior, and decides whether a build is released. Codex does not independently publish a release.

## Development-Time Access Is Different From Runtime Behavior

During development, Codex was intentionally allowed to inspect the local project files and the Forgotten Adventures directory and filename structure, and to review screenshots and selected asset previews supplied for classification and interface work. That access occurred inside the owner's development sessions and was used to build this tool.

The released application does not contain Codex and does not reproduce that development access. Installing or running Dymnd does not send a user's library to Codex, OpenAI, or another AI provider.

Forgotten Adventures source asset files are not included with Dym&D Asset Companion. The Feature Guide contains credited interface screenshots displaying that artwork for identification and instruction. Users must provide their own licensed library.

## Checks Performed for This Release Candidate

The 2.5.1 release candidate was subjected to the following checks regardless of whether a line was drafted by Codex or directed by the owner. Some run in the packaging script and others were performed as an explicit release review; this is not presented as a mature continuous-integration or independent-review program.

| Check | What it establishes |
| --- | --- |
| Release build | The complete solution compiles in Release configuration with no compiler errors. Package-vulnerability reporting is checked separately. |
| .NET analyzers | The Release build runs the solution's configured SDK analyzers and reports no compiler or analyzer warnings or errors. |
| Application smoke suite | Taxonomy, source handling, current-state persistence, filtering, indexing, cache behavior, and file-drag support pass automated regression checks. |
| Builder smoke suite | The separate experimental builder passes geometry, zoom, prefab layering, ribbon, sizing, seam-fade, and transparency checks. The builder is not included in the Asset Companion installer. |
| Real-library regression scan | The parser is exercised against the owner's complete standard FA library, including construction sets, texture roles, advanced facets, wall additions, and ribbon anchors. |
| Dependency vulnerability query | Direct and transitive NuGet packages are checked against the current NuGet vulnerability database. |
| Runtime-capability review | Source is checked for network, telemetry, updater, shell, process-launch, and source-library mutation code. |
| Packaged artifact checks | The self-contained Windows ZIP and installer were built from the tested source and accompanied by SHA-256 checksums. An isolated QA installer was installed, launched, and uninstalled successfully. |
| Documentation checks | The illustrated source document was privacy-scrubbed, audited for missing image descriptions, rendered to the shipped PDF, and inspected page by page. |

For the 2.5.1 release-candidate review, the full-library regression covered 166,878 assets, both smoke suites passed, the configured Release analyzer build reported zero warnings or errors, and NuGet reported no known vulnerable packages in the application dependency tree.

## What These Checks Do Not Prove

This project has not received an independent professional security audit. Its code has not received comprehensive line-by-line review by a second human developer. The automated checks are meaningful regression and static-analysis gates, but they cannot prove the absence of every defect or security issue.

The project also does not yet enforce one uniform source-formatting policy. Formatting consistency is maintenance work, not a security guarantee, but it is another respect in which this remains a small, early-stage project rather than a mature audited product.

The installer is currently unsigned. Windows SmartScreen may therefore warn before installation. A checksum can confirm that a downloaded file matches the published release artifact, but it is not a substitute for code signing or independent review.

Dymnd parses filenames and decodes images selected by the user. As with any desktop application that opens third-party files, users should obtain libraries from sources they trust and keep Windows and the .NET runtime components supplied with the application current.

## Documentation and Screenshots

The feature guide and this disclosure were drafted and edited with Codex assistance and reviewed through conversation with the owner. Instructional screenshots are captures of the real application, with annotations supplied during hands-on testing. Screenshots displaying Forgotten Adventures artwork carry adjacent attribution and are used for identification and instructional commentary. If the documentation disagrees with the shipped interface, that is a documentation defect and should be corrected.

## Accountability

AI assistance does not make the software automatically safe or unsafe. What matters is an honest account of the process, clear limits on runtime behavior, repeatable checks, and a person making the release decision.

For Dym&D Asset Companion, that account includes an unusual but important limitation: the owner has exercised the software extensively but has relied on Codex for most code-level implementation and review. Users should weigh that fact alongside the local-only runtime design, read-only treatment of source artwork, automated checks, published checksums, and the absence of known vulnerable dependencies when deciding whether to run it.
