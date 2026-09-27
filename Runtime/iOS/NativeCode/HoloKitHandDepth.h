// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT
//
// Hand joint depth from ARKit's LiDAR scene depth, ported from holokit-swift-sdk
// (HandProjection.swift, HandTracker.swift). Pure C++: no ARKit or CoreVideo, so
// Tests/Hands/Native~ builds and runs it on the Mac.
//
// Image points are Vision's normalized coordinates on the native captured image
// (origin bottom-left), which share the scene depth image's axes.
#pragma once

#include <cstddef>
#include <cstdint>

namespace holokit {

/// Joints without a depth of their own, and no hand depth, sit on this plane (m).
constexpr float kHandVisualizationDepth = 0.65f;
/// A joint's own depth further than this from the hand's depth hit the background
/// past a thin fingertip (device trace: 26% of measured fingertips were 25-60 cm
/// from the wrist); the hand's depth is used instead (m).
constexpr float kMaximumJointDepthFromHand = 0.10f;
/// How long a hand keeps its last depth when LiDAR misses it (s).
constexpr double kHandDepthMemory = 0.5;
/// Joints below this Vision confidence do not vote for the hand's depth.
constexpr float kHandDepthMinimumConfidence = 0.3f;

/// A scene depth image (metres) with its optional confidence map (ARConfidenceLevel,
/// one byte per pixel, same size), rows `depthRowFloats` / `confidenceRowBytes` apart.
struct HandDepthImage {
    const float* depth = nullptr;
    std::size_t width = 0, height = 0, depthRowFloats = 0;
    const std::uint8_t* confidence = nullptr;
    std::size_t confidenceRowBytes = 0;
};

/// The 3x3 median depth around an image point, when at least medium-confidence,
/// in 0.05-4 m, and consistent (the centre and two thirds of the samples within
/// max(8 cm, 8%) of the median); false otherwise.
bool reliableHandDepth(const HandDepthImage& image, float x, float y, float* depth);

/// Median of `count` values (sorted in place; the mean of the two middle ones for
/// an even count), false with fewer than `minimumCount`.
bool handDepthMedian(float* values, int count, int minimumCount, float* median);

/// This frame's measured hand depth, else the remembered one while it is younger
/// than kHandDepthMemory; false when neither.
bool currentHandDepth(bool measured, float measuredDepth, bool remembered, float rememberedDepth,
                      double rememberedTime, double now, float* depth);

/// A joint's depth: its own reliable depth unless that is more than
/// kMaximumJointDepthFromHand from the hand's, else the hand's, else the fixed
/// plane. Returns whether the joint's own depth was used.
bool selectJointDepth(bool hasOwn, float own, bool hasHand, float hand, float* depth);

} // namespace holokit
