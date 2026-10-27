// On-screen windows front to back as JSON: owner, title, layer, alpha and bounds (points). The probe
// runner compiles this once and polls it to see whether another window covers the Safari stimulus.
import CoreGraphics
import Foundation

let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
let rows: [[String: Any]] = windows.compactMap { window in
    guard let bounds = window[kCGWindowBounds as String] as? [String: CGFloat] else { return nil }
    return ["owner": window[kCGWindowOwnerName as String] as? String ?? "",
            "title": window[kCGWindowName as String] as? String ?? "",
            "layer": window[kCGWindowLayer as String] as? Int ?? 0,
            "alpha": window[kCGWindowAlpha as String] as? Double ?? 1,
            "x": bounds["X"] ?? 0, "y": bounds["Y"] ?? 0, "w": bounds["Width"] ?? 0, "h": bounds["Height"] ?? 0]
}
print(String(data: try! JSONSerialization.data(withJSONObject: rows), encoding: .utf8)!)
