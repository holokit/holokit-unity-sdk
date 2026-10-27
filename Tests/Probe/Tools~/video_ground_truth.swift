// Ground truth for the hand-video tests: Apple Vision on every frame of the source clip.
// Usage: swift video_ground_truth.swift hand-test.mp4 > hand-test.truth.json
import AVFoundation
import Vision

// HoloKit's JointName order (Runtime/iOS/JointName.cs).
let jointOrder: [VNHumanHandPoseObservation.JointName] = [
    .wrist, .thumbCMC, .thumbMP, .thumbIP, .thumbTip,
    .indexMCP, .indexPIP, .indexDIP, .indexTip, .middleMCP, .middlePIP, .middleDIP, .middleTip,
    .ringMCP, .ringPIP, .ringDIP, .ringTip, .littleMCP, .littlePIP, .littleDIP, .littleTip,
]

let url = URL(fileURLWithPath: CommandLine.arguments[1])
let asset = AVURLAsset(url: url)
let track = asset.tracks(withMediaType: .video)[0]
let reader = try! AVAssetReader(asset: asset)
let output = AVAssetReaderTrackOutput(track: track, outputSettings: [
    kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA])
reader.add(output)
reader.startReading()

var frames: [[String: Any]] = []
while let sample = output.copyNextSampleBuffer(), let buffer = CMSampleBufferGetImageBuffer(sample) {
    let time = CMTimeGetSeconds(CMSampleBufferGetPresentationTimeStamp(sample))
    let request = VNDetectHumanHandPoseRequest()
    request.maximumHandCount = 2
    try? VNImageRequestHandler(cvPixelBuffer: buffer, orientation: .up).perform([request])
    var hands: [[String: Any]] = []
    for observation in request.results ?? [] {
        guard let points = try? observation.recognizedPoints(.all),
              let wrist = points[.wrist], let thumb = points[.thumbTip], let index = points[.indexTip] else { continue }
        // Every joint as [x, y, confidence] in normalized image coordinates; [0, 0, 0] when missing.
        let joints = jointOrder.map { name -> [Double] in
            guard let p = points[name] else { return [0, 0, 0] }
            return [Double(p.location.x), Double(p.location.y), Double(p.confidence)]
        }
        hands.append(["x": Double(wrist.location.x), "y": Double(wrist.location.y),
                      "pinch": Double(hypot(thumb.location.x - index.location.x, thumb.location.y - index.location.y)),
                      "joints": joints])
    }
    // Largest-x-first ordering is not stable across frames; consumers match hands by position.
    frames.append(["t": time, "hands": hands])
}
let data = try! JSONSerialization.data(withJSONObject: ["fps": track.nominalFrameRate, "frames": frames])
FileHandle.standardOutput.write(data)
