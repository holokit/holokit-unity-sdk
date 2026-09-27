// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

#include "HoloKitHandDepth.h"

#include <algorithm>
#include <cmath>

namespace holokit {

namespace {
constexpr std::uint8_t kMediumConfidence = 1; // ARConfidenceLevelMedium
}

bool reliableHandDepth(const HandDepthImage& image, float x, float y, float* depth) {
    if (image.depth == nullptr || image.width == 0 || image.height == 0 ||
        !std::isfinite(x) || !std::isfinite(y) || x < 0 || x > 1 || y < 0 || y > 1)
        return false;
    const long width = static_cast<long>(image.width), height = static_cast<long>(image.height);
    const long cx = std::min(width - 1, static_cast<long>(x * width));
    const long cy = std::min(height - 1, static_cast<long>((1 - y) * height));
    float samples[9];
    int count = 0;
    bool hasCentre = false;
    float centre = 0;
    for (long row = std::max(0L, cy - 1); row <= std::min(height - 1, cy + 1); row++) {
        for (long column = std::max(0L, cx - 1); column <= std::min(width - 1, cx + 1); column++) {
            if (image.confidence != nullptr &&
                image.confidence[row * image.confidenceRowBytes + column] < kMediumConfidence)
                continue;
            const float value = image.depth[row * image.depthRowFloats + column];
            if (!std::isfinite(value) || value <= 0.05f || value >= 4.0f) continue;
            samples[count++] = value;
            if (row == cy && column == cx) { hasCentre = true; centre = value; }
        }
    }
    if (count < 3 || !hasCentre) return false;
    std::sort(samples, samples + count);
    const float median = samples[count / 2];
    const float tolerance = std::max(0.08f, median * 0.08f);
    int inliers = 0;
    for (int i = 0; i < count; i++)
        if (std::fabs(samples[i] - median) <= tolerance) inliers++;
    if (std::fabs(centre - median) > tolerance || inliers < std::max(3, (2 * count + 2) / 3)) return false;
    *depth = median;
    return true;
}

bool handDepthMedian(float* values, int count, int minimumCount, float* median) {
    if (count < std::max(1, minimumCount)) return false;
    std::sort(values, values + count);
    const int middle = count / 2;
    *median = count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    return true;
}

bool currentHandDepth(bool measured, float measuredDepth, bool remembered, float rememberedDepth,
                      double rememberedTime, double now, float* depth) {
    if (measured) { *depth = measuredDepth; return true; }
    if (!remembered || now - rememberedTime > kHandDepthMemory) return false;
    *depth = rememberedDepth;
    return true;
}

bool selectJointDepth(bool hasOwn, float own, bool hasHand, float hand, float* depth) {
    if (hasOwn && (!hasHand || std::fabs(own - hand) <= kMaximumJointDepthFromHand)) {
        *depth = own;
        return true;
    }
    *depth = hasHand ? hand : kHandVisualizationDepth;
    return false;
}

} // namespace holokit
