/**************************************************************************
 *   GridAdjuster for Mac                                                 *
 *   TS2GridAdjuster © 2009-2010 Mootilda                                 *
 *   macOS port © 2026 GramzeSweatshop (rhiamom@mac.com)                  *
 *   Ported with Claude (Anthropic)                                       *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Headless harness. Drives Mootilda's own form logic (PrimaryForm.cs,  *
 *   unchanged) through the headless controls, the way a user clicked it. *
 *************************************************************************/

// Grid Adjuster smoke test.
//
//   list   <HOOD>                         lots: size, front, levels on the lot
//   grid   <HOOD> <lot#> [--level L]      a level's vertex heights in clicks
//                                         (lot-file layout), plus the ground
//                                         tile visibility and water table
//   adjust <HOOD> <lot#> --pattern flat|sloped|curved|hypar|basement
//          [--levels A:B] [--front A:B] [--left A:B]   her Range boxes
//          [--elev N] [--to N] [--mult N] [--rotate 0|90|180|270] [--relative]
//          [--amp N] [--phase N] [--period N]
//          [--remove] [--advanced --ground N --water N --no-ground --no-water --all-vertices]
//                                         her Specification screen + Finish;
//                                         values in clicks, as her form shows them
//
// <HOOD> is a hood code (N001) for its main package, or CODE/<package file>
// for a subhood. Options: --out DIR (work folder), --in-place (edit the real
// hood; never the default).
//
// Unless --in-place is given, the whole hood folder is first copied to a work
// folder and only the copy is changed.

using System.Drawing;
using System.Globalization;
using LotExpander;
using SimPe.Interfaces.Files;
using SimPe.Packages;

namespace GridAdjuster.SmokeTest;

internal static class Program
{
    private const uint DESC = 0x0BF999E7;
    private const uint ARY3 = 0x2A51171B;
    private const uint ARY2 = 0x6B943B43;
    private const uint WGRA = 0x0A284D0B;
    private const float Click = 0.1875F;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: list|grid|adjust <HOOD> [lot#] [options] [--out DIR] [--in-place]");
            return 2;
        }

        // Her message boxes: print, answer with the default button.
        MessageBox.Handler = (owner, m) =>
        {
            Console.WriteLine($"    message: [{m.Caption}] {m.Text.Replace("\n", " ")} -> {m.DefaultResult}");
            return m.DefaultResult;
        };

        string mode = args[0];
        string hoodArg = args[1];
        bool inPlace = args.Contains("--in-place");
        string? outDir = Opt(args, "--out");

        string? nbRoot = SimsPaths.NeighborhoodsFolder;
        if (nbRoot == null) { Console.Error.WriteLine("Sims 2 Neighborhoods folder not found."); return 1; }

        string code = hoodArg.Split('/')[0];
        string hoodDir = Path.Combine(nbRoot, code);
        if (!Directory.Exists(hoodDir)) { Console.Error.WriteLine($"No hood folder {hoodDir}"); return 1; }

        if (mode == "adjust" && !inPlace)
        {
            outDir ??= Path.Combine(Path.GetTempPath(), "gridadjuster-smoke", $"{code}-{DateTime.Now:yyyyMMdd-HHmmss}");
            string copy = Path.Combine(outDir, code);
            CopyDir(hoodDir, copy);
            Console.WriteLine($"Working on a copy: {copy}");
            hoodDir = copy;
        }

        string package = hoodArg.Contains('/')
            ? Path.Combine(hoodDir, hoodArg.Split('/', 2)[1])
            : Path.Combine(hoodDir, code + "_Neighborhood.package");

