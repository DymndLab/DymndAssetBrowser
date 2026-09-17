# Release checklist

Release target: **2.9.1**. The owner approved the material-filter/parser update and the commit message before publication. Personal CSP Tool/Tool Group defaults remain disclosed; CSV import/export and diagnostic logging are excluded.

## Before packaging

- Match the application version, release notes, documentation approval record, and Git tag.
- Preserve the approved illustrated PDF and verify its SHA-256 against `release-documents.json`. Do not substitute older drafts.
- State that suggested Tool/Tool Group names are the developer's personal CSP arrangement. No brush files or asset libraries are bundled; users can edit saved associations.
- Review staged files for credentials, private paths, licensed source artwork, local settings, indexes, caches, and test artifacts. Do not force-add ignored work/output directories.
- Run smoke, core/security, theme, and real-library WPF checks using isolated state. Record skipped visual checks honestly; test counts overlap.

## Final package

- Build from the intended release commit with reviewed documentation.
- Verify installer/ZIP/PDF contents, version, checksums, and absence of private build paths in public metadata.
- Run an antivirus scan on the final output.
- Exercise isolated install/upgrade without touching live settings or artwork; confirm the installed binary matches the package.
- Keep the unsigned-installer notice. Automated checks are not an independent security audit or proof of native CSP acceptance.
- The owner confirmed the updated Planner in live use. Native CSP drops, linked File Objects, original/transformed WebP imports, and high-DPI behavior remain manual acceptance areas; do not claim the automated suite proves them.
- Verify Adobe accessory materials do not influence wall-set discovery/Trim defaults, mixed-material sets remain discoverable, and complete sets retain their window segments and accessories.
- Verify stone tiles and dirty-stone textures expose Stone/finish pairs without changing ceramic or actual dirt terrain. Explain that a saved Tile-only filter may need clearing.

## Publication

- Push only the reviewed release commit and matching tag to the existing repository.
- Attach the installer, portable ZIP, approved PDF, SHA-256 list, and privacy-safe manifest.
- Check the tag build and remote attachments before publishing; retain the local verification record.
- Do not include earlier draft HTML/Markdown guides, raw test captures, local data, or authoring DOCX files in release downloads.
