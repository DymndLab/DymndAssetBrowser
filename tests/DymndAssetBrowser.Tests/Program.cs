using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DymndAssetBrowser.App;
using DymndAssetBrowser.App.ViewModels;
using DymndAssetBrowser.Core.Models;
using DymndAssetBrowser.Core.Parsing;
using DymndAssetBrowser.Core.Persistence;

internal static class Program
{
    static int _checks;
    static readonly List<string> Passed = [];
    static readonly List<string> Skipped = [];
    static void Check(bool value, string label) { if (!value) throw new Exception(label); _checks++; Passed.Add(label); Console.WriteLine("PASS " + label); }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            CoreTests();
            if (args.Length > 0 && args[0] == "--theme") { ThemeRegressionTests.Run(Check); return 0; }
            if (args.Length > 0 && args[0] == "--ui") return UiTests(args[1], args[2]);
            if (args.Length > 0 && args[0] == "--startup") return UiTests(args[1], args[2], startupOnly: true);
            if (args.Length > 0 && args[0] == "--planner") return UiTests(args[1], args[2], plannerOnly: true);
            if (args.Length > 0 && args[0] == "--audit") Audit(args[1], args[2]).GetAwaiter().GetResult();
            Console.WriteLine($"{_checks} source-browser checks passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static AssetRecord Asset(string path) => new FaAssetFilenameParser().Parse(@"C:\FA", Path.Combine(@"C:\FA", path)) with { SourceId = "fa" };
    static void CoreTests()
    {
        SecurityRegressionTests.Run(Check);
        PlannerMatchingRegressionTests.Core(Asset, Check);
        FilterShortcutCoreTests();
        var wall = Asset(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_BrickWood_A\Wall_BrickWood_Earthy_Ashen_A_Corner_D_1x1.png");
        var woodland = Asset(@"Woodlands\Base_Woodlands_Settlement\Furniture\Seating\Chair_Wood_Light_A1.png");
        var garden = Asset(@"Woodlands\Botanical_Garden_Settlement\Furniture\Seating\Chair_Wood_Ashen_A1.png");
        var flora = Asset(@"Woodlands\!Wilderness\Flora\Trees\Tree_A1.png");
        var dwarf = Asset(@"Mountain\Mountain_Dwarf_Settlement\Structures\Aesthetics\Floor_Breaks\Floor_Stone_Slate_A1_Path.png");
        var coreBreak = Asset(@"!Core_Settlements\Structures\Building\Floor_Breaks\Stone\Floor_Break_Stone_Earthy_A1_Path.png");
        var soup = Asset(@"!Core_Settlements\Clutter\Food\Prepared_Meals\Soup_Bowl_A1.png");
        var t = SourceTaxonomy.Parse(wall);
        Check(t.Biome == "Base" && t.Context == "Settlement", "Core is Base / Settlement");
        Check(t.Levels.SequenceEqual(new[] { "Structures", "Building", "Walls and Curbs", "Wall BrickWood A" }), "Source hierarchy preserves every directory");
        Check(t.Materials.Select(m => m.Key).SequenceEqual(new[] { "Brick: Earthy", "Wood: Ashen" }), "Ordered compound material pairing");
        Check(!t.Materials.Any(m => m.Key == "Wood: Earthy"), "No finish cross-product");
        Check(SourceTaxonomy.Parse(woodland).Collection == "Base", "Base settlement collection");
        Check(SourceTaxonomy.Parse(garden).Collection == "Botanical Garden", "Named collection separate from biome");
        Check(SourceTaxonomy.Parse(flora).Context == "Wilderness", "Explicit Wilderness context");
        Check(SourceTaxonomy.Parse(soup).Materials.Count == 0, "Soup needs no material");
        foreach (var pair in new[] { ("Ceramic_Diagonal_Tile_Slate.jpg", "Slate"), ("Ceramic_Diagonal_Tile_PatternA_Sandstone.jpg", "Sandstone"), ("Ceramic_Tile_Small_PatternB_Red_Cracked_A.jpg", "Red"), ("Ceramic_Tile_PatternRed.jpg", "Red"), ("Ceramic_Tile_PatternB_Gold.jpg", "Gold"), ("Ceramic_Tile_PatternC_BlueGold.jpg", "Blue / Gold"), ("Ceramic_Tile_PatternA_WhiteBlue.jpg", "White / Blue") })
        {
            var ceramic = SourceTaxonomy.Parse(Asset(@"Desert\Base_Desert_Settlement\Textures\Ceramic_Tiles\" + pair.Item1));
            Check(ceramic.Materials.Count == 1 && ceramic.Materials[0].Key == "Ceramic: " + pair.Item2, "Ceramic owns finish: " + pair.Item1);
            Check(ceramic.Tags.Contains(Path.GetFileNameWithoutExtension(pair.Item1).Split('_').First(t => t.StartsWith("Pattern", StringComparison.OrdinalIgnoreCase) || t == "Slate")), "Ceramic pattern/source tokens retained: " + pair.Item1);
        }
        var deep = SourceTaxonomy.Parse(Asset(@"Horror\!Wilderness\Gore\Corpses\Humanoid\Dwarven\Modular\Dwarven_F1\Outfits\Underwear\Body_A1.png"));
        Check(deep.BrowserPath == "Wilderness > Gore > Corpses > Humanoid" && deep.Tags.Contains("Underwear"), "Browser heading stops at Type; descendants retain tags");
        Check(SourceTaxonomy.Parse(Asset(@"!Core_Settlements\Textures\Stone_Floors\Rock_Tiles_A_03.jpg")).Materials.Single().Material == "Stone", "Explicit material folder fallback");
        Check(SourceTaxonomy.Parse(Asset(@"Desert\Base_Desert_Settlement\Structures\Aesthetics\Floor_Breaks\Tile_Floor_Edge_C\Tile_Floor_Edge_C_White_Corner_B1.png")).Materials.Single().Key == "Tile: White", "Single finish across part tokens");
        Check(SourceTaxonomy.Parse(Asset(@"!Effects\Fire\Fire_A1.png")).Context == "Effects", "Effects root recognized");
        var uncertain = SourceTaxonomy.Parse(Asset(@"NewBiome\Unknown_Pack\Structures\Thing_A1.png"));
        Check(uncertain.Context == "" && uncertain.Levels.Count == 3, "Unknown pack preserved without race inference");
        var tags = new UserAssetTags().Edit(["Soup", "Favorite"], false, ["Food"]).Edit(["Food"], true, ["Food"]);
        Check(tags.Effective(["Food", "Prepared Meals"]).ToHashSet().SetEquals(["Prepared Meals", "Soup", "Favorite"]), "User additions and automatic suppression compose");
        Check(tags.Edit(["Food"], false, ["Food"]).Effective(["Food"]).Contains("Food"), "Adding a suppressed tag restores it");
        var removedCustom = new UserAssetTags().Edit(["test"], false, ["Rug", "Black"]).Edit(["TEST"], true, ["Rug", "Black"]);
        Check(removedCustom.Added.Count == 0 && removedCustom.Suppressed.Count == 0, "Removing a custom tag deletes it without suppression, case-insensitively");
        var oldCustomSuppression = new UserAssetTags { Suppressed = ["test", "Black"] }.Edit(["test"], true, ["Rug", "Black"]);
        Check(oldCustomSuppression.Suppressed.SequenceEqual(["Black"]), "Removing a legacy custom suppression clears it while retaining real source suppression");
        var bothOrigins = new UserAssetTags { Added = ["Black", "Favorite"] }.Edit(["Black", "absent"], true, ["Black"]);
        Check(bothOrigins.Added.SequenceEqual(["Favorite"]) && bothOrigins.Suppressed.SequenceEqual(["Black"]), "Source/user overlap suppresses the source while unknown removal adds no suppression");
        Check(new UserAssetTags().Edit(["Tag"], true, ["Tag"]).Suppressed.Contains("Tag")
            && new UserAssetTags { Added = ["Tag"] }.Edit(["Tag"], true, []).Suppressed.Count == 0, "Bulk removal can distinguish source and custom origins per asset");
        var source = new AssetLibrarySource { Id = "fa", Name = "FA", RootPath = @"C:\FA", ParserProfile = LibraryParserProfiles.Fa };
        var index = new SourceBrowserIndex([wall, woodland, garden, flora, dwarf, coreBreak, soup], [source], new Dictionary<string, UserAssetTags>());
        var joint = Asset(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_BrickWood_A\Wall_BrickWood_Earthy_Ashen_A_T_A_1x1.png");
        var woodWall = Asset(@"!Core_Settlements\Structures\Building\Walls_and_Curbs\Wall_Wood_A\Wall_Wood_Ashen_A_Corner_A_1x1.png");
        var plannerAssets = new[] { wall, joint, woodWall };
        var plannerIndex = new SourceBrowserIndex(plannerAssets, [source], new Dictionary<string, UserAssetTags>());
        var plannerCatalog = new SourcePlannerCatalog(plannerIndex, new AssetCatalogIndex(plannerAssets), WallConstructionCatalog.Build(plannerAssets));
        var materialFilter = new PlannerSourceSelection { Filter = new() { Materials = ["Wood: Ashen", "Brick: Earthy"], AllMaterials = true } };
        var compoundSets = plannerCatalog.WallSets(materialFilter, "fa");
        Check(compoundSets.Count == 1 && compoundSets[0].WallPieces.Count == 2, "Planner match-all materials resolves one complete compound wall set");
        Check(plannerCatalog.WallSets(materialFilter with { Filter = materialFilter.Filter with { AllMaterials = false } }, "fa").Count == 2, "Planner match-any includes mixed and single-material sets");
        var woodOnly = new SourceBrowserFilter { Materials = ["Wood: Ashen"], ExcludeAdditionalMaterials = true };
        Check(plannerIndex.Filter(woodOnly).Single().Asset == woodWall, "Exclude additional materials keeps pure Ashen Wood and removes BrickWood");
        Check(plannerIndex.Filter(woodOnly with { ExcludeAdditionalMaterials = false }).Count == 3, "Inclusive material matching remains the default");
        Check(plannerCatalog.WallSets(materialFilter with { Filter = materialFilter.Filter with { ExcludeAdditionalMaterials = true } }, "fa").Single().WallPieces.Count == 2, "Exact material combination retains the complete wall set");
        var extraMaterialEntry = plannerIndex.Entries[0] with { Taxonomy = plannerIndex.Entries[0].Taxonomy with { Materials = [new("Wood", "Ashen"), new("Brick", "Earthy"), new("Metal")] } };
        var extraIndex = new SourceBrowserIndex(plannerIndex.Entries.Append(extraMaterialEntry));
        Check(extraIndex.Filter(materialFilter.Filter with { ExcludeAdditionalMaterials = true }).Count == 2, "Match all rejects a third material without rejecting the two-material combination");
        Check(extraIndex.Filter(materialFilter.Filter with { ExcludeAdditionalMaterials = true, AllMaterials = false }).Count == 3, "Match any allows subsets of the selected types but rejects a third type");
        Check(extraIndex.Filter(woodOnly with { Materials = [] }).Count == 4, "No selected materials makes exclusion inert");
        Check(plannerIndex.Filter(woodOnly with { Materials = ["wood"] }).Count == 1, "Any-finish parent and case-insensitive material exclusion work");
        var twoFinishes = new SourceBrowserIndex([plannerIndex.Entries[2] with { Taxonomy = plannerIndex.Entries[2].Taxonomy with { Materials = [new("Wood", "Ashen"), new("Wood", "Light")] } }]);
        Check(twoFinishes.Filter(woodOnly).Count == 1, "Extra finishes of an allowed type are not extra material types");
        Check(JsonSerializer.Deserialize<SourceBrowserFilter>("{}")!.ExcludeAdditionalMaterials == false, "Older saved filters default to inclusive materials");
        var cornerOnly = materialFilter with { Filter = materialFilter.Filter with { Tags = ["Corner"] } };
        var ribbon = new RibbonMapping { FamilyKey = compoundSets[0].Id, CspTool = "Building", CspToolGroup = "Walls", CspRibbonName = "Test ribbon" };
        var completePalette = plannerCatalog.Populate(new() { Walls = cornerOnly }, new Dictionary<string, RibbonMapping> { [compoundSets[0].Id] = ribbon }, "fa");
        Check(completePalette.Walls.Count == 2 && completePalette.Walls.Any(a => a.FileName.Contains("_T_")), "A piece-specific tag selects its full set including other shapes");
        Check(completePalette.WallRibbon.Mapping?.CspRibbonName == "Test ribbon", "Source-filtered set retains its single manual ribbon mapping");
        var migrated = plannerCatalog.Migrate(new BuildRecipe { WallSet = new() { SetId = compoundSets[0].Id } }, "fa");
        Check(migrated.Walls.SelectedIdentity == compoundSets[0].Id && migrated.Walls.Filter.Materials.Contains("Wood: Ashen"), "Legacy pinned wall recipe migrates to visible source metadata");
        Check(plannerCatalog.Populate(new() { Walls = new() { Filter = new() { Tags = ["Missing"] } } }, new Dictionary<string, RibbonMapping>(), "fa").Walls.Count == 0, "No matching source wall set never falls back to unrelated walls");
        Check(index.Filter(new() { Levels = ["Furniture"] }).Count == 2, "Category usable without a context or biome");
        Check(index.Filter(new() { Biomes = ["Woodlands"], Collection = "Botanical Garden" }).Count == 1, "Biome and named collection intersect");
        Check(index.Filter(new() { Biomes = ["Woodlands", "Mountain"] }).Count == 4, "Multi-biome OR");
        Check(index.Filter(new() { Levels = ["All", "All", "Floor Breaks"] }).Count == 2, "Detail filter without parents spans actual paths");
        var ancestry = index.ResolveUpstream(new() { Levels = ["All", "All", "Floor Breaks"] }, 3);
        Check(ancestry.SequenceEqual(new[] { "Settlement", "Structures", "All", "Floor Breaks" }), "Shared ancestors fill without choosing an ambiguous branch");
        Check(index.Filter(new() { Context = ancestry[0], Levels = ancestry.Skip(1).ToArray() }).Count == 2, "Automatic ancestry retains all matching branches");
        Check(index.ResolveUpstream(new() { Biomes = ["Mountain"], Levels = ["All", "All", "Floor Breaks"] }, 3)
            .SequenceEqual(new[] { "Settlement", "Structures", "Aesthetics", "Floor Breaks" }), "Biome scope disambiguates upstream path");
        Check(index.ResolveUpstream(new() { Levels = ["All", "All", "Floor Breaks"], Tags = ["nonexistent"], Filename = "nothing", Materials = ["Wood"], Variant = "Z9", Collection = "unknown" }, 3)
            .SequenceEqual(ancestry), "Orthogonal filters do not invent ancestry or prevent completion");
        Check(index.ResolveUpstream(new() { Levels = ["All", "Building", "Floor Breaks"] }, 3)
            .SequenceEqual(new[] { "Settlement", "Structures", "Building", "Floor Breaks" }), "Explicit upstream choice is respected");
        Check(index.ResolveUpstream(new() { Levels = ["All", "All", "Missing"] }, 3)
            .SequenceEqual(new[] { "All", "All", "All", "Missing" }), "Missing types do not fabricate ancestry");
        Check(index.ResolveUpstream(new() { Levels = ["All", "All", "All"] }, 3).All(v => v == "All"), "Clearing a selection does not refill ancestors");
        Check(index.ResolveUpstream(new() { Levels = ["Furniture", "All", "All"] }, 1)[0] == "Settlement", "Category selection also fills unique context");
        var otherSource = dwarf with { SourceId = "other" };
        var sourceIndex = new SourceBrowserIndex([coreBreak, otherSource], [source, source with { Id = "other" }], new Dictionary<string, UserAssetTags>());
        Check(sourceIndex.ResolveUpstream(new() { SourceId = "fa", Levels = ["All", "All", "Floor Breaks"] }, 3)[2] == "Building", "Upstream completion respects selected library");
        Check(index.Filter(new() { Tags = ["Floor Breaks"] }).Count == 2, "Tags bridge Floor Breaks crossover");
        Check(index.Filter(new() { Materials = ["Wood"] }).Count == 3, "Broad material spans walls and furniture");
        Check(index.Filter(new() { Materials = ["Brick: Earthy", "Wood: Ashen"], AllMaterials = true }).Count == 1, "Match-all material pairs");
        Check(index.Filter(new() { Materials = ["Wood: Ashen", "Wood: Light"] }).Count == 3, "Match-any material finishes");
        Check(index.Filter(new() { Biomes = ["Woodlands"], Context = "Wilderness", Levels = ["Furniture"] }).Count == 0, "No fabricated cross-branch matches");
        var generic = SourceTaxonomy.Parse(garden, false);
        Check(generic.Biome == "" && generic.Levels[0] == "Woodlands", "Generic libraries retain paths without FA assumptions");
        var encoded = JsonSerializer.Serialize(new ApplicationState { UserTags = new() { [soup.StableIdentity] = tags }, BrowserFilter = new() { Biomes = ["Woodlands", "Base"] } });
        var decoded = JsonSerializer.Deserialize<ApplicationState>(encoded)!;
        Check(decoded.UserTags[soup.StableIdentity].Suppressed.Contains("Food") && decoded.BrowserFilter.Biomes.Length == 2, "User tags and selections round-trip");
    }
    static async Task Audit(string stateRoot, string output)
    {
        var timer = Stopwatch.StartNew();
        var state = await new ApplicationStateStore(stateRoot).LoadAsync();
        var index = new SourceBrowserIndex(state.Assets, state.Libraries, state.UserTags);
        var elapsed = timer.Elapsed;
        var random = new Random(20260910);
        var sample = index.Entries.OrderBy(_ => random.Next()).Take(500).Select(e => new { e.Asset.RelativePath, e.Taxonomy.Biome, e.Taxonomy.Context, e.Taxonomy.Collection, e.Taxonomy.Levels, e.Taxonomy.Materials, e.Taxonomy.Tags });
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "taxonomy-v1-audit.json"), JsonSerializer.Serialize(new {
            Assets = index.Entries.Count, LoadAndProjectionMs = elapsed.TotalMilliseconds,
            Biomes = index.Entries.GroupBy(e => e.Taxonomy.Biome).Select(g => new { Biome = g.Key, Count = g.Count() }),
            Contexts = index.Entries.GroupBy(e => e.Taxonomy.Context).Select(g => new { Context = g.Key, Count = g.Count() }),
            Directories = index.Entries.GroupBy(e => Path.GetDirectoryName(e.Asset.RelativePath)).Select(g => new { Path = g.Key, Count = g.Count(), g.First().Taxonomy.Biome, g.First().Taxonomy.Context, g.First().Taxonomy.Collection, g.First().Taxonomy.Levels }),
            Sample = sample }, new JsonSerializerOptions { WriteIndented = true }));
        Check(index.Entries.Count > 160000, "Real index loaded without rescanning source assets");
        timer.Restart(); var matches = index.Filter(new() { Tags = ["Floor Breaks"] });
        Console.WriteLine($"Floor Breaks: {matches.Count} in {timer.ElapsedMilliseconds} ms. Projection: {elapsed.TotalSeconds:F2}s");
        Check(matches.Any(e => e.Taxonomy.Biome == "Mountain") && matches.Any(e => e.Taxonomy.Biome == "Base"), "Real folder crossover retrieval");
        Console.WriteLine("Unknown contexts: " + index.Entries.Count(e => e.Taxonomy.Context.Length == 0));
        var ceramics = index.Entries.Where(e => e.Taxonomy.Materials.Any(m => m.Material == "Ceramic")).ToArray();
        await File.WriteAllTextAsync(Path.Combine(output, "ceramic-audit.json"), JsonSerializer.Serialize(new {
            Count = ceramics.Length,
            Finishes = ceramics.SelectMany(e => e.Taxonomy.Materials.Where(m => m.Material == "Ceramic")).GroupBy(m => m.Finish).Select(g => new { Finish = g.Key, Count = g.Count() }),
            UnexpectedTileMaterial = ceramics.Count(e => e.Taxonomy.Materials.Any(m => m.Material == "Tile")),
            Unpaired = ceramics.Where(e => e.Taxonomy.Materials.Any(m => m.Material == "Ceramic" && m.Finish.Length == 0)).Select(e => e.Asset.RelativePath).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    static int UiTests(string stateRoot, string output, bool startupOnly = false, bool plannerOnly = false)
    {
        Environment.SetEnvironmentVariable("DYMND_ASSET_BROWSER_STATE_ROOT", Path.GetFullPath(stateRoot));
        var app = new App(); app.InitializeComponent();
        bool started = false;
        // Closing the test window before completion must not count as success.
        int exit = 1;
        app.DispatcherUnhandledException += (_, e) => { Console.Error.WriteLine(e.Exception); e.Handled = true; exit = 1; app.Shutdown(1); };
        app.Activated += async (_, _) =>
        {
            if (started || app.MainWindow is not MainWindow window) return;
            started = true;
            try
            {
                var vm = (MainViewModel)window.DataContext;
                var overlay = (Grid)window.FindName("StartupOverlay");
                var progress = (ProgressBar)window.FindName("StartupProgress");
                var mainContent = (Grid)window.FindName("MainContent");
                await Task.Delay(200);
                Check(vm.IsStartupLoading && overlay.IsVisible && progress.IsIndeterminate, "Startup shows indeterminate loading bar");
                Check(!mainContent.IsEnabled, "Startup prevents premature browser interactions");
                Check(ReferenceEquals(progress.Foreground, app.FindResource("ActionAccentBrush")) && ((SolidColorBrush)progress.Foreground).Color == (Color)ColorConverter.ConvertFromString("#AE0AD8"), "Loading bar shares Populate purple accent");
                var center = progress.TranslatePoint(new Point(progress.ActualWidth / 2, progress.ActualHeight / 2), overlay);
                Check(Math.Abs(center.X - overlay.ActualWidth / 2) < 1 && Math.Abs(center.Y - overlay.ActualHeight / 2) < 1, "Loading bar sits at exact window content center");
                Capture(window, output, "browser-startup-loading.png");
                var sweep = (Border)progress.Template.FindName("Sweep", progress);
                var firstX = ((TranslateTransform)sweep.RenderTransform).X;
                await Task.Delay(160);
                Check(firstX != ((TranslateTransform)sweep.RenderTransform).X, "Purple loading sweep animates");
                await Wait(() => !vm.IsBusy && vm.IndexedAssetCount > 0);
                await Task.Delay(50);
                Check(!vm.IsStartupLoading && !overlay.IsVisible && !progress.IsIndeterminate && mainContent.IsEnabled, "Startup completion removes overlay and stops animation");
                Check(vm.IndexedAssetCount > 160000, "WPF real-index initialization");
                CheckNavigationTabs(vm, window);
                var wordmark = (Image)window.FindName("BrandWordmark");
                Check(wordmark.Source is CroppedBitmap brand && brand.PixelWidth == 890 && brand.PixelHeight == 540
                    && brand.Source.PixelWidth == 1024 && brand.Source.PixelHeight == 1024
                    && brand.SourceRect == new Int32Rect(114, 245, 890, 540)
                    && wordmark.Stretch == Stretch.Uniform && wordmark.IsVisible, "Supplied purple wordmark loads with display-only margin crop and proportional header scaling");
                var logoPixels = new FormatConvertedBitmap((BitmapSource)wordmark.Source, PixelFormats.Bgra32, null, 0);
                var logoBytes = new byte[logoPixels.PixelWidth * logoPixels.PixelHeight * 4];
                logoPixels.CopyPixels(logoBytes, logoPixels.PixelWidth * 4, 0);
                Check(logoBytes[3] == 0 && Enumerable.Range(0, logoBytes.Length / 4).Any(i =>
                    logoBytes[i * 4] == 168 && logoBytes[i * 4 + 1] == 13 && logoBytes[i * 4 + 2] == 157 && logoBytes[i * 4 + 3] == 255),
                    "Wordmark preserves supplied purple ink and transparent background");
                Check(System.Windows.Automation.AutomationProperties.GetName(wordmark) == "DYM&D", "Wordmark retains an accessible application name");
                using (var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/DYM%26D%20Asset%20Browser;component/Assets/Dymnd.ico")).Stream)
                {
                    var icon = BitmapDecoder.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    Check(icon.Frames.Select(f => f.PixelWidth).SequenceEqual(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }), "Application icon contains nine Windows sizes");
                    foreach (var frame in icon.Frames)
                    {
                        var pixels = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                        var bytes = new byte[pixels.PixelWidth * pixels.PixelHeight * 4];
                        pixels.CopyPixels(bytes, pixels.PixelWidth * 4, 0);
                        Check(bytes[3] == 0 && Enumerable.Range(0, bytes.Length / 4).Any(i =>
                            bytes[i * 4 + 3] > 200 && bytes[i * 4] > 120 && bytes[i * 4 + 1] < 40 && bytes[i * 4 + 2] > 120),
                            $"Purple icon at {frame.PixelWidth}px retains ink and transparency");
                    }
                }
                Check(window.Icon is not null, "Window icon loads from embedded branding resource");
                CaptureElement((FrameworkElement)window.FindName("AppBranding"), output, "app-branding.png");
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "startup-profile.json"), JsonSerializer.Serialize(vm.StartupTimingsMs, new JsonSerializerOptions { WriteIndented = true }));
                if (startupOnly) { Capture(window, output, "browser-ready.png"); exit = 0; return; }
                if (plannerOnly) { await vm.ShowBuildAsync(); await PlannerTests(vm, window, stateRoot, output); exit = 0; return; }
                var controls = (SourceBrowserControls)window.FindName("SourceControls");
                await UtilitiesTests(vm, window, output);
                await FilterShortcutUiTests(vm, window, stateRoot, output);
                await FilenameClearTests(vm, window, controls, output);
                Check(vm.SourceFacets.Count == 4 && vm.SourceFacets[3].Label == "Type", "Exactly four classification selectors plus Biome, no generated Detail levels");
                var materialButton = (System.Windows.Controls.Primitives.ToggleButton)controls.FindName("MaterialButton");
                var biomeButton = (System.Windows.Controls.Primitives.ToggleButton)controls.FindName("BiomeButton");
                var materialPopup = (System.Windows.Controls.Primitives.Popup)controls.FindName("MaterialPopup");
                var biomePopup = (System.Windows.Controls.Primitives.Popup)controls.FindName("BiomePopup");
                materialButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check(materialPopup.IsOpen == window.IsActive, "Materials popup only opens for the active owner");
                await Task.Delay(100); Capture(window, output, "material-button-open.png");
                Check(materialPopup.IsOpen ? materialButton.IsChecked == true && ((SolidColorBrush)((Border)materialButton.Template.FindName("Surface", materialButton)).Background).Color == (Color)ColorConverter.ConvertFromString("#39485A") : materialButton.IsChecked == false, "Material button reflects open/closed state without a stuck highlight");
                materialButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check(!materialPopup.IsOpen, "Second Materials click closes popup");
                biomeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check(biomePopup.IsOpen == window.IsActive, "Biome popup only opens for the active owner");
                biomeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check(!biomePopup.IsOpen, "Second Biome click closes popup");
                materialButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                window.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
                Check(!materialPopup.IsOpen, "Outside click closes Materials");
                biomeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                window.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
                Check(!biomePopup.IsOpen, "Outside click closes Biome");
                var wood = vm.MaterialGroups.Single(g => g.Name == "Wood");
                wood.Parent.IsSelected = true;
                Check(wood.Parent.IsSelected && wood.Choices.All(c => !c.IsSelected), "Collapsed parent selects any Wood");
                var ashen = wood.Choices.Single(c => c.Key == "Wood: Ashen"); ashen.IsSelected = true;
                Check(!wood.Parent.IsSelected && ashen.IsSelected, "Finish selection replaces broad parent");
                wood.Parent.IsSelected = true;
                Check(!ashen.IsSelected, "Selecting parent again clears finish restrictions");
                Check(wood.Choices.All(c => c.Label != "Any finish"), "No redundant Any finish child");
                vm.ClearMaterials(); await Task.Delay(300);
                var primaryCombos = Descendants<ComboBox>(controls).Where(c => c.DataContext is SourceFacetChoice f && vm.SourceFacets.Take(4).Contains(f)).ToArray();
                Check(primaryCombos.Length == 4 && primaryCombos.All(c => c.SelectedItem is string), "Every primary combo displays an actual selection");
                await Task.Delay(1000); Capture(window, output, "browser-v1-initial.png");
                vm.ResetFilters(); await Task.Delay(1000);
                vm.SourceFacets[1].Selected = "Workplace Equipment"; await Task.Delay(1000);
                Check(vm.MatchingAssetCount == 9119 && vm.Sections.Sum(s => s.Count) == vm.MatchingAssetCount, "All 9,119 Workplace Equipment matches are represented in sections");
                Check(vm.BrowserRows.Count == vm.Sections.Count && vm.BrowserRows.All(r => r.IsHeader && r.IsCollapsed), "Broad results show every section as a compact heading");
                Check(vm.Sections.All(s => !s.AreAssetsCreated), "Collapsed sections create no asset tile models");
                var workplaceNames = vm.Sections.Select(s => s.Name).ToArray();
                foreach (var term in new[] { "Fishing", "Leatherworking", "Adobe Brickmaking", "Dyeing", "Papyrus", "Tailoring" })
                    Check(workplaceNames.Any(n => n.Contains(term, StringComparison.OrdinalIgnoreCase)), "Workplace section is reachable: " + term);
                Check(vm.BrowserRows.All(r => r.Count > 0 && r.HeaderText.EndsWith($"({r.Count:N0})")), "Every section heading displays its full count");
                Capture(window, output, "workplace-all-sections.png");
                File.WriteAllText(Path.Combine(output, "workplace-sections.json"), JsonSerializer.Serialize(vm.Sections.Select(s => new { s.Name, s.Count }), new JsonSerializerOptions { WriteIndented = true }));
                var largeSection = vm.Sections.OrderByDescending(s => s.Count).First();
                vm.ToggleBrowserSection(largeSection.Name); await Task.Delay(800);
                Check(largeSection.Count > 480 && largeSection.Assets.Count == largeSection.Count && vm.BrowserRows.Where(r => !r.IsHeader).Sum(r => r.Assets.Count) == largeSection.Count, "Expanded section exposes every asset beyond the old 480 cap");
                Check(vm.Sections.Count(s => s.AreAssetsCreated) == 1, "Opening one section leaves other sections lazy");
                Check(largeSection.Assets.Count(t => t.Thumbnail is not null) < largeSection.Count, "Expanded section thumbnails remain viewport-lazy");
                var browserList = (ListBox)window.FindName("BrowserAssetList");
                var finalAssetRow = vm.BrowserRows.Last(r => !r.IsHeader);
                browserList.ScrollIntoView(finalAssetRow); await Task.Delay(1000);
                Check(browserList.ItemContainerGenerator.ContainerFromItem(finalAssetRow) is ListBoxItem, "Scrolling reaches the last row beyond the old cap");
                Check(finalAssetRow.Assets.Any(t => t.Thumbnail is not null), "Scrolling loads final-row thumbnails on demand");
                Capture(window, output, "workplace-last-row.png");
                var picks = largeSection.Assets.Take(4).ToArray();
                vm.SelectAsset(picks[0]); vm.SelectAsset(picks[2], shift: true);
                Check(vm.SelectedAssets.Count == 3, "Shift selection works in expanded sections");
                vm.SelectAsset(picks[3], control: true);
                Check(vm.SelectedAssets.Count == 4, "Control selection works in expanded sections");
                vm.ToggleBrowserSection(largeSection.Name); await Task.Delay(200);
                Check(vm.BrowserRows.All(r => r.IsHeader) && largeSection.Assets.All(t => t.Thumbnail is null), "Collapsing releases thumbnails without hiding other headings");
                vm.SelectAllBrowserResults();
                Check(vm.SelectionSummary.Contains("9,119"), "Select all includes collapsed and uncreated sections");
                vm.ToggleBrowserSection(largeSection.Name);
                Check(largeSection.Assets.All(t => t.IsSelected), "Expanding after Select all preserves selection coverage");
                vm.ToggleBrowserSection(largeSection.Name);
                vm.ResetFilters(); await Task.Delay(500);
                vm.SourceFacets[3].Selected = "Adobe Brickmaking"; await Task.Delay(800);
                Check(vm.SourceFacets.Select(f => f.Selected).SequenceEqual(new[] { "Settlement", "Workplace Equipment", "Tools", "Adobe Brickmaking" }), "Adobe Brickmaking populates its complete real-library upstream path");
                Check(vm.MatchingAssetCount > 0, "Completed Adobe path has results");
                Check(vm.Sections.Count == 1 && vm.BrowserRows.Any(r => !r.IsHeader), "Single-section results expand automatically");
                Capture(window, output, "browser-adobe-upstream.png");
                vm.SourceFacets[2].Selected = "All"; await Task.Delay(300);
                Check(vm.SourceFacets[2].Selected == "All" && vm.SourceFacets[3].Selected == "Adobe Brickmaking", "User can clear an upstream filter without forced refill");
                await vm.SaveStateAsync();
                var ancestrySaved = (await new ApplicationStateStore(stateRoot).LoadAsync()).BrowserFilter;
                Check(ancestrySaved.Context == "Settlement" && ancestrySaved.Levels.SequenceEqual(new[] { "Workplace Equipment", "All", "Adobe Brickmaking" }), "Completed and manually cleared hierarchy saves correctly");
                vm.ResetFilters(); await Task.Delay(300);
                vm.SetBiomes(["Woodlands", "Mountain"]); await Task.Delay(1000);
                Check(vm.SelectedBiomes.Count == 2 && vm.SourceFacets[0].Options.Contains("Wilderness"), "Biome UI cascade");
                vm.SourceFacets[0].Selected = "Settlement";
                vm.SourceFacets[1].Selected = "Furniture";
                await Task.Delay(1000);
                Check(vm.MatchingAssetCount > 0 && vm.SourceFacets[2].Options.Contains("Seating"), "Furniture drill-down");
                var popup = (System.Windows.Controls.Primitives.Popup)controls.FindName("MaterialPopup");
                popup.IsOpen = true; await Task.Delay(200);
                PreparePopupContent(popup);
                foreach (var expander in Descendants<Expander>(popup.Child).Take(2)) expander.IsExpanded = true;
                await Task.Delay(200); CapturePopup(popup, output, "browser-v1-material-picker.png");
                popup.IsOpen = false;
                Capture(window, output, "browser-v1-furniture.png");
                vm.SourceFacets[2].Selected = "Seating";
                vm.SetBiomes(["Base"]); await Task.Delay(1000);
                Check(vm.SourceFacets[1].Selected == "Furniture", "Valid downstream selection retained");
                vm.SetBiomes(["Woodlands"]); vm.SourceFacets[0].Selected = "Wilderness"; await Task.Delay(500);
                Check(vm.SourceFacets[1].Selected == "All" && vm.SourceFacets[1].Options.Contains("Flora"), "Invalid category cleared after context change");
                vm.ResetFilters(); vm.TagSearch = "Floor Breaks"; await Task.Delay(1200);
                Check(vm.MatchingAssetCount > 100, "Real tag search crosses folders");
                Capture(window, output, "browser-v1-floor-breaks.png");
                vm.SelectAllBrowserResults(); var count = vm.MatchingAssetCount;
                await vm.EditSelectedTagsAsync("V1 Test Review", false);
                await vm.ReindexAllLibrariesAsync();
                Check(!vm.StatusText.Contains("failed", StringComparison.OrdinalIgnoreCase), "Full reindex of real library succeeds read-only");
                vm.ResetFilters(); vm.TagSearch = "V1 Test Review"; await Task.Delay(1200);
                Check(vm.MatchingAssetCount == count, "Bulk tags include every result and survive full reindex");
                vm.SelectAllBrowserResults(); await vm.EditSelectedTagsAsync("V1 Test Review", true);
                await Task.Delay(1000); Check(vm.MatchingAssetCount == 0, "Bulk tag removal");
                vm.ResetFilters(); vm.SearchText = "Wall_BrickWood_Earthy_Ashen"; await Task.Delay(1500);
                var wallSection = vm.Sections.First();
                if (vm.BrowserRows.First(r => r.IsHeader && r.Name == wallSection.Name).IsCollapsed) vm.ToggleBrowserSection(wallSection.Name);
                var tile = wallSection.Assets.First();
                vm.SelectAsset(tile); vm.RotateSelected(90); vm.FlipSelectedHorizontally();
                Check(tile.RotationAngle == 90 && tile.IsFlippedHorizontally, "Existing rotation and flip behavior");
                var originalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(tile.Asset.FilePath)));
                var drag = await vm.GetDragFileAsync(tile);
                Check(drag != tile.Asset.FilePath && File.Exists(drag), "Transformed drag creates cache PNG");
                Check(originalHash == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(tile.Asset.FilePath))), "Source image remains byte-identical");
                vm.ThumbnailSize = 130; Capture(window, output, "browser-v1-small-thumbnails.png");
                Check(!vm.IsPlannerReady, "Browser and reindex do not initialize unused Planner");
                var recipeBefore = (await new ApplicationStateStore(stateRoot).LoadAsync()).BuildRecipe;
                await vm.SaveStateAsync();
                var recipeAfter = (await new ApplicationStateStore(stateRoot).LoadAsync()).BuildRecipe;
                Check(JsonSerializer.Serialize(recipeBefore) == JsonSerializer.Serialize(recipeAfter), "Browser-only save preserves deferred Planner recipe");
                await vm.ShowBuildAsync(); await Task.Delay(1000); Capture(window, output, "browser-v1-planner.png");
                Check(vm.IsBuildMode, "Planner remains accessible");
                Check(vm.IsPlannerReady && vm.FloorBuildSelector.Materials.Count > 1 && vm.WallsBuildSelector.WallSystems.Count > 1, "First Planner entry initializes usable selectors");
                Check(!vm.IsStartupLoading && !overlay.IsVisible, "Planner preparation does not show startup overlay");
                await PlannerTests(vm, window, stateRoot, output);
                File.WriteAllText(Path.Combine(output, "planner-preparation.json"), JsonSerializer.Serialize(new { vm.LastPlannerPreparationMs }));
                var firstPreparation = vm.LastPlannerPreparationMs;
                vm.ShowBrowser(); await vm.ShowBuildAsync();
                Check(vm.IsBuildMode && vm.LastPlannerPreparationMs == firstPreparation, "Subsequent Planner entry reuses prepared catalog");
                await vm.ReindexAllLibrariesAsync();
                Check(vm.IsPlannerReady && vm.FloorBuildSelector.Materials.Count > 1, "Reindex refreshes an initialized Planner");
                vm.ShowBrowser(); vm.ResetFilters(); await Task.Delay(500);
                vm.SourceFacets[1].Selected = "Workplace Equipment"; await Task.Delay(1000);
                var expandAll = (Button)window.FindName("ExpandAllBrowserButton");
                var collapseAll = (Button)window.FindName("CollapseAllBrowserButton");
                expandAll.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await Task.Delay(700);
                Check(vm.BrowserRows.Where(r => !r.IsHeader).Sum(r => r.Assets.Count) == 9119 && vm.BrowserRows.Where(r => r.IsHeader).All(r => !r.IsCollapsed), "Expand all exposes every filtered asset and category");
                Check(vm.Sections.SelectMany(s => s.Assets).Count(t => t.Thumbnail is not null) < 500, "Expand all retains viewport-lazy thumbnail loading");
                var unchangedRows = vm.BrowserRows.ToArray();
                expandAll.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check(vm.BrowserRows.SequenceEqual(unchangedRows), "Repeated Expand all is a no-op rather than rebuilding rows");
                Capture(window, output, "browser-expand-all.png");
                collapseAll.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await Task.Delay(300);
                Check(vm.BrowserRows.Count == vm.Sections.Count && vm.BrowserRows.All(r => r.IsCollapsed) && vm.Sections.SelectMany(s => s.Assets).All(t => t.Thumbnail is null), "Collapse all retains headings and releases thumbnails");
                Check(expandAll.TranslatePoint(new Point(), browserList).Y < 0 && vm.MatchingAssetCount == 9119, "Expansion buttons sit above the viewport without changing filters or counts");
                Capture(window, output, "browser-collapse-all.png");
                vm.ResetFilters(); await Task.Delay(500);
                await vm.SaveStateAsync();
                Console.WriteLine($"{_checks} checks passed including WPF integration.");
                exit = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; }
            finally
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(new { Version = typeof(MainWindow).Assembly.GetName().Version?.ToString(), Success = exit == 0, PassedChecks = _checks, Checks = Passed, SkippedVisualChecks = Skipped, Timestamp = DateTimeOffset.Now, LiveCspDropVerified = false }, new JsonSerializerOptions { WriteIndented = true }));
                window.Close(); app.Shutdown(exit);
            }
        };
        app.Run(); return exit;
    }
    static async Task Wait(Func<bool> done)
    { var timer = Stopwatch.StartNew(); while (!done()) { if (timer.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("UI initialization"); await Task.Delay(100); } }
    static void CheckNavigationTabs(MainViewModel vm, MainWindow window)
    {
        var browser = (Button)window.FindName("BrowserTabButton");
        var planner = (Button)window.FindName("PlannerTabButton");
        var accent = (SolidColorBrush)window.FindResource("ActionAccentBrush");
        var active = vm.IsBuildMode ? planner : browser;
        var inactive = vm.IsBuildMode ? browser : planner;
        Check(ReferenceEquals(active.BorderBrush, accent) && !ReferenceEquals(inactive.BorderBrush, accent)
            && active.BorderThickness == new Thickness(2) && inactive.BorderThickness == new Thickness(2),
            (vm.IsBuildMode ? "Planner" : "Browser") + " active tab has the purple outline, with no size jump on switching");
    }
    static async Task PlannerTests(MainViewModel vm, MainWindow window, string stateRoot, string output)
    {
        CheckNavigationTabs(vm, window);
        await vm.ResetBuildAsync(); await Task.Delay(400);
        await TagEditorTests(vm, window, stateRoot, output);
        await vm.ResetBuildAsync(); await Task.Delay(200);
        Check(vm.PlannerWallSets.Count > 500 && vm.BuildWallSampleAssets.Count == vm.PlannerWallSets.Count, "New Planner shows every matching wall set");
        Check(vm.BuildFloorSampleAssets.Count > 240 && vm.BuildFloorSampleAssets.Count == vm.FloorPlannerFilter.MatchingCount, "Floor previews are no longer truncated at 240");
        Check(vm.BuildTrimSampleAssets.Count > 240 && vm.BuildTrimSampleAssets.Count == vm.TrimPlannerFilter.MatchingCount, "Trim previews are no longer truncated at 240");
        Check(Descendants<PlannerFilterCard>(window).Count() == 3, "Three independent source-filter cards replace legacy Planner controls");
        Capture(window, output, "planner-source-overview.png");
        window.Width = 1000; window.Height = 680; await Task.Delay(500);
        Check(((ListBox)window.FindName("BuildAssetList")).ActualHeight > 100, "Small-window Planner keeps a usable palette viewport");
        Capture(window, output, "planner-small-window.png");
        window.Width = 1280; window.Height = 900; await Task.Delay(300);
        var wall = vm.WallsPlannerFilter;
        var card = (PlannerFilterCard)window.FindName("WallsPlannerCard");
        var biome = (System.Windows.Controls.Primitives.ToggleButton)card.FindName("BiomeButton");
        var material = (System.Windows.Controls.Primitives.ToggleButton)card.FindName("MaterialButton");
        var biomePopup = (System.Windows.Controls.Primitives.Popup)card.FindName("BiomePopup");
        var materialPopup = (System.Windows.Controls.Primitives.Popup)card.FindName("MaterialPopup");
        void Click(System.Windows.Controls.Primitives.ToggleButton button) => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Click(biome); Check(biomePopup.IsOpen == window.IsActive, "Planner biome popup respects foreground ownership"); Click(biome); Check(!biomePopup.IsOpen, "Planner biome button toggles closed");
        wall.SetBiomes(["Mountain", "Woodlands"]);
        Check(wall.SelectedBiomes.Count == 2 && wall.CollectionFacet.Options.Contains("Base"), "Planner supports multi-biome progressive choices");
        var collectionChoices = wall.CollectionFacet.Options.ToArray();
        Check(collectionChoices.Any(c => c.Contains("Dwarf", StringComparison.OrdinalIgnoreCase)), "Mountain exposes its Dwarven settlement collection");
        wall.SetBiomes(["Woodlands"]);
        Check(!wall.CollectionFacet.Options.Any(c => c.Contains("Dwarf", StringComparison.OrdinalIgnoreCase)), "Biome selection restricts settlement type choices");
        wall.Reset();
        Click(material); Check(materialPopup.IsOpen == window.IsActive, "Planner material popup respects foreground ownership");
        wall.SelectMaterials(["Wood: Ashen"]);
        var inclusiveWoodCount = wall.MatchingCount;
        var excludeCheck = (CheckBox)card.FindName("ExcludeMaterialsCheckBox");
        excludeCheck.IsChecked = true;
        Check(wall.ExcludeAdditionalMaterials && wall.MatchingCount > 0 && wall.MatchingCount < inclusiveWoodCount, "Exclude additional materials checkbox narrows Ashen Wood walls");
        Check(vm.PlannerWallSets.All(s => s.WallPieces.All(a => SourceTaxonomy.Parse(a).Materials.All(m => m.Material == "Wood"))), "Pure Wood preview has no compound wall pieces");
        Check(!vm.FloorPlannerFilter.ExcludeAdditionalMaterials && !vm.TrimPlannerFilter.ExcludeAdditionalMaterials, "Material exclusion is independent per Planner card");
        Check(wall.MaterialSummary.Contains("no other materials"), "Active exclusion is visible on the closed material button");
        Check(wall.MaterialGroups.Single(g => g.Name == "Brick").Choices.Single(c => c.Key == "Brick: Earthy").HasMatches, "Exclusion availability allows adding Brick to the permitted material types");
        var availablePureVariants = wall.VariantOptions.Where(o => o.HasMatches).Select(o => o.Value).ToArray();
        foreach (var variant in wall.VariantOptions.Where(o => o.Value != "All").ToArray())
        {
            wall.VariantFacet.Selected = variant.Value;
            Check((wall.MatchingCount > 0) == availablePureVariants.Contains(variant.Value), "Pure material variant availability agrees with results: " + variant.Value);
        }
        wall.VariantFacet.Selected = "All";
        wall.SelectMaterials(["Wood: Ashen", "Brick: Earthy"]); wall.MatchAllMaterials = true;
        Check(wall.MatchingCount > 0 && vm.PlannerWallSets.All(s => s.WallPieces.All(a => SourceTaxonomy.Parse(a).Materials.All(m => m.Material is "Wood" or "Brick"))), "Planner can require Wood plus Brick without other materials");
        CapturePopup(materialPopup, output, "planner-exclude-materials.png");
        var excludeState = JsonSerializer.Deserialize<PlannerSourceSelection>(JsonSerializer.Serialize(wall.Selection))!;
        wall.Reset(); wall.Restore(excludeState);
        Check(wall.ExcludeAdditionalMaterials && excludeCheck.IsChecked == true && wall.MatchingCount > 0, "Exclusion round-trips and restores its checkbox");
        wall.Reset();
        Check(!wall.ExcludeAdditionalMaterials && excludeCheck.IsChecked == false, "Reset restores inclusive default");
        await Task.Delay(100);
        var woodGroup = wall.MaterialGroups.Single(g => g.Name == "Wood");
        PreparePopupContent(materialPopup);
        var woodExpander = Descendants<Expander>(materialPopup.Child).Single(e => ReferenceEquals(e.DataContext, woodGroup));
        woodExpander.IsExpanded = true;
        woodGroup.Parent.IsSelected = true;
        woodGroup.Choices.Single(c => c.Key == "Wood: Ashen").IsSelected = true;
        Check(!woodGroup.Parent.IsSelected && woodExpander.IsExpanded && (materialPopup.IsOpen || !window.IsActive), "Material multiselect preserves popup and expanded groups");
        wall.MaterialGroups.Single(g => g.Name == "Brick").Choices.Single(c => c.Key == "Brick: Earthy").IsSelected = true;
        var anyCount = vm.PlannerWallSets.Count;
        wall.MatchAllMaterials = true;
        Check(vm.PlannerWallSets.Count > 0 && vm.PlannerWallSets.Count <= anyCount, "Real compound walls match both selected material finishes");
        Check(vm.PlannerWallSets.All(s => s.WallPieces.Any(a => SourceTaxonomy.Parse(a).Materials.Any(m => m.Key == "Wood: Ashen") && SourceTaxonomy.Parse(a).Materials.Any(m => m.Key == "Brick: Earthy"))), "Match-all does not fabricate material pairs across unrelated assets");
        CapturePopup(materialPopup, output, "planner-materials.png");
        Click(material); Check(!materialPopup.IsOpen, "Planner material button toggles closed");
        Check(vm.FloorPlannerFilter.Selection.Filter.Materials.Length == 0 && vm.TrimPlannerFilter.Selection.Filter.Materials.Length == 0, "Wall filtering does not overwrite independent Floor/Trim choices");
        wall.SelectMaterials(["Stone", "Wood: Ashen"]); wall.VariantFacet.Selected = "A";
        var stoneGroup = wall.MaterialGroups.Single(g => g.Name == "Stone");
        var unavailable = stoneGroup.Choices.Where(c => new[] { "Purple", "Redrock", "Volcanic" }.Contains(c.Label)).ToArray();
        Check(unavailable.Length == 3 && unavailable.All(c => !c.HasMatches && !c.CanToggle), "Stone Purple/Redrock/Volcanic are unavailable with Ashen wood in Match all");
        Check(stoneGroup.Choices.Where(c => new[] { "Earthy", "Sandstone", "Slate" }.Contains(c.Label)).Count(c => c.HasMatches && c.CanToggle) == 3, "Compatible Stone finishes stay available when replacing Any finish");
        Click(material); await Task.Delay(100);
        PreparePopupContent(materialPopup);
        var stoneExpander = Descendants<Expander>(materialPopup.Child).Single(e => ReferenceEquals(e.DataContext, stoneGroup));
        stoneExpander.IsExpanded = true; PreparePopupContent(materialPopup); if (materialPopup.IsOpen) stoneExpander.BringIntoView(); await Task.Delay(200);
        var purpleLabel = Descendants<TextBlock>(stoneExpander).Single(t => t.Text == "Purple");
        Check(purpleLabel.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough) && !purpleLabel.IsEnabled
            && purpleLabel.Foreground is SolidColorBrush gray && gray.Color == Color.FromRgb(138, 145, 153), "Unavailable material labels render grey, struck through and disabled");
        CapturePopup(materialPopup, output, "planner-material-availability.png");
        wall.MatchAllMaterials = false;
        Check(unavailable.All(c => c.HasMatches && c.CanToggle), "Match any re-enables Stone finishes that contribute their own results");
        wall.MatchAllMaterials = true; wall.Tags = "this-tag-does-not-exist";
        Check(stoneGroup.Parent.IsSelected && !stoneGroup.Parent.HasMatches && stoneGroup.Parent.CanToggle, "Selected zero-match parent remains removable");
        Check(wall.MaterialGroups.SelectMany(g => g.Choices).All(c => !c.HasMatches), "Tags constrain material availability");
        wall.MatchAllMaterials = false;
        Check(unavailable.All(c => !c.HasMatches), "Match any does not ignore a zero-match tag constraint");
        wall.Tags = ""; wall.SelectMaterials(["Wood: Ashen", "Brick: Earthy"]); wall.MatchAllMaterials = true;
        var selectedFinish = wall.MaterialGroups.Single(g => g.Name == "Wood").Choices.Single(c => c.Key == "Wood: Ashen");
        wall.Tags = "this-tag-does-not-exist";
        Check(selectedFinish.IsSelected && !selectedFinish.HasMatches && selectedFinish.CanToggle, "Selected zero-match finish remains removable");
        selectedFinish.IsSelected = false;
        Check(!selectedFinish.IsSelected && !selectedFinish.CanToggle, "Unavailable checked finish can be unchecked");
        wall.Tags = ""; wall.VariantFacet.Selected = "All"; wall.SelectMaterials(["Wood: Ashen", "Brick: Earthy"]);
        Check(ReferenceEquals(stoneGroup, wall.MaterialGroups.Single(g => g.Name == "Stone")) && stoneExpander.IsExpanded, "Availability updates preserve material groups and expansion");
        Click(material);
        wall.SelectMaterials(["Stone: Slate", "Wood: Ashen"]); wall.MatchAllMaterials = true;
        var l1 = wall.VariantOptions.Single(o => o.Value == "L1");
        Check(!l1.HasMatches && wall.VariantOptions.Single(o => o.Value == "A").HasMatches, "Slate and Ashen walls disable L1 but retain A");
        var variantCombo = (ComboBox)card.FindName("VariantCombo");
        variantCombo.IsDropDownOpen = true; await Task.Delay(150);
        if (variantCombo.IsDropDownOpen)
        {
            var l1Container = (ComboBoxItem)variantCombo.ItemContainerGenerator.ContainerFromItem(l1);
            l1Container.BringIntoView(); await Task.Delay(150);
            var l1Label = Descendants<TextBlock>(l1Container).Single(t => t.Text == "L1");
            Check(!l1Container.IsEnabled && l1Label.TextDecorations.Any(d => d.Location == TextDecorationLocation.Strikethrough)
                && l1Label.Foreground is SolidColorBrush variantGray && variantGray.Color == Color.FromRgb(138, 145, 153), "Variant L1 is visibly grey, struck through and disabled in the dropdown");
            CaptureElement((FrameworkElement)((System.Windows.Controls.Primitives.Popup)variantCombo.Template.FindName("PART_Popup", variantCombo)).Child, output, "planner-variant-availability.png");
        }
        else { Check(!window.IsActive, "Inactive owner blocks programmatic Variant popup"); Skipped.Add("Variant popup screenshot: owner inactive"); }
        variantCombo.IsDropDownOpen = false;
        wall.VariantFacet.Selected = "L1"; // Simulate a saved selection that has become invalid.
        Check(wall.MatchingCount == 0 && !l1.HasMatches && wall.VariantOptions.Single(o => o.Value == "A").HasMatches, "Variant alternatives ignore the current invalid variant restriction");
        Check((string?)variantCombo.SelectedValue == "L1", "Invalid existing variant remains visible rather than silently broadening filters");
        variantCombo.SelectedValue = "All";
        Check(wall.VariantFacet.Selected == "All" && wall.MatchingCount > 0, "Variant All clears an invalid saved selection");
        wall.ClearMaterials();
        Check(l1.HasMatches && ReferenceEquals(l1, wall.VariantOptions.Single(o => o.Value == "L1")), "Clearing material constraints re-enables L1 in place");
        foreach (var filter in new[] { wall, vm.FloorPlannerFilter, vm.TrimPlannerFilter })
        {
            filter.Tags = "this-tag-does-not-exist";
            Check(filter.VariantOptions.All(o => o.HasMatches == (o.Value == "All")), filter.Title + " variant availability honors tags with All always usable");
            filter.Tags = "";
        }
        wall.SelectMaterials(["Wood: Ashen", "Brick: Earthy"]);
        var beforeMissing = vm.PlannerWallSets.Count; wall.Tags = "this-tag-does-not-exist";
        Check(vm.PlannerWallSets.Count == 0 && vm.BuildWallAssets.Count == 0, "Planner tags can yield zero sets without unrelated fallback");
        wall.Tags = ""; Check(vm.PlannerWallSets.Count == beforeMissing, "Clearing tags restores candidate wall sets");
        wall.Reset();
        var sample = vm.BuildWallSampleAssets.First(t => t.Asset.FileName.Contains("BrickWood_Earthy_Ashen", StringComparison.OrdinalIgnoreCase));
        wall.SelectMaterials(["Wood: Ashen", "Brick: Earthy"]); wall.MatchAllMaterials = true;
        wall.Tags = SourceTaxonomy.Parse(sample.Asset).Levels.Last();
        Check(vm.PlannerWallSets.Count == 1 && !wall.HasPinnedSelection && vm.BuildWallAssets.Count == vm.PlannerSelectedWallSet!.WallPieces.Count, "Real filters alone resolve one full wall set without a preview click");
        wall.Reset();
        Check(vm.SelectPlannerSample(sample) && vm.PlannerSelectedWallSet?.Id == sample.WallSetId, "Clicking a wall sample narrows to its complete construction set");
        var chosen = vm.PlannerSelectedWallSet!;
        Check(vm.BuildWallAssets.Select(t => t.Asset.StableIdentity).ToHashSet().SetEquals(chosen.WallPieces.Select(a => a.StableIdentity)), "Clicked set includes every wall-piece shape");
        vm.SelectAsset(vm.BuildWallAssets.First());
        await vm.EditSelectedTagsAsync("Planner Unique Test", false);
        wall.ClearPinnedSelection(); wall.Tags = "Planner Unique Test"; wall.MatchAllTags = true;
        Check(vm.PlannerWallSets.Count == 1 && vm.BuildWallAssets.Count == chosen.WallPieces.Count, "Planner consumes user tags and still expands the whole tagged set");
        vm.SelectAsset(vm.BuildWallAssets.First()); await vm.EditSelectedTagsAsync("Planner Unique Test", true);
        Check(vm.PlannerWallSets.Count == 0, "Removing a user tag refreshes Planner matching immediately");
        wall.Reset(); wall.SelectIdentity(chosen.Id);
        var chosenFloor = vm.BuildFloorSampleAssets.First();
        Check(vm.SelectPlannerSample(chosenFloor) && vm.BuildFloorSampleAssets.Count == 1, "Clicking a floor still chooses that exact texture");
        await PlannerInteractionRegressionTests.Run(vm, window, Check);
        await PlannerMatchingRegressionTests.Run(vm, window, Check);
        vm.TrimPlannerFilter.SelectMaterials(["Wood: Ashen"]);
        Check(vm.BuildTrimSampleAssets.Count > 0 && vm.BuildTrimSampleAssets.All(t => SourceTaxonomy.Parse(t.Asset).Materials.Any(m => m.Key == "Wood: Ashen")), "Trim filters use source material finishes");
        vm.TrimPlannerFilter.ExcludeAdditionalMaterials = true;
        Check(vm.BuildTrimSampleAssets.Count > 0 && vm.BuildTrimSampleAssets.All(t => SourceTaxonomy.Parse(t.Asset).Materials.All(m => m.Material == "Wood")), "Trim honors material exclusion independently");
        vm.BuildCspTool = "Test Building"; vm.BuildCspToolGroup = "Test Walls"; vm.BuildCspRibbonName = "Planner regression ribbon";
        await vm.SaveBuildWallMappingAsync();
        Check(vm.BuildCspRibbonName == "Planner regression ribbon" && vm.BuildWallAssets.Count == chosen.WallPieces.Count, "Populate retains whole wall set and its single saved ribbon");
        Check(vm.BuildFloorAssets.Count == 1 && vm.BuildFloorAssets[0].Asset.StableIdentity == chosenFloor.Asset.StableIdentity, "Populate respects exact floor choice");
        var recipe = (await new ApplicationStateStore(stateRoot).LoadAsync()).BuildRecipe;
        Check(recipe.SourcePlanner?.Walls.SelectedIdentity == chosen.Id && recipe.SourcePlanner.Trim.Filter.Materials.Contains("Wood: Ashen"), "Source Planner selections persist explicitly");
        var reopened = new MainViewModel(new ApplicationStateStore(stateRoot));
        await reopened.InitializeAsync(); await reopened.ShowBuildAsync();
        Check(reopened.PlannerSelectedWallSet?.Id == chosen.Id && reopened.BuildFloorAssets.Count == 1 && reopened.BuildCspRibbonName == "Planner regression ribbon", "Saved source recipe restores its complete palette and ribbon after reopening");
        Check(recipe.SourcePlanner!.Trim.Filter.ExcludeAdditionalMaterials && reopened.TrimPlannerFilter.ExcludeAdditionalMaterials
            && !reopened.WallsPlannerFilter.ExcludeAdditionalMaterials, "Saved per-card exclusion survives application reopening");
        var floorFilterBefore = JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection);
        vm.ShowBrowser(); CheckNavigationTabs(vm, window); vm.SelectAllBrowserResults(); await vm.ShowBuildAsync(); CheckNavigationTabs(vm, window);
        Check(!vm.SelectionSummary.StartsWith("All "), "Browser Select all does not leak into Planner tag edits");
        await vm.ResetBuildComponentAsync(BuildComponent.Walls);
        Check(JsonSerializer.Serialize(vm.FloorPlannerFilter.Selection) == floorFilterBefore, "Reset Walls does not reset Floor");
        wall.Restore(recipe.SourcePlanner!.Walls); vm.FloorPlannerFilter.Restore(recipe.SourcePlanner.Floor); vm.TrimPlannerFilter.Restore(recipe.SourcePlanner.Trim);
        await vm.PopulateBuildAsync();
        ((Expander)window.FindName("BuildSetupExpander")).IsExpanded = false;
        await Task.Delay(700); Capture(window, output, "planner-populated.png");
        var leftList = (ListBox)window.FindName("BuildAssetList");
        var rightList = (ListBox)window.FindName("BuildRightAssetList");
        var leftScroll = Descendants<ScrollViewer>(leftList).First();
        var rightScroll = Descendants<ScrollViewer>(rightList).First();
        leftScroll.ScrollToTop(); rightScroll.ScrollToTop(); await Task.Delay(250);
        var trimHeaderIndex = vm.BuildRightPaletteRows.ToList().FindIndex(r => r.IsHeader && r.LeftSection == BuildPaletteSection.OtherOpenings);
        Check(trimHeaderIndex == 2 && vm.BuildPaletteRows.ToList().FindIndex(r => r.IsHeader && r.LeftSection == BuildPaletteSection.Detailing) > trimHeaderIndex,
            "One floor row is followed immediately by Trim, independently of wall count");
        var trimHeaderContainer = (ListBoxItem)rightList.ItemContainerGenerator.ContainerFromIndex(trimHeaderIndex);
        var trimPosition = trimHeaderContainer.TranslatePoint(new Point(), rightList);
        Check(trimPosition.Y >= 0 && trimPosition.Y + trimHeaderContainer.ActualHeight < rightList.ActualHeight,
            "Trim header is above the fold in the populated single-floor palette");
        Capture(window, output, "planner-independent-columns.png");
        var rightRowsBefore = vm.BuildRightPaletteRows.ToArray();
        leftScroll.ScrollToVerticalOffset(350); await Task.Delay(200);
        Check(leftScroll.VerticalOffset > 0 && rightScroll.VerticalOffset == 0, "Wall scrolling does not move Floor or Trim");
        vm.ToggleBuildSection(BuildPaletteSection.Walls); await Task.Delay(200);
        Check(vm.BuildRightPaletteRows.SequenceEqual(rightRowsBefore) && rightScroll.VerticalOffset == 0,
            "Collapsing Walls preserves right-column rows and scroll position");
        vm.ToggleBuildSection(BuildPaletteSection.Walls); leftScroll.ScrollToTop(); await Task.Delay(200);
        var leftRowsBefore = vm.BuildPaletteRows.ToArray();
        vm.ToggleBuildSection(BuildPaletteSection.Floors); await Task.Delay(150);
        Check(vm.BuildRightPaletteRows[1].IsHeader && vm.BuildRightPaletteRows[1].LeftSection == BuildPaletteSection.OtherOpenings
            && vm.BuildPaletteRows.SequenceEqual(leftRowsBefore), "Collapsing Floor brings Trim straight under its header without rebuilding Walls");
        vm.ToggleBuildSection(BuildPaletteSection.Floors);
        rightScroll.ScrollToBottom(); await Task.Delay(600);
        Check(rightScroll.VerticalOffset > 0 && leftScroll.VerticalOffset == 0, "Trim scrolling is independent from Walls");
        Check(vm.BuildRightPaletteRows.SelectMany(r => r.LeftAssets).Count() == vm.BuildFloorAssets.Count + vm.BuildDoorFrameAssets.Count + vm.BuildWindowSillAssets.Count + vm.BuildOtherOpeningAssets.Count,
            "Independent right column retains every floor and trim asset");
        Check(vm.BuildRightPaletteRows.Last().LeftAssets.Last().Thumbnail is not null, "Scrolling the right column loads its final trim thumbnail");
        Check(Descendants<ListBoxItem>(rightList).Count() < vm.BuildRightPaletteRows.Count, "Right-column rows remain virtualized");
        rightScroll.ScrollToTop(); leftScroll.ScrollToTop(); await Task.Delay(150);
        ((Expander)window.FindName("BuildSetupExpander")).IsExpanded = true;
        Capture(window, output, "planner-selected-set.png");
        await Task.Delay(200);
        Check(((ListBox)window.FindName("BuildAssetList")).ActualHeight > 240, "Compact expanded Planner leaves over 240px for assets at 1280x900");
        var clearWall = (Button)card.FindName("ClearChoiceButton");
        var wallScroller = (ScrollViewer)card.FindName("FilterBodyScroller");
        Check(clearWall.IsVisible && wallScroller.ScrollableHeight < 1, "Chosen-set action and all filters fit without scrolling at normal window size");
        window.Width = 1000; window.Height = 680; await Task.Delay(300);
        var fixedHeaderPosition = clearWall.TranslatePoint(new Point(), window);
        wallScroller.ScrollToBottom(); await Task.Delay(150);
        var scrolledHeaderPosition = clearWall.TranslatePoint(new Point(), window);
        var buttonInCard = clearWall.TranslatePoint(new Point(), card);
        Check(clearWall.IsVisible && Math.Abs(fixedHeaderPosition.Y - scrolledHeaderPosition.Y) < 1
            && buttonInCard.Y >= 0 && buttonInCard.Y + clearWall.ActualHeight <= card.ActualHeight, "Clear choice remains fully visible while a small-window card scrolls");
        Check(((ListBox)window.FindName("BuildAssetList")).ActualHeight > 160, "Compact minimum-size Planner retains over 160px of asset viewport");
        Capture(window, output, "planner-compact-small-pinned.png");
        var filtersBeforeClear = JsonSerializer.Serialize(wall.Selection.Filter);
        clearWall.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(!wall.HasPinnedSelection && JsonSerializer.Serialize(wall.Selection.Filter) == filtersBeforeClear, "Header Clear choice removes only the pin and preserves filters");
        wall.Restore(recipe.SourcePlanner.Walls);
        window.Width = 1280; window.Height = 900; wallScroller.ScrollToTop(); await Task.Delay(200);
        await PopupLifecycleTests(window);
        // Test mappings stay exclusively in the isolated state root, never the installed app.
        await vm.ResetBuildAsync();
    }
    static async Task TagEditorTests(MainViewModel vm, MainWindow window, string stateRoot, string output)
    {
        const string testTag = "Dialog Custom Test";
        var target = vm.BuildWallSampleAssets.First();
        vm.SelectAsset(target);
        var before = vm.TagDetails(target.Asset);
        var dialog = new TagEditorWindow(vm) { Owner = window };
        dialog.Show(); await Task.Delay(100);
        var input = (TextBox)dialog.FindName("TagInput");
        var apply = (Button)dialog.FindName("ApplyButton");
        var remove = (RadioButton)dialog.FindName("RemoveMode");
        var saved = (TextBlock)dialog.FindName("SaveStatus");
        input.Text = testTag;
        Check(apply.IsEnabled && vm.TagDetails(target.Asset) == before,
            $"Tag dialog stages changes without saving until Apply (enabled={apply.IsEnabled}, targets={vm.GetTagEditTargets().Length}, unchanged={vm.TagDetails(target.Asset) == before})");
        Check(((TextBlock)dialog.FindName("ChangePreview")).Text.Contains(testTag), "Tag dialog previews the exact pending edit");
        apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await Wait(() => saved.Text.StartsWith("Saved:") || saved.Text.StartsWith("Could not"));
        Check(saved.Text.StartsWith("Saved:") && dialog.IsVisible && !apply.IsEnabled, "Successful tag edit stays open with explicit saved confirmation");
        Check(((TextBox)dialog.FindName("TagDetailsText")).Text.Contains("User tags: " + testTag), "Dialog refreshes user tags after save");
        Capture(dialog, output, "tag-editor-saved.png");
        input.Text = testTag; remove.IsChecked = true;
        Check(((TextBlock)dialog.FindName("ChangePreview")).Text.Contains("Custom-only tags will be deleted"), "Removal preview explains custom deletion versus source suppression");
        apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await Wait(() => saved.Text.StartsWith("Saved:") || saved.Text.StartsWith("Could not"));
        var metadata = JsonSerializer.Deserialize<ApplicationState>(await File.ReadAllTextAsync(Path.Combine(stateRoot, "settings.json")))!;
        metadata.UserTags.TryGetValue(target.Asset.StableIdentity, out var userTags);
        Check(saved.Text.StartsWith("Saved:") && !(userTags?.Added.Contains(testTag) ?? false) && !(userTags?.Suppressed.Contains(testTag) ?? false),
            "Removing a custom tag persists neither an addition nor a suppression");
        input.Text = "Never Applied";
        ((Button)dialog.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Check(!vm.TagDetails(target.Asset).Contains("Never Applied"), "Closing the tag editor discards unconfirmed input");
        // Exercise the shared Browser/Planner context-menu construction without
        // changing the system clipboard or opening an Explorer window in tests.
        var tileElement = Descendants<Border>(window).First(b => b.DataContext is AssetTileViewModel && b.ContextMenu is not null);
        typeof(MainWindow).GetMethod("Asset_ContextMenuOpening", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(window, [tileElement, null]);
        var menuItems = tileElement.ContextMenu!.Items.OfType<MenuItem>().ToArray();
        Check(menuItems.Any(m => (string)m.Header == "Copy filepath") && menuItems.Any(m => (string)m.Header == "Copy full filepath")
            && menuItems.Any(m => (string)m.Header == "Show in File Explorer"), "Asset menu retains existing copy action and adds full Windows path and Explorer actions");
        var launch = DymndAssetBrowser.App.Services.ExplorerRevealService.CreateStartInfo(target.Asset.FilePath);
        Check(launch.FileName.EndsWith("explorer.exe") && launch.UseShellExecute && launch.Arguments == "/select,\"" + Path.GetFullPath(target.Asset.FilePath) + "\"",
            "Explorer action targets the original full source filepath for selection");
        var specialFile = Path.Combine(output, "spaces, commas & symbols.png");
        File.Copy(target.Asset.FilePath, specialFile, true);
        Check(DymndAssetBrowser.App.Services.ExplorerRevealService.CreateStartInfo(specialFile).Arguments == "/select,\"" + Path.GetFullPath(specialFile) + "\"",
            "Explorer selection safely quotes spaces, commas and ampersands");
        bool missingRejected = false;
        try { DymndAssetBrowser.App.Services.ExplorerRevealService.CreateStartInfo(Path.Combine(output, "missing-file.png")); }
        catch (FileNotFoundException) { missingRejected = true; }
        Check(missingRejected, "Explorer action reports unavailable source files without launching");
    }
    static void PreparePopupContent(System.Windows.Controls.Primitives.Popup popup)
    {
        // Layout-only inspection does not open the popup or steal foreground.
        if (popup.IsOpen) return;
        if (popup.Child is FrameworkElement child)
        {
            child.Measure(new Size(350, 800));
            child.Arrange(new Rect(new Point(), child.DesiredSize));
            child.UpdateLayout();
        }
    }
    static void CapturePopup(System.Windows.Controls.Primitives.Popup popup, string output, string name)
    {
        if (popup.IsOpen) CaptureElement((FrameworkElement)popup.Child, output, name);
        else Skipped.Add(name + ": popup closed / owner inactive");
    }
    static async Task UtilitiesTests(MainViewModel vm, MainWindow window, string output)
    {
        var root = Path.Combine(output, "disposable-cache-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "original.png");
        var pixels = Enumerable.Range(0, 40 * 20).SelectMany(_ => new byte[] { 40, 100, 210, 255 }).ToArray();
        var original = BitmapSource.Create(40, 20, 96, 96, PixelFormats.Bgra32, null, pixels, 40 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(original));
        using (var stream = File.Create(source)) encoder.Save(stream);
        var originalBytes = File.ReadAllBytes(source);
        var cache = new DymndAssetBrowser.App.Services.ImageCacheService(Path.Combine(root, "cache"));
        await cache.GetThumbnailAsync(source);
        var paths = new List<string>();
        for (int angle = 0; angle < 360; angle += 45)
        {
            var path = await cache.GetDragFileAsync(source, angle);
            paths.Add(path);
            Check(path == await cache.GetDragFileAsync(source, angle + 360), $"Normalized {angle}° transform reuses the same cache file");
            if (angle % 90 != 0)
            {
                var bmp = new BitmapImage(); bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.UriSource = new Uri(Path.GetFullPath(path)); bmp.EndInit(); bmp.Freeze();
                Check(bmp.PixelWidth == 43 && bmp.PixelHeight == 43, $"{angle}° rotation expands bounds without cropping the 40x20 source");
            }
        }
        var flipped = await cache.GetDragFileAsync(source, 45, true);
        Check(flipped != paths[1] && File.ReadAllBytes(source).SequenceEqual(originalBytes), "Flip has its own cached transform and original bytes remain untouched");
        var kind = DymndAssetBrowser.App.Services.ImageCacheKind.Transformed;
        Check((await cache.GetUsageAsync(kind)).Count == 8 && (await cache.GetUsageAsync(DymndAssetBrowser.App.Services.ImageCacheKind.Thumbnails)).Count == 1, "Cache utility counts generated thumbnail and transform categories separately");
        var unrelated = Path.Combine(root, "cache", "rotated", "keep-me.png"); File.Copy(source, unrelated, true);
        var nested = Path.Combine(root, "cache", "rotated", "nested"); Directory.CreateDirectory(nested);
        File.Copy(paths[1], Path.Combine(nested, Path.GetFileName(paths[1])), true);
        var dialog = new CacheUtilitiesWindow(cache) { Owner = window };
        dialog.Show(); await Task.Delay(500);
        Check(((TextBlock)dialog.FindName("TransformedUsage")).Text.StartsWith("8 files"), "Cache dialog loads counts only when opened");
        Capture(dialog, output, "cache-utilities.png"); dialog.Close();
        var cleared = await cache.ClearAsync(kind);
        Check(cleared.Deleted == 8 && cleared.Failed == 0 && File.Exists(unrelated) && Directory.GetFiles(nested).Length == 1 && File.ReadAllBytes(source).SequenceEqual(originalBytes), "Transform cleanup deletes only recognized direct children, preserving other files and originals");
        Check((await cache.GetUsageAsync(kind)).Count == 0 && (await cache.GetUsageAsync(DymndAssetBrowser.App.Services.ImageCacheKind.Thumbnails)).Count == 1, "Transform cleanup leaves thumbnail cache untouched");
        Check((await cache.ClearAsync(kind)).Deleted == 0 && File.Exists(await cache.GetDragFileAsync(source, 45)), "Repeated clearing is safe and removed transforms regenerate on demand");
        using (var locked = File.Open(paths[1], FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var busyResult = await cache.ClearAsync(kind);
            Check(busyResult.Deleted == 0 && busyResult.Failed == 1, "Locked transformed images are retained and reported as failed, not silently deleted");
        }
        Check((await cache.ClearAsync(kind)).Deleted == 1, "Previously locked cache file can be cleared after release");
        Check((await cache.ClearAsync(DymndAssetBrowser.App.Services.ImageCacheKind.Thumbnails)).Deleted == 1, "Thumbnail cleanup is independently scoped");
        var tile = new AssetTileViewModel(AssetRecordForTest(source));
        await vm.GetDragFileAsync(tile);
        Check(!vm.RandomRotationEnabled, "Random rotation starts off for a new application session");
        DymndAssetBrowser.App.Infrastructure.TransformShortcuts.Apply(vm, System.Windows.Input.Key.Q, System.Windows.Input.ModifierKeys.Shift);
        Check(tile.RotationAngle == 315, "Shift Q rotates left by 45 degrees");
        DymndAssetBrowser.App.Infrastructure.TransformShortcuts.Apply(vm, System.Windows.Input.Key.E, System.Windows.Input.ModifierKeys.Shift);
        DymndAssetBrowser.App.Infrastructure.TransformShortcuts.Apply(vm, System.Windows.Input.Key.E, System.Windows.Input.ModifierKeys.None);
        Check(tile.RotationAngle == 90, "Shift E reverses 45 degrees and ordinary E retains 90-degree increments");
        DymndAssetBrowser.App.Infrastructure.TransformShortcuts.Apply(vm, System.Windows.Input.Key.R, System.Windows.Input.ModifierKeys.None);
        DymndAssetBrowser.App.Infrastructure.TransformShortcuts.Apply(vm, System.Windows.Input.Key.R, System.Windows.Input.ModifierKeys.None, repeat: true);
        Check(vm.RandomRotationEnabled && !DymndAssetBrowser.App.Infrastructure.TransformShortcuts.Apply(vm, System.Windows.Input.Key.R, System.Windows.Input.ModifierKeys.Control), "R toggles once on key hold and ignores modified shortcuts");
        tile.FlipHorizontally();
        var randomPaths = new HashSet<string>();
        for (int i = 0; i < 24; i++) randomPaths.Add((await vm.GetDragFileAsync(tile))!);
        Check(randomPaths.All(p => Enumerable.Range(0, 8).Any(n => p.EndsWith($"_R{n * 45}_FH.png"))) && tile.RotationAngle == 90 && tile.IsFlippedHorizontally,
            "Random drags use only eight 45-degree angles, retain flip, and never mutate tile orientation");
        vm.RandomRotationEnabled = false;
        Check((await vm.GetDragFileAsync(tile))!.EndsWith("_R90_FH.png"), "Disabling random restores the manual rotation and flip");
        var search = (TextBox)((SourceBrowserControls)window.FindName("SourceControls")).FindName("SearchBox");
        search.Focus();
        if (search.IsKeyboardFocused)
        {
            var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, System.Windows.Input.Key.R) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            window.RaiseEvent(args);
            Check(!vm.RandomRotationEnabled && !args.Handled, "R remains ordinary text input while filename search is focused");
        }
        else Skipped.Add("Text-entry R event check: test owner inactive");
    }
    static AssetRecord AssetRecordForTest(string path) => new FaAssetFilenameParser().Parse(Path.GetDirectoryName(path)!, path);

    static void FilterShortcutCoreTests()
    {
        var asset = Asset(@"Woodlands\Botanical_Garden_Settlement\Structures\Building\Walls_and_Curbs\Wall_BrickWood_Earthy_Ashen_A_Corner_D_1x1.png");
        var taxonomy = SourceTaxonomy.Parse(asset);
        var entry = new SourceBrowserIndex.Entry(asset, taxonomy, ["Favorite", "Ashen"]);
        var options = AssetFilterShortcut.Options(entry);
        Check(options.Any(o => o.Key == "Material:Wood") && options.Any(o => o.Key == "Material:Wood: Ashen") && options.Any(o => o.Key == "Material:Brick: Earthy"), "Filter-to offers broad and correctly paired compound materials");
        Check(options.Where(o => o.IsTag).Select(o => o.Value).Order().SequenceEqual(new[] { "Ashen", "Favorite" }), "Filter-to tags use effective indexed tags, including custom edits");
        var filter = AssetFilterShortcut.Create(entry, ["Level:1", "Biome", "Material:Wood", "Material:Wood: Ashen", "Tag:Favorite", "Tag:Ashen"], "fa")!;
        Check(filter.Context == "Settlement" && filter.Levels.SequenceEqual(new[] { "Structures", "Building", "All" }), "Filter-to hierarchy includes ancestors but not unchosen descendants");
        Check(filter.Materials.SequenceEqual(new[] { "Wood: Ashen" }) && filter.AllTags && filter.Tags.Length == 2, "Filter-to normalizes broad material plus finish and requires all selected tags");
        Check(filter.SourceId == "fa" && filter.Filename == "" && filter.Identities is null && !filter.CaseSensitive, "Filter-to creates fresh constraints while retaining the chosen library");
        var compound = AssetFilterShortcut.Create(entry, ["Material:Wood: Ashen", "Material:Brick: Earthy"], "fa")!;
        Check(compound.AllMaterials && compound.Materials.Length == 2 && compound.Context == "All", "Multiple material characteristics combine with AND without imposing a category");
        var singleIndex = new SourceBrowserIndex([entry]);
        Check(options.All(o => singleIndex.Filter(AssetFilterShortcut.Create(entry, [o.Key], "fa")!).Count == 1)
            && singleIndex.Filter(AssetFilterShortcut.Create(entry, options.Select(o => o.Key), "fa")!).Count == 1, "Every individual or combined characteristic retains the clicked asset");
        Check(AssetFilterShortcut.Create(entry, ["invented"], "fa") is null && AssetFilterShortcut.Create(entry, [], "fa") is null, "Empty or invalid shortcut selections are no-ops");
        var generic = new SourceBrowserIndex.Entry(asset, new("", "", "", ["Custom", "Decor"], [], []), []);
        Check(!AssetFilterShortcut.Options(generic).Any(o => o.Key is "Biome" or "Context" || o.Key.StartsWith("Material:")), "Generic imports omit absent FA characteristics instead of inventing them");
        Check(new SourceBrowserIndex([generic]).Filter(AssetFilterShortcut.Create(generic, ["Level:1"], "fa")!).Count == 1, "Generic folder hierarchy can be filtered without a context");
    }

    static async Task FilterShortcutUiTests(MainViewModel vm, MainWindow window, string stateRoot, string output)
    {
        var state = await new ApplicationStateStore(stateRoot).LoadAsync();
        var indexed = new SourceBrowserIndex(state.Assets, state.Libraries, state.UserTags);
        var entry = indexed.Entries.First(e => e.Taxonomy.Materials.Any(m => m.Key == "Wood: Ashen")
            && e.Taxonomy.Materials.Any(m => m.Key == "Brick: Earthy") && e.Taxonomy.Level(2).Length > 0);
        var tile = new AssetTileViewModel(entry.Asset);
        var options = vm.BrowserFilterOptions(tile);
        var library = vm.SelectedSourceId;
        vm.SearchText = "this query should disappear";
        vm.TagSearch = "old tag"; vm.FilenameCaseSensitive = true;
        var mods = System.Windows.Input.ModifierKeys.Control;
        int applied = 0, closed = 0;
        var root = DymndAssetBrowser.App.Infrastructure.AssetFilterMenu.Create(options, keys => { applied++; vm.ApplyBrowserFilter(tile, keys); }, () => closed++, () => mods);
        var wood = root.Items.OfType<MenuItem>().Single(i => (string?)i.Tag == "Material:Wood: Ashen");
        var brick = root.Items.OfType<MenuItem>().Single(i => (string?)i.Tag == "Material:Brick: Earthy");
        var apply = root.Items.OfType<MenuItem>().Last();
        void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Click(wood);
        Check(wood.IsChecked && applied == 0 && closed == 0 && vm.SearchText.Length > 0 && apply.IsEnabled, "Ctrl-click queues a filter without applying or closing the menu");
        mods = System.Windows.Input.ModifierKeys.Shift; Click(brick);
        Check(brick.IsChecked && (string)apply.Header == "Apply selected (2)", "Shift-click also queues a second characteristic");
        Click(brick); Check(!brick.IsChecked && (string)apply.Header == "Apply selected (1)", "Modified click toggles a queued characteristic off");
        Click(brick); Click(apply); await Task.Delay(1200);
        Check(applied == 1 && closed == 1 && vm.SearchText == "" && vm.TagSearch == "" && !vm.FilenameCaseSensitive, "Apply selected closes menu and replaces filename and old tag constraints");
        Check(vm.MatchAllMaterials && vm.MaterialGroups.SelectMany(g => g.Choices).Count(c => c.IsSelected) == 2 && vm.SelectedSourceId == library, "Compound selection updates the visible material controls without changing library");
        var expected = indexed.Filter(AssetFilterShortcut.Create(entry, ["Material:Wood: Ashen", "Material:Brick: Earthy"], library)!);
        Check(vm.MatchingAssetCount == expected.Count && expected.Any(e => e.Asset.StableIdentity == entry.Asset.StableIdentity), "Browser results exactly match selected characteristics and include the original tile");
        mods = System.Windows.Input.ModifierKeys.None;
        var subcategory = root.Items.OfType<MenuItem>().Single(i => (string?)i.Tag == "Level:1");
        Click(subcategory); await Task.Delay(1200);
        Check(applied == 2 && vm.SourceFacets[0].Selected == entry.Taxonomy.Context && vm.SourceFacets[1].Selected == entry.Taxonomy.Level(0)
            && vm.SourceFacets[2].Selected == entry.Taxonomy.Level(1) && vm.SourceFacets[3].Selected == "All" && vm.MaterialSummary == "All materials", "Ordinary click immediately filters only that hierarchy and its ancestors");
        await vm.SaveStateAsync();
        var saved = await new ApplicationStateStore(stateRoot).LoadAsync();
        Check(saved.BrowserFilter.Filename == "" && saved.BrowserFilter.Levels[1] == entry.Taxonomy.Level(1), "Filter-to uses the normal persisted Browser filter state");
        var element = new Border { DataContext = tile, ContextMenu = new ContextMenu() };
        typeof(MainWindow).GetMethod("Asset_ContextMenuOpening", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, [element, null]);
        Check(element.ContextMenu.Items.OfType<MenuItem>().Any(i => (string)i.Header == "Filter to"), "Browser right-click menu exposes Filter to alongside existing actions");
        var canceled = DymndAssetBrowser.App.Infrastructure.AssetFilterMenu.Create(options, _ => applied++, () => closed++, () => System.Windows.Input.ModifierKeys.Control);
        Click(canceled.Items.OfType<MenuItem>().First(i => i.Tag is not null));
        Check(applied == 2 && closed == 2, "Closing or abandoning a staged menu requires no rollback because nothing applied");
        // Render a checked row offscreen without opening a native popup over other apps.
        var row = canceled.Items.OfType<MenuItem>().First(i => i.IsChecked);
        row.Measure(new Size(400, 40)); row.Arrange(new Rect(0, 0, 400, 40)); row.UpdateLayout();
        Check(((TextBlock)row.Template.FindName("CheckMark", row)).Visibility == Visibility.Visible, "Queued menu characteristic renders a visible checkmark");
        CaptureElement(row, output, "filter-to-checked-row.png");
        vm.ResetFilters(); await Task.Delay(1200);
    }
    static async Task FilenameClearTests(MainViewModel vm, MainWindow window, SourceBrowserControls controls, string output)
    {
        var search = (TextBox)controls.FindName("SearchBox");
        var clear = (Button)controls.FindName("ClearSearchButton");
        var previousQuery = vm.SearchText;
        var previousCase = vm.FilenameCaseSensitive;
        vm.SearchText = ""; await Task.Delay(100);
        Check(clear.Visibility == Visibility.Collapsed, "Empty filename search hides its clear button");
        vm.FilenameCaseSensitive = true;
        search.Text = "Wall_BrickWood_Earthy_Ashen"; await Task.Delay(100);
        Check(clear.IsVisible && vm.SearchText == search.Text, "Typing a filename query reveals the bound clear button");
        Check(search.Padding.Right >= clear.Width + clear.Margin.Right, "Filename text reserves space for the clear button");
        CaptureElement(controls, output, "filename-search-clear.png");
        clear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        await Task.Delay(100);
        Check(search.Text == "" && vm.SearchText == "" && clear.Visibility == Visibility.Collapsed, "Clear click empties the textbox and filter and hides itself");
        Check(search.CaretIndex == 0 && System.Windows.Input.FocusManager.GetFocusedElement(System.Windows.Input.FocusManager.GetFocusScope(search)) == search, "Clear click restores search focus and caret for the next query");
        if (window.IsActive) Check(search.IsKeyboardFocused, "Active-window clear click restores keyboard input to filename search");
        else Skipped.Add("Search clear keyboard-focus check: owner inactive; logical focus and caret verified");
        Check(vm.FilenameCaseSensitive, "Filename clear preserves the case-sensitive option");
        search.Text = " "; await Task.Delay(100);
        Check(clear.IsVisible, "Whitespace filename text also exposes a clear action");
        clear.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        search.Text = "new query"; await Task.Delay(100);
        Check(clear.IsVisible && vm.SearchText == "new query", "Search binding remains usable for the next query after clearing");
        vm.FilenameCaseSensitive = previousCase; vm.SearchText = previousQuery;
        await Task.Delay(1000);
    }
    static async Task PopupLifecycleTests(MainWindow window)
    {
        var card = (PlannerFilterCard)window.FindName("WallsPlannerCard");
        var combo = (ComboBox)card.FindName("VariantCombo");
        var popup = (System.Windows.Controls.Primitives.Popup)combo.Template.FindName("PART_Popup", combo);
        var material = (System.Windows.Controls.Primitives.Popup)card.FindName("MaterialPopup");
        var biome = (System.Windows.Controls.Primitives.Popup)card.FindName("BiomePopup");
        var browser = (SourceBrowserControls)window.FindName("SourceControls");
        var browserMaterial = (System.Windows.Controls.Primitives.Popup)browser.FindName("MaterialPopup");
        var browserBiome = (System.Windows.Controls.Primitives.Popup)browser.FindName("BiomePopup");
        var all = new[] { popup, material, biome, browserMaterial, browserBiome };
        Check(all.All(DymndAssetBrowser.App.Infrastructure.PopupOwnerGuard.GetEnabled), "ComboBox and Browser/Planner popups all carry owner guards");
        var selected = combo.SelectedValue;
        if (window.IsActive)
        {
            combo.IsDropDownOpen = true; await Task.Delay(50);
            Check(combo.IsDropDownOpen && popup.IsOpen, "Foreground Variant dropdown still opens normally");
            // Deterministically dispatch the real owner event without activating
            // another user's window during the regression run.
            typeof(Window).GetMethod("OnDeactivated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(window, [EventArgs.Empty]);
            Check(!combo.IsDropDownOpen && !popup.IsOpen, "Owner deactivation closes Variant and resets its open state");
        }
        else Skipped.Add("Foreground open/deactivation branch: owner already inactive");
        window.Hide(); await Task.Delay(50);
        Check(all.All(p => !p.IsOpen), "Hiding the owner leaves no detached popup visible");
        combo.IsDropDownOpen = true; material.IsOpen = true; biome.IsOpen = true; await Task.Delay(50);
        Check(!combo.IsDropDownOpen && all.All(p => !p.IsOpen), "Programmatic attempts cannot open popups for a hidden inactive owner");
        window.WindowState = WindowState.Minimized;
        combo.IsDropDownOpen = true; material.IsOpen = true; await Task.Delay(50);
        Check(!combo.IsDropDownOpen && !material.IsOpen, "Minimized owner cannot display Variant or material popups");
        window.WindowState = WindowState.Normal;
        window.ShowActivated = false; window.Show(); await Task.Delay(100);
        if (!window.IsActive)
        {
            combo.IsDropDownOpen = true; material.IsOpen = true; await Task.Delay(50);
            Check(!combo.IsDropDownOpen && !material.IsOpen, "Visible but background owner rejects automation-opened popups");
        }
        Check(Equals(selected, combo.SelectedValue), "Popup dismissal preserves the selected filter");
    }
    static void Capture(Window window, string output, string name)
        => CaptureElement(window, output, name);
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T typed) yield return typed; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
    static void CaptureElement(FrameworkElement window, string output, string name)
    {
        window.UpdateLayout(); var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp)); Directory.CreateDirectory(output);
        using var stream = File.Create(Path.Combine(output, name)); png.Save(stream);
    }
}
