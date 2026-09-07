# Dym&D Asset Companion 2.5.1

Dym&D Asset Companion 2.5.1 is a Windows desktop tool for browsing large Forgotten Adventures and custom image libraries, dragging full-resolution assets into Clip Studio Paint, and assembling coordinated wall, floor, detailing, and trim palettes.

## Highlights

- A retrieval-oriented `Category > Type > Subtype` catalog separates what an asset is from its Material, Appearance, Theme, and Scene / Use.
- Broad virtual parents such as Walls collect related construction pieces, additions, cracks, damage, and detailing under collapsible result headings.
- Filename Search treats unquoted words as order-independent AND terms and preserves quoted phrases as exact adjacent searches.
- Advanced filters expose the objective Source Folder / Set and final Filename Variant without confusing detail collections with construction families.
- Selected assets can be rotated with **Q/E** and flipped horizontally with **F** before full-resolution dragging.
- Planner selects complete wall construction sets and shows representative cards while several sets match.
- Floor and Trim can be window-shopped from bounded live samples before the final palette is populated.
- Wall Additions and Detailing present compatible inserts, damage, decorations, and Arctic snow treatment without affecting ribbon resolution.
- CSP ribbon information is stored once per exact wall set. The Ribbon field uses the exact brush or sub-tool name displayed in CSP; Tool and Tool Group are optional organization notes.
- The illustrated Feature Guide and candid AI Disclosure are included with both release formats.
- A concise Italian quick-start is included for first-time setup and everyday use.

## Forgotten Adventures Library Requirement

Use **Add FA** with the standard Forgotten Adventures `_Assets` tree exactly as downloaded. The FA-aware catalog relies on both the official folder structure and descriptive filename cadence. Renaming, flattening, or reorganizing that tree can split wall sets, hide associated details, misclassify assets, or prevent ribbon resolution.

Custom folders can be registered with **Add Other**, but custom classification is experimental. Clear descriptive filenames provide the best results; ordinary filename searching and dragging are more dependable than Planner grouping for custom assets.

## Local Data and Source Safety

Dymnd indexes registered images in place and treats the source library as read-only. Its SQLite index, settings, ribbon mappings, thumbnails, and transformed drag copies are stored separately under `%LOCALAPPDATA%\DymndAssetBrowser`.

The application contains no runtime AI, telemetry, analytics, updater, or network client. See `docs\AI-DISCLOSURE.md` for the development and review disclosure.

## Distribution Notes

- The portable ZIP and per-user installer contain the same application and documentation.
- SHA-256 checksums are supplied with the release artifacts.
- The installer is not currently code-signed, so Windows SmartScreen may display a warning.
- Forgotten Adventures source asset files are not included. The illustrated guide contains credited interface screenshots; supply your own licensed library for use in the application.
- The experimental wall/floor/roof Builder is not part of the 2.5.1 Asset Companion release.
