# Release checklist

Release target: **2.9.0**. The owner approved publishing the existing behavior with the personal CSP Tool/Tool Group defaults disclosed, rather than waiting for CSV import/export.

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
- Some floor assets still expose Tile separately from the intended Stone/finish pairing. This limitation is disclosed rather than changed in this release.

## Publication

- Push only the reviewed release commit and matching tag to the existing repository.
- Attach the installer, portable ZIP, approved PDF, SHA-256 list, and privacy-safe manifest.
- Check the tag build and remote attachments before publishing; retain the local verification record.
- Do not include earlier draft HTML/Markdown guides, raw test captures, local data, or authoring DOCX files in release downloads.
