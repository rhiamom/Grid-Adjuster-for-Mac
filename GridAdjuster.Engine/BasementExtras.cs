/***************************************************************************
 *   GridAdjuster for Mac                                                  *
 *   macOS port © 2026 GramzeSweatshop (rhiamom@mac.com)                   *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                         *
 ***************************************************************************/
// "Finish basement" — a Mac extra, NOT part of Mootilda's TS2GridAdjuster.
//
// Her Basement pattern sinks the foundation storey (level 1) and the storeys
// above it, but leaves two things that were verified in game (N006 "basement
// test", 2026-10-02) to need fixing by hand:
//
//  1. The rim. The game keeps a foundation's height area one vertex wider
//     than its walls; that rim is the flat outside tile a front door needs.
//     Her pattern lowers level 2 and up only inside her rectangle, so the rim
//     on those levels stays a storey higher and doors will not snap.
//     Fix: give each rim vertex, on level 2 and up, the height of the
//     basement vertex next to it.
//
//  2. Objects. Doors, windows and furniture keep their old height and float a
//     storey up. Fix: lower each one by the drop, keeping it on its storey,
//     with her own R_OBJT.AddElevation (from her final LotAdjuster source;
//     she wrote it but never called it).
//
// Both steps are idempotent: a rim vertex that already matches is unchanged,
// and an object is lowered only while it floats a full drop above the floor
// it belongs to. So it is safe to run after each Basement pass of an L-shaped
// house, or twice by mistake.
//
// The basement is found from the lot itself: the ground tiles her pattern made
// invisible (3ARY 0x15, ground level), so no rectangles need typing in.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SimPe.Interfaces.Files;
using SimPe.Packages;

namespace LotExpander
{
    public static class BasementExtras
    {
        private const uint ARY3 = 0x2A51171B;   // 3D arrays: instance 1 = grid elevations, 0x15 = visible tiles
        private const uint OBJT = 0xFA1C39F7;   // object placement (her R_OBJT)
        private const uint XOBJ = 0x584F424A;   // object storey (her R_XOBJ: y, x at 80, storey at 88)
        private const uint XMTO = 0x584D544F;   // same layout, for the objects that have no XOBJ
                                                // (doors, windows, beds, ...) — found 2026-10-02
        private const float Eps = 0.01F;
        private static readonly bool Debug = Environment.GetEnvironmentVariable("GRIDADJUSTER_DEBUG") == "1";

        public sealed class Result
        {
            public int BasementVertices;
            public int RimValuesChanged;
            public int ObjectsLowered;
            public float Drop;
            public List<string> Lowered { get; } = new List<string>();
            public string Message;
        }

        // A c3DArray record: 64 zero bytes, id, version, name, width, height,
        // depth, then elements depth-major ([level][width][height]).
        private sealed class Ary3
        {
            public byte[] Data;
            public int W, H, D, Header, Size;
            public static Ary3 Read(GeneratableFile pkg, IPackedFileDescriptor pfd)
            {
                var a = new Ary3 { Data = (byte[])pkg.Read(pfd).UncompressedData.Clone() };
                var br = new BinaryReader(new MemoryStream(a.Data));
                br.BaseStream.Position = 72;
                br.ReadString();
                a.W = br.ReadInt32(); a.H = br.ReadInt32(); a.D = br.ReadInt32();
                a.Header = (int)br.BaseStream.Position;
                a.Size = (a.Data.Length - a.Header) / (a.W * a.H * a.D);
                return a;
            }
            private int Pos(int d, int w, int h) => Header + ((d * W + w) * H + h) * Size;
            public float F(int d, int w, int h) => BitConverter.ToSingle(Data, Pos(d, w, h));
            public void SetF(int d, int w, int h, float v) => BitConverter.GetBytes(v).CopyTo(Data, Pos(d, w, h));
            public byte B(int d, int w, int h) => Data[Pos(d, w, h)];
        }

