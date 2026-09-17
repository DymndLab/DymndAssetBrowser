# DYM&D Asset Browser

A local Windows application for browsing image libraries and assembling wall, floor, and trim palettes for Clip Studio Paint. It supports Forgotten Adventures libraries and other folders of images.

**Current release: 2.9.0.** [Download the Windows installer or portable ZIP](https://github.com/DymndLab/DymndAssetBrowser/releases/tag/v2.9.0).

## Start here

1. Run `Dymnd-Asset-Browser-Setup-2.9.0.exe`, or extract the portable ZIP and run **DYM&D Asset Browser.exe**.
2. Choose **Add FA...** for an intact Forgotten Adventures library, or **Add Other...** for a different image folder.
3. Wait for the first index. Later starts load the saved index, with a purple loading indicator.
4. Use **Browser** to find assets, or **Planner** to prepare a Walls + Floor + Trim palette.
5. Select an asset and drag it into your target application. The source artwork stays unchanged.

Supported images: **PNG, JPG/JPEG, and WebP**. WebM and video are not supported. The Windows x64 package includes the runtime; users do not need the .NET SDK. The installer is currently unsigned. Obtain it from a source you trust and compare any supplied checksum; a matching checksum is not a security guarantee.

## Browse your library

The progressive filters are **Biome > Context > Category > Subcategory > Type**. Every level is optional: leave it at **All**, or choose a useful category directly. Selecting a deeper type fills unique upstream choices without inventing an ambiguous parent.

**Materials**, **Settlement collection**, **Variant**, and **Tags** provide additional ways to narrow results. Material finishes are paired with their material: Wood: Ashen and Brick: Earthy are separate values on a mixed wall.

- Material matching includes mixed-material assets by default. Use **Exclude additional materials** for wood-only walls.
- **Match all selections** requires every selected material; otherwise any selected material can match.
- Tag input is comma-separated. **All tags** requires every entered tag.
- Filename search combines unquoted words with AND and supports quoted phrases.
- Click the search box's **X** to clear it and keep typing.
- Expand a result heading, or use **Expand all / Collapse all**. All matching headings remain available; thumbnails load as you scroll.
- **Select all results** includes matching assets in collapsed sections, not just visible thumbnails.
- Right-click **Filter to** to start a fresh search from an asset's characteristics. Ctrl/Shift-click stages multiple choices; **Apply selected** replaces the old filters and filename search while retaining the library.

## Place and transform

| Control | Effect |
| --- | --- |
| Q / E | Rotate left / right by 90 degrees |
| Shift+Q / Shift+E | Rotate left / right by 45 degrees |
| F | Flip horizontally |
| R / Random button | Toggle random rotation for drags, using eight 45-degree angles |

Shortcuts do not rotate assets while you are typing in a text box. Random rotation starts off each session and does not rewrite the tile's manual orientation.

Untransformed drags use the **original file**, including WebP. Rotated/flipped WebPs use **lossless WebP** cache copies; transformed PNG/JPG/JPEG images use PNG copies. There is no added upscaling. Diagonal rotations need resampling and extra transparent bounds. Animated WebP previews/transforms use only the first frame; animated import into CSP is not promised.

Right-click also offers **Copy filepath** (the existing verified Foundry-friendly path where available), **Copy full filepath** (absolute Windows path), and **Show in File Explorer** (selects the original file).

## Plan a building

Planner has independent **Walls**, **Floor**, and **Trim** cards, each with Biome, Settlement type, Materials, Tags, and Variant. Broad wall filters show one sample per complete set. Click a sample to choose that set, or narrow filters until only one set remains.

Choose a floor and trim, then **Populate Build**. Wall pieces/detailing occupy the left column; floor textures and trim occupy the independently scrolling right column. One wall-set ribbon association applies to the complete selected set. It is a reference to a CSP brush, not an automatic brush installer or selector.

On a fresh/reset Planner, a wall click also supplies matching materials and finishes to Trim, allowing matching single-material and mixed pieces. Floor stays unchanged. Manually edited Trim is preserved; **Use these materials for Trim** explicitly replaces its materials. Clicking a Trim sample chooses its exact material combination.

Right-click **Apply filters to Planner** to use biome, settlement, materials, variant, or tags from an asset. Ctrl/Shift-click queues several properties. The **Apply to: Walls / Floor / Trim** checkboxes choose which sections receive them; those sections' previous filters are replaced. The active Browser/Planner button has a purple outline.

### Included brush references

Recognized wall sets include suggested CSP ribbon brush filenames. The included **Tool** and **Tool Group** labels reflect the developer's personal CSP arrangement, not tool groups supplied by Forgotten Adventures. Your brush locations may differ. Edit the fields and choose **Save for Wall Set**, or organize your CSP brushes to match if you prefer. Existing saved overrides are retained. No brushes are bundled, installed, selected, or reorganized by the app. Association CSV import/export is not included in 2.9.0.

## Tags, local data, and cache

**Edit tags...** stages an add/remove operation, previews it, and saves only on **Apply changes**. Removing a custom-only tag deletes that addition. Hiding an inferred tag stores a suppression so reindexing does not immediately restore it.

Settings, tags, mappings, the SQLite index, and caches live under `%LOCALAPPDATA%\DymndAssetBrowser`. Back up that folder with the app closed. The portable ZIP uses the same user-local state location; it is not a separate portable profile.

The gear opens cache utilities. Cache files persist between sessions. Do not clear transformed files during an import. **CSP File Objects linked to cached files may stop working; ordinary imported images are unaffected.** Removing a registered library removes its index entries, not its source artwork.

## Documentation

- [Feature guide (PDF)](docs/Dymnd-Asset-Browser-Feature-Guide.pdf)
- [AI-assisted development](docs/AI-DISCLOSURE.md)
- [2.9.0 release notes](RELEASE-NOTES.md)
- [Development and packaging](DEVELOPMENT.md)
- [Documentation review checklist](docs/RELEASE-CHECKLIST.md)

The illustrated PDF is the owner-approved guide. The repository copy includes a drag-and-drop introduction added to section 1 after the 2.9.0 release; the existing release downloads retain the earlier PDF. The Planner refinements described above postdate its screenshots. The guide has not been regenerated from earlier drafts.

## Independence and artwork

MIT licensed. This is an independently developed tool, not affiliated with or endorsed by Forgotten Adventures or CELSYS. Forgotten Adventures artwork, CSP brushes, and other asset libraries are not bundled. Supply your own libraries with the appropriate rights. Any Forgotten Adventures artwork visible in documentation screenshots is credited to [Forgotten Adventures](https://www.forgotten-adventures.net/).

The separate experimental Builder is not included in this application package. Planner is an asset-selection palette, not a wall-drawing or roof-generation engine.
