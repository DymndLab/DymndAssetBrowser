# DYM&D Asset Browser 2.9.1

Windows x64 release. Includes the installer, portable ZIP, illustrated feature guide, and SHA-256 checksums. No asset library or CSP brushes are bundled.

## Update Planner material matching and floor parsing

- Match wall sets by construction materials, excluding incidental shelf materials from discovery and Trim defaults.
- Preserve mixed-material walls and complete wall sets, including Adobe window segments and shelf pieces.
- Parse stone tiles as Stone with paired finishes, not Tile. Ceramic remains separate.
- Ensure dirty-stone textures are discoverable when filtering for Stone.
- Add regression tests and update release notes.

The library audit covered 709 wall sets and 47 material/finish choices. Nine Adobe Wide sets no longer appear under Wood because of their wooden shelves. Stone parsing corrections cover 36 tiled floor textures, 260 matching inlay pieces and nine dirty-stone textures; asset placement categories remain unchanged.

No reindex is required for these material facets. If a saved floor filter selects Tile and now shows no results, clear it and choose Stone and the desired finish. The included PDF now explicitly explains drag-and-drop in section 1.

## Included CSP brush references

Recognized wall sets include suggested ribbon brush filenames. The supplied **Tool** and **Tool Group** labels reflect the developer's personal CSP organization, not groups supplied by Forgotten Adventures. Your locations may differ: edit the fields and choose **Save for Wall Set**, or arrange your CSP brushes to match. The app does not install or reorganize brushes. Existing user-saved associations are retained. CSV import/export is planned, not included in this release.

## Existing Planner behavior

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

The material-update full run passed **528 automated checks**, including Windows UI integration and **209 overlapping core checks**. Four inactive-owner popup screenshots were skipped in the recorded material-update run. The taxonomy/source/filter/index/cache smoke suite and theme checks passed. The audit found changes only to the intended Adobe material matches among the wall-set material/finish queries.

These checks do not establish native CSP import acceptance, complete visual/high-DPI acceptance, or an independent security audit.

## Known limits and review items

- A bare Tile filename without explicit composition remains Tile; the parser does not assume all tiles are stone.
- Generic-library metadata depends on folder and filename evidence. FA wall-set/ribbon assumptions may not apply to other providers.
- Animated WebP is not an animation workflow; previews/transforms use the first frame. No video support.
- Native CSP imports and linked File Objects were not retested for this material-only update.
- The build is unsigned; Windows may display an unknown-publisher warning.
- The illustrated guide includes the approved drag-and-drop introduction. Its screenshots and older stone-as-Tile troubleshooting note predate this update; these release notes and the README describe current behavior.

For the previous public version, see [release 2.9.0](https://github.com/DymndLab/DymndAssetBrowser/releases/tag/v2.9.0). Debug logging and association CSV import/export are not included in 2.9.1.
