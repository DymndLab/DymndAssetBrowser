# DYM&D Asset Browser roadmap

## Future release - Planned

### CSV import/export for wall-set brush associations

Requested 16 September 2026. Deferred beyond 2.9.0; the current personal Tool/Tool Group defaults are disclosed in the README and release notes.

- Add **Import associations...** and **Export associations...**.
- One CSV row per wall set: wall set ID, readable wall set name, Ribbon, Tool, Tool Group. Match by stable ID, not display name alone.
- Export effective associations, including built-in suggestions and saved user overrides, so the owner's complete current setup can be backed up and shared.
- Import previews additions, changes and unmatched sets before confirmation. Validate malformed or duplicate rows; never silently guess unmatched sets.
- Allow blank Tool and Tool Group cells. Make replacement of existing assignments an explicit choice, including whether imported blanks clear those fields.
- Back up settings before applying an import. Canceling the preview must leave settings unchanged.
- Import/export affects association metadata only. It does not install .sut brushes or reorganize CSP.

### Separate personal organization from built-in brush suggestions

- The current built-in Tool and Tool Group labels reflect the owner's personal CSP arrangement, not an organization supplied by Forgotten Adventures.
- Keep built-in wall-set-to-Ribbon-name suggestions for recognized sets.
- For fresh users, leave Tool and Tool Group blank until assigned or imported.
- Before removing the organizational defaults, export the owner's complete effective associations and preserve them in local settings. Retain all existing manual overrides; no manual re-entry should be needed.
- Do not silently populate new users' settings with the owner's arrangement. The owner's CSV may be shared separately if desired.

### Acceptance checks

- Export/import round trip preserves wall-set identity and all association fields, including Unicode, commas and quotes.
- Preview, cancel, conflict handling, blank-field behavior and unmatched/duplicate-row handling behave as documented.
- Existing user overrides survive upgrades and migration.
- The owner's effective associations are unchanged after migration; a fresh profile shows suggested Ribbon names with blank Tool/Tool Group fields.
- No source artwork, installed CSP brushes or CSP tool groups are modified.

This roadmap is planning only. It does not authorize a build, installation, version bump or publication until implementation is requested.
