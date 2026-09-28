// Runs Apple Vision's hand pose request on image files and prints the 21 joints of each hand
// (Vision's normalized, bottom-left coordinates and confidence) as JSON lines.
import Foundation
import Vision
import ImageIO

let names: [VNHumanHandPoseObservation.JointName] = [
    .wrist, .thumbCMC, .thumbMP, .thumbIP, .thumbTip, .indexMCP, .indexPIP, .indexDIP, .indexTip,
    .middleMCP, .middlePIP, .middleDIP, .middleTip, .ringMCP, .ringPIP, .ringDIP, .ringTip,
    .littleMCP, .littlePIP, .littleDIP, .littleTip]

let request = VNDetectHumanHandPoseRequest()
request.maximumHandCount = 2
for path in CommandLine.arguments.dropFirst() {
    guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { continue }
    let handler = VNImageRequestHandler(cgImage: image, orientation: .up)
    try? handler.perform([request])
    var hands: [[[Double]]] = []
    for observation in request.results ?? [] {
        hands.append(names.map { name in
            guard let p = try? observation.recognizedPoint(name) else { return [0, 0, 0] }
            return [Double(p.location.x), Double(p.location.y), Double(p.confidence)]
        })
    }
    let line: [String: Any] = ["file": path, "hands": hands, "width": image.width, "height": image.height]
    let data = try! JSONSerialization.data(withJSONObject: line)
    print(String(data: data, encoding: .utf8)!)
}
