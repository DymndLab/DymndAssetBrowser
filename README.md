# Dym&D Asset Companion

Dym&D Asset Companion is a Windows desktop companion for large Forgotten Adventures and custom image libraries. It indexes assets in place, provides semantic taxonomy and filename search, and lets you drag full-resolution files into Clip Studio Paint. Planner generates a combined Walls + Floor + Trim construction palette with one wall-set ribbon association.

Current release: **2.5.1**. Version 3 is reserved for the integrated wall, roof, and floor creator.

## Install and start

1. Download the current `Dymnd-Asset-Companion-Setup-*.exe` from the GitHub release.
2. Run the installer and open **Dym&D Asset Companion** from the Start menu.
3. On first launch, choose your Forgotten Adventures `_Assets` folder. You may select `_Assets` itself or its parent folder.
4. Let the initial index finish. Later launches load the saved SQLite index instead of rescanning the library.

The installer and repository do **not** redistribute Forgotten Adventures source asset files. You must supply your own licensed asset library. The illustrated guide contains interface screenshots that display Forgotten Adventures artwork for identification and instruction; artwork credit belongs to [Forgotten Adventures](https://www.forgotten-adventures.net/). The application reads source files in place; it does not copy, move, rename, modify, or delete them.

Windows may show a SmartScreen warning for community builds until the installer is code-signed. Check that the download came from the expected GitHub release before choosing **More info > Run anyway**.

## Everyday use

- **Add FA...** registers one Forgotten Adventures `_Assets` folder with the semantic FA parser.
- **Add Other...** registers any other image folder with the conservative Generic parser.
- **Reindex Source** updates only the selected library; **Reindex All** updates every registered library.
- Reindexing rebuilds parser-derived classifications and preserves ribbon mappings.
- Removing a source removes its index records but never deletes source files.
- Browser and Planner tiles use the same native Windows file-drag pipeline. **Q/E** rotate the selected tile and **F** flips it horizontally; dragging exports that exact full-resolution transform from cache. An untransformed drag hands the untouched original file to the target application.
- **Copy filepath** is enabled when the asset has a verified Foundry-friendly path relative to the configured Foundry `Data` folder or a verified FA Nexus mirror.

Application state, the SQLite index, mappings, and thumbnail cache are stored under `%LOCALAPPDATA%\DymndAssetBrowser`. Back up the current state folder to preserve personal mappings and settings.

## Current development model

- Category, Type, and Subtype form a shallow cascading hierarchy; Material, Appearance, Theme, and Scene / Use answer how or where an asset can be used. Advanced filters expose the objective containing Source Folder / Set and final Filename Variant without reusing parser-specific Family meanings.
- Material and Appearance are multi-valued. A Stone + Wood wall can be found as Stone/Sandstone or Wood/Ashen without hiding one finish in Family.
- Planner's Walls selector works with complete construction sets and dynamic component controls rather than raw asset counts.
- Broad Planner wall filters show one representative card per matching set; choosing a card immediately opens the complete wall kit in the same results area.
- Compatible detailing can belong to several wall sets. Cracks, rubble, loose pieces, damage overlays, wall decorations/heads, and themed overlays are presented separately from structural members.
- Wall Additions presents compatible structural inserts such as Arrow Slits and Arched Wall pieces, matched to the selected wall and trim appearances.
- Exact wall sets own one ribbon association. Unresolved associations remain visible and correctable at set level.
- Settlement Structures and Textures now use item-level path/filename roles: floors, roofs, overlays, openings, textiles, liquids, terrain, hedge mazes, and elevation ribbons no longer fall into one generic texture bucket.
- Build/Paint values in Scene / Use retrieve floor surfaces, roof parts, wall details, hedge-maze pieces, ground surfaces, cave surfaces, and terrain-ribbon anchors directly.
- Planner wall controls remain usable at narrower window widths.
- Browser result groups can be collapsed. Context-specific Planner labels replace the ambiguous generic Kind / Profile label.
- `Food & Dining` retrieves ingredients, prepared food, and kitchen/dining clutter together without requiring a narrower setting. `Tavern Kitchen` additionally includes useful tables, shelving, and storage.

See [RELEASE-NOTES.md](RELEASE-NOTES.md) for the current release summary.

See the [Feature Guide](docs/Dymnd-Asset-Companion-Feature-Guide.pdf) for complete illustrated usage instructions and the [AI Disclosure](docs/AI-DISCLOSURE.md) for a candid description of how Codex was used, what review the code has received, and what AI is not present at runtime.

An [Italian quick-start](docs/QUICK-START-IT.md) is also available for installation and first use.

## Development

Prerequisites: Windows and the .NET 10 SDK.

```powershell
dotnet restore FAFamilyBrowser.slnx
dotnet build FAFamilyBrowser.slnx -c Release --no-restore
dotnet run --project tests/FAFamilyBrowser.SmokeTests/FAFamilyBrowser.SmokeTests.csproj -c Release --no-build
```

To run the optional full-library regression scan without embedding a personal path:

```powershell
dotnet run --project tests/FAFamilyBrowser.SmokeTests/FAFamilyBrowser.SmokeTests.csproj -c Release --no-build -- --real-scan --fa-root "X:\path\to\_Assets"
```

To create release artifacts:

```powershell
.\scripts\build-release.ps1
```

The script creates a self-contained `win-x64` publish directory and ZIP. If Inno Setup 6 is installed, it also creates the per-user installer under `artifacts\installer`.

## Experimental Builder V1

`experiments/DymndBuilder.V1` is a separate, provisionally named construction prototype. It filters indexed wall sets, accepts snapped line/rectangle/circle guides on a square-sized canvas, and renders the associated ribbons and junction prefabs into a full-resolution transparent PNG. Its floor selector is currently a disabled WIP extension point. It does not alter the Asset Companion database, CSP brushes, or source art, and it is not included in the Asset Companion installer.

Run `scripts\build-builder-v1.ps1` to test and publish its standalone executable under `artifacts\builder-v1\win-x64`.

## Distribution notes

MIT licensed. This is an unofficial community tool, is not affiliated with or endorsed by Forgotten Adventures, and does not include any third-party asset libraries.
