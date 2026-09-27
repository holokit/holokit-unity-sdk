// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT
//
// Host tests for HoloKitHandDepth, the Swift SDK's HandProjection depth rules.

#include "HoloKitHandDepth.h"

#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <vector>

using namespace holokit;

static int failures = 0;
#define CHECK(condition, message) do { if (!(condition)) { std::printf("FAIL %s: %s\n", __func__, message); failures++; } } while (0)

// A 256 x 192 depth image (ARKit's scene depth size) filled with `value`, all high confidence.
struct Image {
    std::vector<float> depth = std::vector<float>(256 * 192);
    std::vector<std::uint8_t> confidence = std::vector<std::uint8_t>(256 * 192, 2);
    explicit Image(float value) { for (auto& d : depth) d = value; }
    float& at(int column, int row) { return depth[row * 256 + column]; }
    HandDepthImage view(bool withConfidence = true) {
        HandDepthImage image;
        image.depth = depth.data(); image.width = 256; image.height = 192; image.depthRowFloats = 256;
        if (withConfidence) { image.confidence = confidence.data(); image.confidenceRowBytes = 256; }
        return image;
    }
};

static void flatSurfaceGivesItsDepth() {
    Image image(0.42f);
    float depth = 0;
    CHECK(reliableHandDepth(image.view(), 0.5f, 0.5f, &depth), "flat surface is reliable");
    CHECK(std::fabs(depth - 0.42f) < 1e-6f, "the surface's depth");
}

static void pointsOutsideTheImageHaveNoDepth() {
    Image image(0.42f);
    float depth = 0;
    CHECK(!reliableHandDepth(image.view(), -0.01f, 0.5f, &depth), "left of the image");
    CHECK(!reliableHandDepth(image.view(), 0.5f, 1.01f, &depth), "above the image");
    CHECK(!reliableHandDepth(image.view(), NAN, 0.5f, &depth), "NaN");
    CHECK(reliableHandDepth(image.view(), 1.0f, 0.0f, &depth), "the edge itself is inside");
}

static void aFingertipOverTheBackgroundIsRejected() {
    // Vision y is up; row = (1 - y) * height. A tip pixel whose centre hit the far wall.
    Image image(0.40f);
    int column = 128, row = 96;
    image.at(column, row) = 2.5f;
    float depth = 0;
    CHECK(!reliableHandDepth(image.view(), (column + 0.5f) / 256, 1 - (row + 0.5f) / 192, &depth),
          "centre far from its neighbours' median");
    // Finger, knuckle shadow and wall a third each: no consistent depth.
    Image edge(0.40f);
    for (int r = row - 1; r <= row + 1; r++) { edge.at(column, r) = 1.0f; edge.at(column + 1, r) = 2.5f; }
    CHECK(!reliableHandDepth(edge.view(), (column + 0.5f) / 256, 1 - (row + 0.5f) / 192, &depth),
          "fewer than two thirds of the samples agree");
}

static void lowConfidenceAndOutOfRangeSamplesAreSkipped() {
    Image image(0.40f);
    for (auto& c : image.confidence) c = 0;
    float depth = 0;
    CHECK(!reliableHandDepth(image.view(), 0.5f, 0.5f, &depth), "low confidence everywhere");
    CHECK(reliableHandDepth(image.view(false), 0.5f, 0.5f, &depth), "no confidence map: every sample counts");
    Image tooClose(0.03f), tooFar(5.0f);
    CHECK(!reliableHandDepth(tooClose.view(), 0.5f, 0.5f, &depth), "closer than 5 cm");
    CHECK(!reliableHandDepth(tooFar.view(), 0.5f, 0.5f, &depth), "further than 4 m");
}

static void handDepthIsTheMedianOfAtLeastThree() {
    float values[] = { 0.5f, 0.3f, 0.4f, 0.9f };
    float median = 0;
    CHECK(handDepthMedian(values, 4, 3, &median), "four values");
    CHECK(std::fabs(median - 0.45f) < 1e-6f, "mean of the middle two");
    float two[] = { 0.3f, 0.4f };
    CHECK(!handDepthMedian(two, 2, 3, &median), "two joints are not enough");
}

static void handDepthIsRememberedForHalfASecond() {
    float depth = 0;
    CHECK(currentHandDepth(true, 0.4f, true, 0.6f, 0.0, 10.0, &depth) && depth == 0.4f, "measured wins");
    CHECK(currentHandDepth(false, 0, true, 0.6f, 10.0, 10.5, &depth) && depth == 0.6f, "remembered at 0.5 s");
    CHECK(!currentHandDepth(false, 0, true, 0.6f, 10.0, 10.51, &depth), "forgotten after 0.5 s");
    CHECK(!currentHandDepth(false, 0, false, 0, 0, 0, &depth), "nothing to remember");
}

static void jointsTakeTheHandDepthWhenTheirOwnIsOff() {
    float depth = 0;
    CHECK(selectJointDepth(true, 0.45f, true, 0.40f, &depth) && depth == 0.45f, "own depth within 10 cm");
    CHECK(!selectJointDepth(true, 0.60f, true, 0.40f, &depth) && depth == 0.40f, "own depth 20 cm behind: the hand's");
    CHECK(!selectJointDepth(false, 0, true, 0.40f, &depth) && depth == 0.40f, "no own depth: the hand's");
    CHECK(selectJointDepth(true, 0.9f, false, 0, &depth) && depth == 0.9f, "no hand depth: its own");
    CHECK(!selectJointDepth(false, 0, false, 0, &depth) && depth == kHandVisualizationDepth, "neither: the 0.65 m plane");
}

int main() {
    flatSurfaceGivesItsDepth();
    pointsOutsideTheImageHaveNoDepth();
    aFingertipOverTheBackgroundIsRejected();
    lowConfidenceAndOutOfRangeSamplesAreSkipped();
    handDepthIsTheMedianOfAtLeastThree();
    handDepthIsRememberedForHalfASecond();
    jointsTakeTheHandDepthWhenTheirOwnIsOff();
    if (failures == 0) std::printf("All hand depth tests passed\n");
    return failures == 0 ? 0 : 1;
}
