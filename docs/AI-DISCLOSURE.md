# AI-assisted development

DYM&D Asset Browser 2.9.1.

My programming experience is mainly C++ for microcontrollers, rather than desktop applications. Codex wrote the application code and supporting scripts. I defined the workflow and feature behavior, tested revisions with my own library and Clip Studio Paint, and directed changes. I have not personally reviewed most of the code; the source is available for others to inspect.

## What AI was responsible for

- Writing all of the application's code, including the supporting scripts used for classification and asset-index testing.
- Proposing implementation approaches and helping organize the code.
- Implementing features and fixes from the agreed requirements.
- Writing and running automated regression checks and scripted interactions with the Windows application.
- Investigating failures, reviewing code, and preparing builds, packaging scripts, and documentation drafts.

## What I was responsible for

- Defining the application's purpose and how it should fit my mapmaking workflow.
- Defining behaviors for browsing, filtering, wall sets, tagging, and placement/export of assets.
- Working through the data flow and trade-offs: source files, indexed metadata, user overrides, and generated image caches.
- Testing features with my own asset library and Clip Studio Paint.
- Identifying incorrect classifications and usability problems, then reviewing the resulting changes.
- Deciding which changes to keep and approving releases.

## How the application works

- It is a local Windows desktop application. The application contains no runtime AI integration, telemetry, or application-owned upload feature. Copying paths, dragging files, adding and editing asset tags, and opening File Explorer are explicit user actions.
- It reads artwork from folders you register. It does not move or reorganize that artwork.
- Folder paths and filenames supply searchable metadata. Forgotten Adventures libraries use FA-specific rules; other libraries use a more general parser.
- The application stores its own index, settings, tags, ribbon mappings, and generated images under `%LOCALAPPDATA%\DymndAssetBrowser`, separately from source artwork.
- Filters search the index. Thumbnails load as needed instead of loading the entire library into the window at once. On my computer, startup now takes about five seconds with my library.
- User tags are stored separately from inferred tags. Removing a custom-only tag deletes that addition; hiding an automatically inferred tag records an override.
- The Planner assembles wall sets, floors, and trim. It provides known ribbon associations where available and allows manual corrections. I'm checking those associations against CSP as I use different brush groups; many remain unverified.
- Dragging an unchanged asset supplies the original image, including WebP. Rotation and flip create cached copies: lossless WebP for WebP sources, and PNG for other supported image types. Diagonal rotation resamples pixels and expands transparent bounds; lossless encoding preserves the resulting image.
- Clearing the cache deletes generated image copies - for example images you rotated, flipped, or randomized. This is not necessary but can save disk space if you are building up a large number of duplicate images. Images already imported into CSP are unaffected, but File Objects linked to cached files may stop working.
- Forgotten Adventures artwork and CSP brushes are not bundled. Users obtain their own libraries, whether obtained from Forgotten Adventures, other online asset providers, or created themselves.

## Testing

- Features are implemented and revised incrementally, with automated checks, scripted application testing, and my hands-on use.
- The 2.9.0 full automated run passed **466 checks**, including Windows UI integration, with no skipped popup screenshots. This is not a claim of complete visual acceptance on every system.
- The core run passed **158 checks**, and the taxonomy/source/filter/index/cache smoke suite passed. Those checks overlap the full run and are not additional unique tests.
- The subsequent material-update run passed **528 checks**, including WPF integration and **209 overlapping core checks**. Four inactive-owner popup screenshots were skipped; native CSP importing was not retested. The audit examined all 709 indexed wall sets and 47 material/finish choices, rather than assuming that passing a sample established correctness across the library.
- An earlier retained snapshot of reports from 10-14 September 2026 contains **33 test runs and 5,593 recorded check passes**. These totals include repeated regression checks and checks completed before a later failure; they are not counts of unique tests or entirely successful runs.
- A focused subset covers **28 parsing and metadata checks**, with **894 recorded passes** across those reports. Examples include folder hierarchy, biome versus settlement collection, compound-material finish pairing, ceramic finishes, and unknown-folder handling.
- Parser changes were evaluated through repeated asset sampling reviews, alongside automated regression tests. Samples ranged from roughly **250 items for category-specific checks to 500 items for broad parser reviews**. Real-library audits covered an index of my **166,878 assets**; indexing an asset does not establish that its classification is correct.
- Hands-on CSP sessions are not counted in those totals. The latest automated run did not verify a native drop into CSP.

The historical snapshot totals have not been recalculated to include subsequent runs. They are retained records, not the complete lifetime testing history.

The installer is unsigned. Dependency and antivirus checks are part of release preparation, but this project has not received an independent professional security audit.
