# GridAdjuster for Mac

A native macOS port of **TS2GridAdjuster** by Mootilda, the tool that changes the
grid elevations on a *Sims 2* lot. With it you can flatten, slope or curve the
ground and the floors of a built lot, and make **real basements with no sloped
ground around them**.

It runs natively on the Aspyr **Sims 2 Super Collection** (Intel and Apple
Silicon, macOS 10.15 or later). No Wine.

Mootilda's own program logic runs unchanged: her form code and her lot-file code
are compiled exactly as she wrote them, and only the Windows window has been
redrawn for the Mac. She has passed away, so her tools are preserved as she left
them. The one addition, **Finish basement**, is a separate step, described below.

## What it does

Choose a neighbourhood and a lot, then set:

- **Range**: which levels, plus a rectangle of tiles counted from the front and
  from the left of the lot.
- **Pattern**:
  - **Flat** sets the area to one height (on level 0 that flattens the
    ground; on a floor level it makes split levels or a sunken room).
  - **Sloped** makes a straight ramp from one height to another, in one of four directions.
  - **Curved** makes a wave (height, length and starting point in tiles).
  - **Basement** sinks the house into the ground (see below).
- **Elevation** in clicks (16 clicks = one wall height). Untick **Clicks** to
  use the game's raw numbers instead.
- **Relative** adds to the current heights instead of replacing them.

It changes only the lot's height data. Walls, floors and roofs follow the new
heights when the lot loads. A backup of the lot (`.bkp`) is made every time;
tick **Versioned backups** to keep numbered ones.

Quit the game before using it.

## Making a basement

These steps were worked out and tested in game on the Super Collection.

1. **Build a two-storey house on a foundation.** The storey sitting on the
   foundation **becomes the basement**, and the storey above becomes the ground
   floor. Build the basement rooms there, with walls, floor and furniture, at
   normal height where you can see them.
2. **Before converting, also add:**
   - the **stairs** from the basement storey up to the ground floor (they cannot
     be placed afterwards);
   - the **front door and windows on the upper storey**. Don't put doors or
     windows on the basement storey, and if they are already there, don't delete
     them afterwards: that leaves holes you can't close.
3. **Save the lot and quit the game.**
4. In GridAdjuster for Mac choose the lot, pick **Basement**, and enter the
   rectangle **one square in from the foundation edge on every side**, which
   means counting the tiles *inside* the foundation walls. A rectangle as big as the
   foundation leaves a gap one tile wide all round.
   - For an L-shaped house, do one rectangle per arm (use **Restart** in
     between). Let the second rectangle overlap the first by one tile, so no
     ridge of ground is left along the join.
5. Click **Finish**, then **Finish basement…** (see below).
6. Load the lot in the game.

The ground under the basement is dug deep and made invisible. The open space
below the basement floor is normal for this technique (the Mod The Sims guides
call it the "nether").

A stoop or porch foundation outside the front door is optional; stairs can go
straight to the tile outside the door. A stoop foundation built against the
house before converting ends up sloped, though a floor tile on top mostly hides it.

## Finish basement (a Mac extra)

Mootilda's Basement pattern lowers the floors but leaves two things for you to
fix by hand:

- the strip just outside the walls stays a storey too high on the new ground
  floor, so the **front door won't snap** onto the wall;
- **doors, windows and furniture float** a storey above where they belong.

After a successful Basement **Finish**, her last screen shows a
**Finish basement…** button that fixes both. It finds the basement from the lot
itself, so there is nothing to type in. It moves every object down by exactly the
amount the floors dropped and keeps each object on its own storey. To do that it
uses a routine of Mootilda's own that she wrote but never put to use. It is
safe to click after each pass of an L-shaped house, or twice by mistake:
anything already fixed is left alone. It saves a backup ending in
`_extras.bkp` first.

## Building from source

Requires the .NET 8 SDK.

```sh
dotnet build -c Release
dotnet run --project GridAdjuster.App -c Release
```

`build/release.sh [version]` builds, signs and notarizes the universal `.app`
and `.dmg` (needs the developer's signing identity).

`GridAdjuster.SmokeTest` runs her screens without a window, on a **copy** of a
neighbourhood (`list`, `grid`, `adjust`, `extras`). See the comments at the top of
its `Program.cs`. Set `GRIDADJUSTER_SIMS_FOLDER` to point both the app and the
test at a scratch copy of your Sims 2 folder.

## Credits and licence

- **TS2GridAdjuster** © 2009–2010 Mootilda.
- SimPE package code © 2004–2007 Ambertation (Quaxi).
- macOS port, Finish basement and icon © 2026 GramzeSweatshop (Rhiamom).

GNU General Public License, version 2 or later. See `Licences/GPL-LICENSE.txt`.
