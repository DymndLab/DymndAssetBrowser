# Changelog

## 2.5.1

- Added the illustrated Feature Guide and a candid AI Disclosure to the release package. The disclosure distinguishes Codex's substantial development role from the application's local, AI-free runtime and records the project's actual review and security limitations.
- Added installed Start-menu access to the Feature Guide and made tagged CI builds derive and validate their release version instead of silently packaging a development version.

- Rebuilt Browser Advanced filters around objective source metadata. `Source Folder / Set` now names the asset's actual containing folder and `Filename Variant` uses the final letter-number design code, eliminating overloaded Family/Variant behavior such as Adobe wall cracks masquerading as an Adobe construction set.
- Added full-library advanced-facet regression coverage, including strict separation of `Wall Adobe Cracks` and `Wall Adobe Wide A` across all 166,878 indexed assets.
- Corrected Arctic and household catalog rules: post-office decals are Signs & Notices; corrugated panels are cross-listed as roofing and construction sheet material; firewood is Heating & Fuel; Rocky Snow JPGs are ground surfaces; Arctic blood is gore; and snow edges/paths are snowy terrain detailing rather than shorelines.
- Made an Arctic wall-theme choice immediately populate Planner Detailing with reusable snow edges, piles, patches, and frost/snow/ice overlays; terrain ribbons, cracked ice, and blood remain in their purpose-specific Browser buckets.

- Changed filename search to order-independent AND matching for unquoted words, with quoted text retained as one verbatim phrase.

- Added horizontal asset flipping with the **F** hotkey and visible Transform control. The preview and full-resolution PNG dragged into CSP use the same rotation/flip state while the source asset remains untouched.
- Moved all Palisade walls, gates, pillars, walkways, stairs, and loose planks from Boundaries/Fences into `Defenses & Barricades > Palisades`.
- Replaced the catch-all Infrastructure/Platforms bucket: only walkway tiles remain there; loose planks, cell floors, mechanical parts, pipes, grates, and mechanical ribbons now have purpose-specific locations.
- Replaced the remaining large catch-all buckets for water structures, stairs/ladders, decor, rubble, furniture, flora, shelters, natural water features, and workplace equipment with purpose-specific Subtypes. True generic source folders remain explicitly labeled General or Miscellaneous.
- Audited every roof source-folder asset without redesigning the deferred roof taxonomy; all true roof textures and pieces remain under Roofs, while reservoir/coop roofs and two treasure filenames correctly remain with their parent objects.
- Replaced encoded hierarchical Type strings with cascading `Category > Type > Subtype` filters. Broad Types such as Walls, Floors, Terrain, and Cave & Underdark now remain short while Subtype supplies meaningful precision.
- Cross-listed arrow slits and arched wall inserts under the Walls Type while retaining their precise Openings classifications.
- Kept broad parent results organized under collapsible `Type > Subtype` headings.
- Restricted `Construction > Floors > Surfaces` and the Planner Floor picker to texture-folder swatches; plaques, inlays, damage, breaks, overlays, grates, and placeable floor pieces now have dedicated Subtypes.
- Reorganized Nature into Terrain, Cave & Underdark, Flora, and Water & Liquids Types. Cave and water surfaces are distinct from their path, ribbon, formation, and shoreline pieces.
- Collapsed Food & Dining into Ingredients, Prepared Food, and Kitchen & Dining Clutter Types so the full category can furnish a kitchen in one pass.
- Removed the obsolete scalar classification editor, assignment menus, manual-correction state, and correction-bearing index fields. Classification is now entirely parser- and taxonomy-driven.

