import AppKit

let args = CommandLine.arguments
let outputPath = args.count > 1 ? args[1] : "dist/background.png"
let logoPath = args.count > 2 ? args[2] : "CherryHud/Assets/logo.png"

let logoImg = NSImage(contentsOfFile: logoPath)

// Extended 2880x1800 canvas so that if the user resizes or maximizes the Finder window,
// the background stays seamlessly dark (#13141C) without ever exposing a white void.
let totalW: CGFloat = 2880
let totalH: CGFloat = 1800
let windowW: CGFloat = 640
let windowH: CGFloat = 420

let image = NSImage(size: NSSize(width: totalW, height: totalH))
image.lockFocus()

// 1. Base dark theme background: #13141C (CherryHud HUD slate) across entire 2880x1800
let bg = NSColor(red: 19/255, green: 20/255, blue: 28/255, alpha: 1.0)
bg.setFill()
NSRect(x: 0, y: 0, width: totalW, height: totalH).fill()

// 2. Subtle dark gradient highlight in the primary 640x420 window viewport
let winGradient = NSGradient(
    colors: [
        NSColor(red: 26/255, green: 28/255, blue: 39/255, alpha: 1.0),
        NSColor(red: 19/255, green: 20/255, blue: 28/255, alpha: 1.0)
    ]
)
winGradient?.draw(in: NSRect(x: 0, y: totalH - windowH, width: windowW, height: windowH), angle: -90)

// 3. Top subtle accent bar in Cherry Ruby
let topBarRect = NSRect(x: 0, y: totalH - 3, width: totalW, height: 3)
NSColor(red: 212/255, green: 22/255, blue: 48/255, alpha: 1.0).setFill()
topBarRect.fill()

// 4. Header: Logo + Title + Subtitle
let logoSize: CGFloat = 38
let logoY = totalH - 68
let logoX: CGFloat = 48
if let logo = logoImg {
    logo.draw(in: NSRect(x: logoX, y: logoY, width: logoSize, height: logoSize))
}

let title = "CherryHud" as NSString
let titleAttrs: [NSAttributedString.Key: Any] = [
    .font: NSFont.systemFont(ofSize: 22, weight: .bold),
    .foregroundColor: NSColor.white
]
title.draw(at: NSPoint(x: logoX + logoSize + 14, y: logoY + 12), withAttributes: titleAttrs)

let subtitle = "Glissez CherryHud dans le dossier Applications" as NSString
let subAttrs: [NSAttributedString.Key: Any] = [
    .font: NSFont.systemFont(ofSize: 13, weight: .regular),
    .foregroundColor: NSColor(red: 154/255, green: 160/255, blue: 176/255, alpha: 1.0)
]
subtitle.draw(at: NSPoint(x: logoX + logoSize + 14, y: logoY - 8), withAttributes: subAttrs)

// 5. Dark icon cards with frosted light label pills (so Finder's black text is 100% readable)
let iconCenterY = totalH - 195
let cardW: CGFloat = 176
let cardH: CGFloat = 195
let cardY = iconCenterY - 105

func drawPillCard(centerX: CGFloat) {
    let r = NSRect(x: centerX - cardW / 2, y: cardY, width: cardW, height: cardH)
    let path = NSBezierPath(roundedRect: r, xRadius: 18, yRadius: 18)
    NSColor(red: 28/255, green: 30/255, blue: 42/255, alpha: 0.95).setFill()
    path.fill()
    NSColor(red: 255/255, green: 255/255, blue: 255/255, alpha: 0.12).setStroke()
    path.lineWidth = 1.0
    path.stroke()

    // Light frosted pill specifically behind the label text area (y: iconCenterY - 72, h: 26, w: 136)
    let pillW: CGFloat = 136
    let pillH: CGFloat = 26
    let pillRect = NSRect(x: centerX - pillW / 2, y: iconCenterY - 72, width: pillW, height: pillH)
    let pill = NSBezierPath(roundedRect: pillRect, xRadius: 8, yRadius: 8)
    NSColor(red: 240/255, green: 242/255, blue: 246/255, alpha: 0.95).setFill()
    pill.fill()
    NSColor(red: 255/255, green: 255/255, blue: 255/255, alpha: 0.3).setStroke()
    pill.lineWidth = 0.5
    pill.stroke()
}

drawPillCard(centerX: 160)
drawPillCard(centerX: 480)

// 6. Center Arrow Badge (Stem Green from Cherry logo)
let arrow = "➔" as NSString
let arrowAttrs: [NSAttributedString.Key: Any] = [
    .font: NSFont.systemFont(ofSize: 34, weight: .bold),
    .foregroundColor: NSColor(red: 112/255, green: 208/255, blue: 120/255, alpha: 1.0)
]
let arrowSize = arrow.size(withAttributes: arrowAttrs)
arrow.draw(at: NSPoint(x: (windowW - arrowSize.width) / 2, y: iconCenterY - 10), withAttributes: arrowAttrs)

image.unlockFocus()

if let tiff = image.tiffRepresentation,
   let rep = NSBitmapImageRep(data: tiff),
   let png = rep.representation(using: .png, properties: [:]) {
    try? png.write(to: URL(fileURLWithPath: outputPath))
    print("Dark DMG background generated at: \(outputPath)")
}
