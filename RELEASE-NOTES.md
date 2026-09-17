# DYM&D Asset Browser 2.9.0

Windows x64 release. Includes the installer, portable ZIP, illustrated feature guide, and SHA-256 checksums. No asset library or CSP brushes are bundled.

## Included CSP brush references

Recognized wall sets include suggested ribbon brush filenames. The supplied **Tool** and **Tool Group** labels reflect the developer's personal CSP organization, not groups supplied by Forgotten Adventures. Your locations may differ: edit the fields and choose **Save for Wall Set**, or arrange your CSP brushes to match. The app does not install or reorganize brushes. Existing user-saved associations are retained. CSV import/export is planned, not included in this release.

## Latest Planner improvements

- Choosing a wall set supplies matching material/finish defaults to untouched Trim, including matching single-material and mixed pieces. Floor is unchanged; manual Trim choices are preserved.
- Clicking Trim filters to its exact material combination, before or after Populate Build.
- Right-click **Apply filters to Planner** supports queued characteristics and compact **Walls / Floor / Trim** target checkboxes. Only checked sections have their filters replaced.
- **Use these materials for Trim** provides an explicit material-transfer action; Adobe metadata is now recognized.
- Floor drag initiation and retained Floor/Trim thumbnails are corrected.
- Browser and Planner navigation show a purple outline on the active page.

## Browser

- Source-aware, optional progressive filtering: Biome, Context, Category, Subcategory, Type.
- Separate multi-select materials/finishes, settlement collection, variant, and tag controls.
- Mixed-material matching by default, with Match all selections and Exclude additional materials options.
- All matching result headings remain reachable; expandable sections and viewport-lazy thumbnails replace the old limited result preview.
- Expand all / Collapse all, thumbnail size control, and a filename-search clear button.
- Right-click Filter to can replace the current filters and filename search with one or several characteristics of an asset.
- Tag editing previews changes, requires Apply changes, and confirms saved edits. Custom-only tags are removed outright; inferred tags can be suppressed.
- Copy full filepath and Show in File Explorer accompany the existing Foundry-friendly Copy filepath action.

## Planner and placement

- Independent Walls, Floor, and Trim cards use the Browser's source-aware filtering approach.
- Click a wall sample to choose its whole construction set, or reach one set through filters.
- Unavailable material/variant choices are visibly disabled; existing invalid selections can still be cleared.
- Floor and Trim share a right-hand scrolling column independent of the wall column.
- Ribbon associations belong to the complete wall set and remain manually correctable.
- Q/E rotate by 90 degrees; Shift+Q/E by 45 degrees; F flips.
- R toggles random drag rotation across eight 45-degree angles without changing manual orientation.
- WebP import/thumbnail support uses a bundled decoder. Normal drags preserve the original WebP; transformed WebPs use lossless WebP copies. Other image transforms retain PNG output.
- Cache utilities show generated file counts and sizes and clear recognized generated files. Existing cached files are not automatically removed on upgrade or exit.

## Reliability and packaging

- Product source/project names now use DymndAssetBrowser; FA-specific parser identifiers remain where meaningful.
- Installer upgrades no longer delete arbitrary contents of the installation directory.
- New output directories and current-run checksums prevent stale packages being mistaken for current output.
- The packaging gate runs both regression suites. GitHub automation retains review artifacts without publishing a release.
- Settings have a per-state-directory instance lock, stale-save checks, last-good backup, and explicit recovery.
- Indexing skips linked descendants and rejects linked roots/ancestors. Use the physical library folder.
- Cache writes and maintenance reject linked cache directories/ancestors.
- SQLite paths containing semicolons and quotes are handled safely through a connection-string builder.

## Verification snapshot

The 2.9.0 full run passed **466 automated checks**, including Windows UI integration, with no skipped popup screenshots. The core run passed **158 checks**, overlapping the full-run total, and the taxonomy/source/filter/index/cache smoke suite passed. The owner also confirmed the updated Planner behavior in live use. The release dependency audit reported no known vulnerable packages from the configured NuGet source.

These checks do not establish native CSP import acceptance, complete visual/high-DPI acceptance, or an independent security audit.

## Known limits and review items

- Some floor filenames still expose Tile separately instead of the intended Stone/finish pairing. Broaden Materials or search filenames/tags when needed; a parser correction remains open.
- Generic-library metadata depends on folder and filename evidence. FA wall-set/ribbon assumptions may not apply to other providers.
- Animated WebP is not an animation workflow; previews/transforms use the first frame. No video support.
- Test original and transformed WebP drags in CSP and verify any linked File Objects before release.
- The build is unsigned; Windows may display an unknown-publisher warning.
- The owner-approved illustrated guide is included unchanged. The latest Planner refinements are described above and in the README; its screenshots predate those refinements.

For the earlier public version, see [release 2.5.1](https://github.com/DymndLab/DymndAssetBrowser/releases/tag/v2.5.1).