- Added a live Planner wall-set gallery: broad filters show one representative card per matching set, and choosing a card replaces the gallery with that complete wall kit.
- Added live, bounded Floor and Trim sample galleries with true matching totals, so both can be window-shopped before Populate Build.
- Made a left-click choose an exact wall set or floor texture; clicking Trim now chooses its material and appearance while retaining every matching frame, sill, arch, and opening variant.
- Added Planner tile context filters: right-click a sample and broaden directly to its meaningful Material, Appearance, Theme, Family, Variant, or wall-set property.
- Restored the Planner's stable two-by-two palette: Wall Pieces beside Floor Textures, then Detailing beside the combined Trim Set.
- Aligned all three selector cards around Theme, material/system, appearance, kind/profile, and variant; removed fixed Group/SubGroup controls from Floor and Trim.
- Added gentle wall-to-Floor/Trim defaults: untouched panes inherit an unambiguous wall material and appearance, while user-edited panes remain independent.
- Moved the four core Glass sheets to `Surface Materials > Glass` and excluded texture-folder records from Trim even when an older index still labels them as Openings.
- Kept the explicit Populate Build action for combining the selected walls with potentially large Floor and Trim result sets.
- Made Browser result headings collapsible.
- Moved raw Family / Set to Advanced filters, added the visible Subtype filter, and prevented wood inserts in single-material Adobe pieces from polluting wall Appearance choices.
- Modeled Mudbrick walls as Adobe with `Mudbrick Light` / `Mudbrick Red` appearances and `Thin` / `Wide` profiles.
- Removed parser-version bookkeeping and the stale-index banner from the pre-alpha application.

- Catalogued all 54,925 requested settlement-structure/Horror-wall assets and 2,837 texture assets into semantic retrieval roles with zero unsorted items in that scope.
- Distinguished floor and roof surfaces, overlays, drapes, pergolas, glass, magical liquids, wilderness terrain, cave surfaces, and liquid terrain instead of treating every texture as generic terrain.
- Added Hedge Maze and terrain/elevation ribbon discovery, including Volcanic cliff and cave-path anchors.
- Added Build/Paint retrieval values to Scene / Use and refined specialty structure roles such as pods, domes, igloos, panels, pools, orbs, and energy artifacts.
- Rebuilt the Planner wall selector layout for narrow windows.
- Added full-library regression coverage for construction roles, texture roles, wall-set anchors, Adobe variant grouping, and wide-only Adobe shelves.

- Added a dedicated Planner `WALL ADDITIONS` section for structural inserts that complement the selected wall set.
- Associated all 19 core arrow slits by masonry appearance and, where present, the selected wall or trim wood finish.
- Associated the 36 core `Arched_Wall` masonry inserts by wall appearance as the first additional build-component family.
- Separated Arrow Slits and Arched Wall Inserts from generic Windows & Sills in Browser Type filtering.
- Prevented wall additions from appearing a second time under Other Openings.

- Replaced the Browser's flat top-level taxonomy with retrieval-oriented Category, Type, Material, Appearance, Theme, and Scene / Use facets.
- Made Browser Material and Appearance multi-valued, so compound assets such as Stone + Wood are discoverable through either component.
- Added named scene retrieval beginning with Tavern Kitchen, alongside inferred room, venue, activity, and environment contexts.
- Added first-class wall construction sets. Planner now selects one set rather than counting individual wall PNGs, includes every compatible structural piece, and resolves one set-level CSP ribbon.
- Added many-to-many compatible wall detailing for cracks, rubble, loose pieces, damage overlays, wall decorations/heads, Dwarven wall pieces, and Drow metal overlays without allowing detailing to affect ribbon resolution.
- Added dynamic component controls for compound wall systems and stable exact-set persistence.
- Standardized user-facing branding as Dymnd.

## 2.4.1

- Migrated active user state from the legacy `FAFamilyBrowser` folder to `%LOCALAPPDATA%\DymndAssetBrowser` with automatic validation and fallback.
- Retained libraries, manual corrections, ribbon mappings, Planner selections, the SQLite index, and available thumbnail cache entries.

## 2.4.0

- Added first-run Forgotten Adventures library discovery and separate **Add FA…** / **Add Other…** actions.
- Removed personal filesystem assumptions from production, tests, and audit tooling.
- Expanded the semantic FA parser using recurring patterns learned from reviewed classifications, without shipping a per-filename classification catalog.
- Preserved manual corrections as the highest-priority classification source across reindexing.
- Added release packaging for a self-contained Windows ZIP and per-user installer.
- Replaced the app and installer artwork with a transparent nine-resolution Windows icon.

## 2.3.4

- Added a verified Foundry-friendly **Copy filepath** context-menu command.
- Retained seamless Planner section containers, card-only hover feedback, and continuous virtualized scrolling.

## 2.2.0–2.3.3

- Added Planner with independent Walls, Floor, and Trim selectors and persistent generated palettes.
- Added shared wall-set CSP ribbon associations and native file drag support.
- Added multiple source libraries, Generic parsing, shared taxonomy, bulk classification, filename search, dependent facets, and persistent SQLite indexing.
