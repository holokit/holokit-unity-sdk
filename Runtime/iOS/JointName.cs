#if UNITY_IOS
namespace HoloInteractive.XR.HoloKit.iOS
{
    // https://developer.apple.com/documentation/vision/vnhumanhandposeobservationjointname?language=objc
    public enum JointName
    {
        // The wrist.
        Wrist = 0,
        // The thumb’s carpometacarpal (CMC) joint.
        ThumbCMC = 1,
        //The thumb’s metacarpophalangeal (MP) joint.
        ThumbMP = 2,
        // The thumb’s interphalangeal (IP) joint.
        ThumbIP = 3,
        // The tip of the thumb.
        ThumbTip = 4,
        // The index finger’s metacarpophalangeal (MCP) joint.
        IndexMCP = 5,
        // The index finger’s proximal interphalangeal (PIP) joint.
        IndexPIP = 6,
        // The index finger’s distal interphalangeal (DIP) joint.
        IndexDIP = 7,
        // The tip of the index finger.
        IndexTip = 8,
        // The middle finger’s metacarpophalangeal (MCP) joint.
        MiddleMCP = 9,
        // The middle finger’s proximal interphalangeal (PIP) joint.
        MiddlePIP = 10,
        // The middle finger’s distal interphalangeal (DIP) joint.
        MiddleDIP = 11,
        // The tip of the middle finger.
        MiddleTip = 12,
        // The ring finger’s metacarpophalangeal (MCP) joint.
        RingMCP = 13,
        // The ring finger’s proximal interphalangeal (PIP) joint.
        RingPIP = 14,
        // The ring finger’s distal interphalangeal (DIP) joint.
        RingDIP = 15,
        // The tip of the ring finger.
        RingTip = 16,
        // The little finger’s metacarpophalangeal (MCP) joint.
        LittleMCP = 17,
        // The little finger’s proximal interphalangeal (PIP) joint.
        LittlePIP = 18,
        // The little finger’s distal interphalangeal (DIP) joint.
        LittleDIP = 19,
        // The tip of the little finger.
        LittleTip = 20
    }
}
#endif