        /// <param name="lotPath">The lot package her Basement pattern just saved.</param>
        /// <param name="groundIndex">Grid array index of level 0 (0 - her WGRA MinimumLevel).</param>
        public static Result Run(string lotPath, int groundIndex)
        {
            var r = new Result();
            GeneratableFile pkg = SimPe.Packages.File.LoadFromFile(lotPath);
            try
            {
                IPackedFileDescriptor gridPfd = pkg.FindFile(ARY3, 0, 0xFFFFFFFF, 1);
                IPackedFileDescriptor visPfd = pkg.FindFile(ARY3, 0, 0xFFFFFFFF, 0x15);
                if (gridPfd == null || visPfd == null)
                    return Fail(r, "This lot has no grid elevation or tile visibility array.");
                Ary3 grid = Ary3.Read(pkg, gridPfd);
                Ary3 vis = Ary3.Read(pkg, visPfd);
                int g = groundIndex, l1 = g + 1;
                if (grid.D < l1 + 2)
                    return Fail(r, "The lot needs at least two storeys on the foundation.");

                // Basement vertices: corners of hidden ground tiles.
                var basement = new bool[grid.W, grid.H];
                for (int w = 0; w < vis.W; w++)
                    for (int h = 0; h < vis.H; h++)
                        if (vis.B(g, w, h) == 0)
                            basement[w, h] = basement[w + 1, h] = basement[w, h + 1] = basement[w + 1, h + 1] = true;
                var rim = new bool[grid.W, grid.H];
                for (int w = 0; w < grid.W; w++)
                    for (int h = 0; h < grid.H; h++)
                    {
                        if (basement[w, h]) { r.BasementVertices++; continue; }
                        for (int dw = -1; dw <= 1 && !rim[w, h]; dw++)
                            for (int dh = -1; dh <= 1; dh++)
                            {
                                int a = w + dw, b = h + dh;
                                if (a >= 0 && a < grid.W && b >= 0 && b < grid.H && basement[a, b]) { rim[w, h] = true; break; }
                            }
                    }
                if (r.BasementVertices == 0)
                    return Fail(r, "No basement found on this lot (no hidden ground tiles). Run her Basement pattern first.");

                // The drop: how far the foundation storey sank (rim keeps the
                // old foundation height on level 1).
                var rimL1 = new List<float>();
                var inL1 = new List<float>();
                for (int w = 0; w < grid.W; w++)
                    for (int h = 0; h < grid.H; h++)
                        if (rim[w, h]) rimL1.Add(grid.F(l1, w, h));
                        else if (basement[w, h]) inL1.Add(grid.F(l1, w, h));
                r.Drop = Median(rimL1) - Median(inL1);
                if (r.Drop < 0.5F)
                    return Fail(r, $"The basement floor is not below the foundation (drop {r.Drop:0.##}).");

                // 1. Rim on level 2 and up: copy from a neighbouring basement vertex.
                for (int d = l1 + 1; d < grid.D; d++)
                    for (int w = 0; w < grid.W; w++)
                        for (int h = 0; h < grid.H; h++)
                        {
                            if (!rim[w, h]) continue;
                            float? src = null;
                            for (int dw = -1; dw <= 1 && src == null; dw++)
                                for (int dh = -1; dh <= 1; dh++)
                                {
                                    int a = w + dw, b = h + dh;
                                    if (a >= 0 && a < grid.W && b >= 0 && b < grid.H && basement[a, b]) { src = grid.F(d, a, b); break; }
                                }
                            if (src.HasValue && Math.Abs(grid.F(d, w, h) - src.Value) > Eps)
                            {
                                grid.SetF(d, w, h, src.Value);
                                r.RimValuesChanged++;
                            }
                        }
                if (r.RimValuesChanged > 0)
                    gridPfd.SetUserData(grid.Data, true);

                // 2. Objects in the house (basement or rim tiles).
                var storeys = new Dictionary<uint, int>();
                foreach (IPackedFileDescriptor x in pkg.FindFiles(XOBJ).Concat(pkg.FindFiles(XMTO)))
                {
                    byte[] xd = pkg.Read(x).UncompressedData;
                    if (xd.Length >= 92 && !storeys.ContainsKey(x.Instance)) storeys[x.Instance] = BitConverter.ToInt32(xd, 88);
                }
                FieldInfo coordField = typeof(R_OBJT).GetField("Coord", BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (IPackedFileDescriptor o in pkg.FindFiles(OBJT))
                {
                    var main = MainPosition(pkg, o, coordField);
                    if (Debug) Console.Error.WriteLine($"obj {o.Instance:X4} main {main} storey {(storeys.TryGetValue(o.Instance, out int ds) ? ds : -1)}");
                    if (main == null) continue;
                    var (x, y, z) = main.Value;
                    int tw = (int)Math.Floor(x), th = (int)Math.Floor(y);
                    if (tw < 0 || th < 0 || tw + 1 >= grid.W || th + 1 >= grid.H) continue;
                    bool inHouse = false;
                    for (int a = tw; a <= tw + 1; a++)
                        for (int b = th; b <= th + 1; b++)
                            if (basement[a, b] || rim[a, b]) inHouse = true;
                    if (!inHouse) continue;

                    if (!Floats(grid, tw, th, z, r.Drop, storeys.TryGetValue(o.Instance, out int s) ? s - 1 + g : (int?)null, g))
                        continue;
                    new R_OBJT(pkg, o).AddElevation(-r.Drop, z - Eps);
                    r.ObjectsLowered++;
                    r.Lowered.Add($"{o.Instance:X4} at ({x}, {y}) height {z:0.##} -> {z - r.Drop:0.##}");
                }

                if (r.RimValuesChanged == 0 && r.ObjectsLowered == 0)
                {
                    r.Message = "Nothing to do: the rim and objects are already right.";
                    return r;
                }

                // Save like her PackageSave, with a backup of its own so hers
                // (taken before the Basement pass) is kept.
                string dir = Path.GetDirectoryName(lotPath);
                string bkp = Path.Combine(dir, Path.GetFileNameWithoutExtension(lotPath) + "_extras.bkp");
                System.IO.File.Copy(lotPath, bkp, true);
                MemoryStream ms = pkg.Build();
                pkg.Close();
                pkg = null;
                System.IO.File.WriteAllBytes(lotPath, ms.ToArray());
                r.Message = $"Rim: {r.RimValuesChanged} heights fixed. Objects: {r.ObjectsLowered} lowered by {r.Drop / 0.1875F:0} clicks.";
                return r;
            }
            finally
            {
                pkg?.Close();
            }
        }

        private static Result Fail(Result r, string msg) { r.Message = msg; return r; }

        private static float Median(List<float> v)
        {
            if (v.Count == 0) return 0;
            v.Sort();
            return v[v.Count / 2];
        }

        // An object floats if it sits a full drop above the floor it belongs
        // to. With a storey (XOBJ) that floor is known; doors and windows have
        // none, so use the highest floor surface at or below the object.
        private static bool Floats(Ary3 grid, int tw, int th, float z, float drop, int? level, int g)
        {
            float floor;
            if (level.HasValue && level.Value > g && level.Value < grid.D)
                floor = Corner(grid, level.Value, tw, th);
            else
            {
                floor = float.MinValue;
                for (int d = g + 1; d < grid.D; d++)
                {
                    float f = Corner(grid, d, tw, th);
                    if (f <= z + Eps && f > floor) floor = f;
                }
                if (floor == float.MinValue) return false;
            }
            return z - floor >= drop - Eps;
        }

        // The highest corner of the tile. Inside the basement all four corners
        // agree; on a rim tile two corners are on the wall line (down with the
        // basement) and two on the rim, and the rim is the floor that tile's
        // objects (a porch plant, a door) stand on.
        private static float Corner(Ary3 grid, int d, int tw, int th) =>
            Math.Max(Math.Max(grid.F(d, tw, th), grid.F(d, tw + 1, th)),
                     Math.Max(grid.F(d, tw, th + 1), grid.F(d, tw + 1, th + 1)));

        // The object's own position, read by her R_OBJT: a pass that changes
        // nothing (minimum height = infinity) fills her Coord table.
        private static (float, float, float)? MainPosition(GeneratableFile pkg, IPackedFileDescriptor pfd, FieldInfo coordField)
        {
            byte[] before = pkg.Read(pfd).UncompressedData;
            try
            {
                var probe = new R_OBJT(pkg, pfd);
                probe.AddElevation(0F, float.MaxValue);
                var coords = (Array)coordField.GetValue(probe);
                foreach (int i in new[] { 0, 2 })   // Main (cObject), else Animatable entry 1
                {
                    object c = coords.GetValue(i);
                    if (c == null) continue;
                    Type t = c.GetType();
                    float x = (float)t.GetField("X").GetValue(c);
                    float y = (float)t.GetField("Y").GetValue(c);
                    float z = (float)t.GetField("Z").GetValue(c);
                    if (x > 0 || y > 0) return (x, y, z);
                }
                return null;
            }
            catch
            {
                return null;   // a record her parser does not know: leave it alone
            }
            finally
            {
                // The probe rewrote the record with identical bytes; make sure.
                byte[] after = pkg.Read(pfd).UncompressedData;
                if (!after.AsSpan().SequenceEqual(before))
                    pfd.SetUserData(before, true);
            }
        }
    }
}
