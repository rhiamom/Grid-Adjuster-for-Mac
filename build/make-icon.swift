// GridAdjuster for Mac — app icon.
//
// Mootilda's TS2GridAdjuster had no icon, so this one is new, in the style of
// ConvertiWall for Mac's: a green build-mode grid seen at an angle, its surface
// lifted into a wave (grid elevations changed), with a blue badge holding an
// up/down arrow.
//
// Usage: swift build/make-icon.swift <out.png>   (1024x1024 PNG)
// build/make-icon.sh turns it into build/AppIcon.icns.

import AppKit

let size = 1024.0
let out = CommandLine.arguments[1]
let cs = CGColorSpace(name: CGColorSpace.sRGB)!
let ctx = CGContext(data: nil, width: Int(size), height: Int(size), bitsPerComponent: 8, bytesPerRow: 0,
                    space: cs, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!

func rgb(_ r: Double, _ g: Double, _ b: Double, _ a: Double = 1) -> CGColor { CGColor(srgbRed: r / 255, green: g / 255, blue: b / 255, alpha: a) }

// macOS icon grid: 824-point rounded square centred on the 1024 canvas.
let tile = CGRect(x: 100, y: 100, width: 824, height: 824)
let tilePath = CGPath(roundedRect: tile, cornerWidth: 185, cornerHeight: 185, transform: nil)

ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: -12), blur: 28, color: rgb(0, 0, 0, 0.30))
ctx.addPath(tilePath); ctx.setFillColor(rgb(255, 255, 255)); ctx.fillPath()
ctx.restoreGState()

ctx.saveGState()
ctx.addPath(tilePath); ctx.clip()
let sky = CGGradient(colorsSpace: cs, colors: [rgb(214, 234, 250), rgb(246, 250, 255)] as CFArray, locations: [0, 1])!
ctx.drawLinearGradient(sky, start: CGPoint(x: 0, y: 100), end: CGPoint(x: 0, y: 924), options: [])

// The grid: n x n tiles, isometric projection, height from a wave.
let n = 8
func height(_ i: Double, _ j: Double) -> Double {
    // Flat at the near and far edges, a rounded hill across the middle.
    let u = i / Double(n), v = j / Double(n)
    return 150 * sin(Double.pi * u) * sin(Double.pi * v) * (0.55 + 0.45 * cos(2 * Double.pi * (u - v) * 0.5))
}
let tileW = 46.0, tileH = 27.0
let origin = CGPoint(x: 500, y: 345)
func project(_ i: Double, _ j: Double) -> CGPoint {
    CGPoint(x: origin.x + (i - j) * tileW, y: origin.y + (i + j) * tileH + height(i, j))
}

// Back to front: larger i + j is further away (higher on screen), draw first.
var quads: [(Int, Int)] = []
for i in 0..<n { for j in 0..<n { quads.append((i, j)) } }
quads.sort { ($0.0 + $0.1) > ($1.0 + $1.1) }
for (i, j) in quads {
    let a = project(Double(i), Double(j)), b = project(Double(i + 1), Double(j))
    let c = project(Double(i + 1), Double(j + 1)), d = project(Double(i), Double(j + 1))
    // Shade by slope: tiles facing the viewer (rising towards the back) lighter.
    let slope = (height(Double(i) + 0.5, Double(j) + 1) - height(Double(i) + 0.5, Double(j)))
              - (height(Double(i) + 1, Double(j) + 0.5) - height(Double(i), Double(j) + 0.5))
    let k = max(-1.0, min(1.0, slope / 60))
    ctx.setFillColor(rgb(112 + 30 * k, 178 + 26 * k, 86 + 18 * k))
    ctx.move(to: a); ctx.addLine(to: b); ctx.addLine(to: c); ctx.addLine(to: d); ctx.closePath()
    ctx.fillPath()
    ctx.setStrokeColor(rgb(236, 248, 228, 0.95)); ctx.setLineWidth(5); ctx.setLineJoin(.round)
    ctx.move(to: a); ctx.addLine(to: b); ctx.addLine(to: c); ctx.addLine(to: d); ctx.closePath()
    ctx.strokePath()
}
ctx.restoreGState()

// Badge: blue disc, white ring, up/down arrow.
let c = CGPoint(x: 730, y: 275), R = 155.0
ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: -8), blur: 20, color: rgb(0, 0, 0, 0.35))
ctx.setFillColor(rgb(255, 255, 255)); ctx.fillEllipse(in: CGRect(x: c.x - R - 14, y: c.y - R - 14, width: 2 * R + 28, height: 2 * R + 28))
ctx.restoreGState()
ctx.setFillColor(rgb(36, 112, 214)); ctx.fillEllipse(in: CGRect(x: c.x - R, y: c.y - R, width: 2 * R, height: 2 * R))

ctx.setFillColor(rgb(255, 255, 255))
let shaftW = 34.0, shaftH = 120.0, headW = 120.0, headH = 70.0
ctx.fill(CGRect(x: c.x - shaftW / 2, y: c.y - shaftH / 2, width: shaftW, height: shaftH))
for s in [1.0, -1.0] {
    let base = c.y + s * shaftH / 2
    ctx.move(to: CGPoint(x: c.x, y: base + s * headH))
    ctx.addLine(to: CGPoint(x: c.x - headW / 2, y: base))
    ctx.addLine(to: CGPoint(x: c.x + headW / 2, y: base))
    ctx.closePath(); ctx.fillPath()
}

let rep = NSBitmapImageRep(cgImage: ctx.makeImage()!)
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: out))