        return mode switch
        {
            "list" => List(package),
            "grid" => Grid(package, LotArg(args), Opt(args, "--level")),
            "adjust" => Adjust(package, LotArg(args), args),
            _ => 2,
        };
    }

    private static uint LotArg(string[] args) =>
        args.Length >= 3 && uint.TryParse(args[2].Replace("Lot", ""), out uint v) ? v
            : throw new ArgumentException("needs a lot number (see: list)");

    private static string? Opt(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static decimal? Num(string[] args, string name) =>
        Opt(args, name) is string s ? decimal.Parse(s, CultureInfo.InvariantCulture) : null;

    private static (int, int)? Range(string[] args, string name)
    {
        string? s = Opt(args, name);
        if (s == null) return null;
        string[] p = s.Split(':');
        return (int.Parse(p[0]), int.Parse(p[1]));
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.GetFiles(from))
            System.IO.File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        foreach (string d in Directory.GetDirectories(from))
            CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }

    private static PrimaryForm NewForm()
    {
        var form = new PrimaryForm();
        form.Show();    // Load + Shown, as WinForms did
        return form;
    }

    private static string LotPackagePath(string hoodPackage, uint instance)
    {
        string name = Path.GetFileName(hoodPackage);
        string prefix = name.Substring(0, name.IndexOf('_'));
        return Path.Combine(Path.GetDirectoryName(hoodPackage)!, "Lots", $"{prefix}_Lot{instance}.package");
    }

    // A c2DArray / c3DArray record: 64 zero bytes, block id, version, block
    // name, then width, height (and depth) and the elements, depth-major.
    private sealed class Ary
    {
        public int W, H, D, Size;
        public byte[] Data = Array.Empty<byte>();
        public int Header;

        public static Ary? Read(GeneratableFile pkg, uint type, uint inst)
        {
            IPackedFileDescriptor? pfd = pkg.FindFile(type, 0, 0xFFFFFFFF, inst);
            if (pfd == null) return null;
            var a = new Ary { Data = pkg.Read(pfd).UncompressedData };
            var br = new BinaryReader(new MemoryStream(a.Data));
            br.BaseStream.Position = 64 + 8;
            br.ReadString();
            a.W = br.ReadInt32();
            a.H = br.ReadInt32();
            a.D = type == ARY3 ? br.ReadInt32() : 1;
            a.Header = (int)br.BaseStream.Position;
            a.Size = (a.Data.Length - a.Header) / (a.W * a.H * a.D);
            return a;
        }

        private int Pos(int d, int w, int h) => Header + ((d * W + w) * H + h) * Size;
        public float F(int d, int w, int h) => BitConverter.ToSingle(Data, Pos(d, w, h));
        public byte B(int d, int w, int h) => Data[Pos(d, w, h)];
    }

    private static (int min, int max) Levels(GeneratableFile lot)
    {
        var w = new R_WGRA(lot, lot.FindFile(WGRA, 0, 0xFFFFFFFF, 5));
        return (w.MinimumLevel, w.MaximumLevel);
    }

    private static int List(string package)
    {
        var hood = SimPe.Packages.File.LoadFromFile(package);
        foreach (var pfd in hood.FindFiles(DESC).OrderBy(p => p.Instance))
        {
            var d = new R_DESC(hood, pfd, false);
            string lotPath = LotPackagePath(package, pfd.Instance);
            string levels = "not built";
            if (System.IO.File.Exists(lotPath))
            {
                try { var (lo, hi) = Levels(SimPe.Packages.File.LoadFromFile(lotPath)); levels = $"levels {lo}..{hi}"; }
                catch { levels = "no wall graph"; }
            }
            Console.WriteLine($"  Lot{pfd.Instance,-4} {d.Width}x{d.Height} U11 {d.U11} road {d.U10} type {d.LotType,2} {levels,-14} | {d.LotName}");
        }
        return 0;
    }

    private static string Clicks(float f)
    {
        float c = f / Click;
        return Math.Abs(c - MathF.Round(c)) < 0.01F ? MathF.Round(c).ToString(CultureInfo.InvariantCulture)
                                                    : c.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static int Grid(string package, uint lot, string? levelArg)
    {
        var pkg = SimPe.Packages.File.LoadFromFile(LotPackagePath(package, lot));
        var (lo, hi) = Levels(pkg);
        var g = Ary.Read(pkg, ARY3, 1)!;
        Console.WriteLine($"grid elevation array: {g.W}x{g.H} vertices, {g.D} levels in the array (wall graph levels {lo}..{hi}; array index 0 = level {lo})");
        for (int d = 0; d < g.D; d++)
        {
            var vals = new List<float>();
            for (int w = 0; w < g.W; w++) for (int h = 0; h < g.H; h++) vals.Add(g.F(d, w, h));
            Console.WriteLine($"  level {d + lo,2}: clicks {Clicks(vals.Min())} .. {Clicks(vals.Max())}  (most common {Clicks(vals.GroupBy(v => v).OrderByDescending(x => x.Count()).First().Key)})");
        }
        if (levelArg != null)
        {
            int d = int.Parse(levelArg) - lo;
            Console.WriteLine($"level {levelArg} vertex heights in clicks (rows = width index, columns = height index):");
            for (int w = 0; w < g.W; w++)
                Console.WriteLine($"  {w,3}: " + string.Join(" ", Enumerable.Range(0, g.H).Select(h => Clicks(g.F(d, w, h)).PadLeft(4))));
        }
        var vis = Ary.Read(pkg, ARY3, 0x15);
        if (vis != null)
        {
            int dg = -lo, hidden = 0;
            for (int w = 0; w < vis.W; w++) for (int h = 0; h < vis.H; h++) if (vis.B(dg, w, h) == 0) hidden++;
            Console.WriteLine($"ground tile visibility: {vis.W}x{vis.H}, {hidden} ground tiles hidden");
        }
        var water = Ary.Read(pkg, ARY2, 0x3B76);
        if (water != null)
        {
            var vals = new List<float>();
            for (int w = 0; w < water.W; w++) for (int h = 0; h < water.H; h++) vals.Add(water.F(0, w, h));
            Console.WriteLine($"water table: {water.W}x{water.H}, clicks {Clicks(vals.Min())} .. {Clicks(vals.Max())}");
        }
        return 0;
    }

    private static Dictionary<(uint, uint, uint, uint), byte[]> Snapshot(string path)
    {
        var pkg = SimPe.Packages.File.LoadFromFile(path);
        var all = new Dictionary<(uint, uint, uint, uint), byte[]>();
        // The package stand-in exposes type and instance; number repeats.
        foreach (var pfd in pkg.Index)
        {
            uint n = 0;
            while (all.ContainsKey((pfd.Type, 0, pfd.Instance, n))) n++;
            all[(pfd.Type, 0, pfd.Instance, n)] = pkg.Read(pfd).UncompressedData;
        }
        pkg.Close();
        return all;
    }

    // One pass through her screens: Lot list -> Next -> Specification -> Finish.
    private static int Adjust(string package, uint lot, string[] args)
    {
        string lotPath = LotPackagePath(package, lot);
        var before = Snapshot(lotPath);

        var hood = SimPe.Packages.File.LoadFromFile(package);
        string lotName = new R_DESC(hood, hood.FindFile(DESC, 0, 0xFFFFFFFF, lot), false).LotName;

        var form = NewForm();
        form.OpenNeighborhood(package);
        form.Liste.SelectedItem = lotName;
        if (!form.NextButton.PerformClick() || form.CurrentScreen != PrimaryForm.ScreenSpecification)
        {
            Console.WriteLine($"  could not open the lot's Specification screen: {form.Title.Text}");
            return 1;
        }
        Console.WriteLine($"  {form.Title.Text} ranges: levels {form.FromLevel.Value}..{form.ToLevel.Value}  front {form.FromFront.Value}..{form.ToFront.Value}" +
                          $"  left {form.FromLeft.Value}..{form.ToLeft.Value}  (U11 {form.LotRotation})");

        // Her boxes limit each other (From.Maximum = To.Value...), so set the
        // "to" ends first when widening and the "from" ends first otherwise.
        void SetRange(NumericUpDown from, NumericUpDown to, (int, int)? r)
        {
            if (r == null) return;
            if (r.Value.Item2 >= to.Value) { to.Value = r.Value.Item2; from.Value = r.Value.Item1; }
            else { from.Value = r.Value.Item1; to.Value = r.Value.Item2; }
        }
        SetRange(form.FromLevel, form.ToLevel, Range(args, "--levels"));
        SetRange(form.FromFront, form.ToFront, Range(args, "--front"));
        SetRange(form.FromLeft, form.ToLeft, Range(args, "--left"));

        string pattern = Opt(args, "--pattern") ?? "";
        RadioButton? radio = pattern switch
        {
            "flat" => form.PatternFlat, "sloped" => form.PatternSloped, "curved" => form.PatternCurved,
            "hypar" => form.PatternHyPar, "basement" => form.PatternBasement, _ => null,
        };
        if (radio == null) { Console.WriteLine("  --pattern flat|sloped|curved|hypar|basement"); return 2; }
        // Hypar is hidden in her released form; set it directly to test it anyway.
        if (radio == form.PatternHyPar) radio.Checked = true; else radio.PerformCheck();

        // Her order on screen: basement options, then the elevation boxes.
        if (args.Contains("--remove")) form.RemoveBasement.Checked = true;
        if (args.Contains("--advanced")) form.AdvancedBasementOptions.Checked = true;
        if (args.Contains("--no-ground")) form.AdjustGround.Checked = false;
        if (args.Contains("--no-water")) form.AdjustWater.Checked = false;
        if (args.Contains("--all-vertices")) form.NoMajorVertex.Checked = false;
        if (Num(args, "--elev") is decimal e) form.Offset.Value = e;
        if (Num(args, "--ground") is decimal gr) form.GroundOffset.Value = gr;
        if (Num(args, "--water") is decimal wa) form.WaterOffset.Value = wa;
        if (Num(args, "--to") is decimal to) form.SlopedOffset.Value = to;
        if (Num(args, "--amp") is decimal amp) form.CurvedAmplitude.Value = amp;
        if (Num(args, "--phase") is decimal ph) form.CurvedPhase.Value = ph;
        if (Num(args, "--period") is decimal pe) form.CurvedPeriod.Value = pe;
        if (Num(args, "--mult") is decimal mu) form.Multiplier.Value = mu;
        if (Num(args, "--rotate") is decimal ro) form.Rotate.Value = ro;
        if (args.Contains("--relative")) form.Relative.Checked = true;

        Console.WriteLine($"  {pattern}: levels {form.FromLevel.Value}..{form.ToLevel.Value}  front {form.FromFront.Value}..{form.ToFront.Value}" +
                          $"  left {form.FromLeft.Value}..{form.ToLeft.Value}  elevation {form.Offset.Value}  add/level {form.Multiplier.Value}" +
                          (form.PatternBasement.Checked ? $"  remove {form.RemoveBasement.Checked}  ground {(form.AdjustGround.Checked ? form.GroundOffset.Value.ToString() : "off")}" +
                                                          $"  water {(form.AdjustWater.Checked ? form.WaterOffset.Value.ToString() : "off")}  skip major {form.NoMajorVertex.Checked}" : "") +
                          (form.PatternSloped.Checked ? $"  to {form.SlopedOffset.Value}" : "") +
                          (form.PatternCurved.Checked ? $"  amp {form.CurvedAmplitude.Value} phase {form.CurvedPhase.Value} period {form.CurvedPeriod.Value}" : "") +
                          (form.RotatePanel.Visible ? $"  rotate {form.Rotate.Value} ({form.RotateDesc.Text})" : "") +
                          (form.Relative.Checked ? "  relative" : ""));
        form.NextButton.PerformClick();

        bool aborted = form.Title.ForeColor == Color.Red;
        bool final = form.CurrentScreen == PrimaryForm.ScreenFinal;
        Console.WriteLine($"  {(aborted ? "ABORTED" : final ? "DONE" : "STAYED")}: {form.Title.Text} | {form.Explanation.Text.Trim()}");
        if (!final || aborted) return 1;

        // Check on disk: only her arrays changed, nothing added or lost.
        var after = Snapshot(lotPath);
        var allowed = new HashSet<(uint, uint)> { (ARY3, 1), (ARY3, 0x15), (ARY2, 0x3B76) };
        const uint DIR = 0xE86B1EEF;    // compression directory, rebuilt on every save
        var changed = before.Keys.Where(k => k.Item1 != DIR)
            .Where(k => !after.ContainsKey(k) || !after[k].AsSpan().SequenceEqual(before[k])).ToList();
        bool sameSet = before.Count == after.Count && after.Keys.All(before.ContainsKey);
        bool onlyGrid = changed.All(k => allowed.Contains((k.Item1, k.Item3)));
        bool bkp = System.IO.File.Exists(Path.ChangeExtension(lotPath, ".bkp"));
        Console.WriteLine($"  verify: {changed.Count} resources changed ({string.Join(", ", changed.Select(k => $"{k.Item1:X8}/{k.Item3:X}"))})," +
                          $" same resource list: {(sameSet ? "yes" : "NO")}, only grid arrays: {(onlyGrid ? "yes" : "NO")}, backup: {(bkp ? "yes" : "NO")}");
        Grid(package, lot, null);
        return sameSet && onlyGrid && bkp ? 0 : 1;
    }
}
