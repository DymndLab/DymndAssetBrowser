# Third-party notices

DYM&D Asset Browser 2.8.5 is distributed as a self-contained .NET application. It uses the components below. Forgotten Adventures source asset files and Clip Studio Paint brushes are not bundled.

## Artwork and product references

The Feature Guide includes interface captures showing Forgotten Adventures artwork as part of workflow examples. Captures displaying that artwork carry an adjacent credit; they are not a reusable asset library.

Artwork credit: Forgotten Adventures - <https://www.forgotten-adventures.net/>.

DYM&D Asset Browser is an independent community project, not affiliated with or endorsed by Forgotten Adventures or CELSYS. Users supply their own appropriately licensed libraries. Forgotten Adventures and Clip Studio Paint names identify supported libraries and workflows, not ownership or endorsement of this application. The screenshot selection remains subject to owner review before publication.

## .NET

The self-contained package includes portions of the Microsoft .NET runtime and Windows Desktop runtime, licensed under the MIT License. See <https://github.com/dotnet/runtime> and <https://github.com/dotnet/wpf>.

## Microsoft.Data.Sqlite

- Microsoft.Data.Sqlite 10.0.11
- Microsoft.Data.Sqlite.Core 10.0.11
- License: MIT
- Project: <https://learn.microsoft.com/dotnet/standard/data/sqlite/>

## SQLitePCLRaw

- SQLitePCLRaw.bundle_e_sqlite3 2.1.12
- SQLitePCLRaw.core 2.1.12
- SQLitePCLRaw.lib.e_sqlite3 2.1.12
- SQLitePCLRaw.provider.e_sqlite3 2.1.12
- License: Apache-2.0
- Project: <https://github.com/ericsink/SQLitePCL.raw>

## SkiaSharp / native image codec

- SkiaSharp 4.152.0
- SkiaSharp.NativeAssets.Win32 4.152.0 (Windows runtime dependency)
- License: MIT for the SkiaSharp packages, with additional upstream native-component notices supplied by the packages.
- Project: <https://github.com/mono/SkiaSharp>

Release packaging copies each SkiaSharp package's license and any supplied `THIRD-PARTY-NOTICES.txt` into the package's `licenses` directory. Those full notices, including upstream native-code attributions, remain part of the distribution. They are not replaced by this summary.

The versions and license identifiers above are based on the packages resolved for this build. Refer to each package's license texts and notices for its full terms. Documentation-generation dependencies are development tools only; they are not part of the installed application.
