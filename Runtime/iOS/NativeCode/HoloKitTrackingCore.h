// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT
//
// HoloKit's stereo pose prediction, ported line by line from holokit-swift-sdk
// (HoloKitKalmanPredictor.swift, HoloKitPoseSmoother.swift,
// HoloKitTrackingParameters.swift). Pure C++ on <simd/simd.h>: no ARKit, no
// Unity, so Tests/Tracking/Native~ builds and runs it on the Mac.
//
// All poses are ARKit camera poses (right-handed, the camera looks down -z) on
// ARKit's clock (CACurrentMediaTime seconds). Gyro and acceleration are in
// ARKit camera axes.
#pragma once

#include <simd/simd.h>
#include <optional>
#include <vector>

namespace holokit {

/// Timing and IMU alignment (HoloKitTrackingParameters).
struct TrackingParameters {
    /// Added to every IMU timestamp to align it with ARKit's capture clock.
    double imuTimeOffset = 0;
    /// Prediction lead before the display's presentation time is measured.
    double displayLatency = 0.021;
    /// Time from presentedTime (scan-out start) until the eyes see the frame.
    double scanoutDelay = 0.004;
    /// Longest extrapolation past the newest IMU sample.
    double maximumExtrapolation = 0.05;
    /// Weight of accelerometer integration for position (0 = constant velocity).
    float accelerationWeight = 1;
    /// Small rotation from IMU axes to ARKit camera axes (x, y, z, w).
    simd_float4 imuToCamera = {0, 0, 0, 1};
    /// Pose smoother time constants; 0 disables.
    double outputSmoothing = 0.01;
    double positionSmoothing = 0.03;
};

/// Noise model (HoloKitKalmanParameters). Defaults fit on iPhone 15 Pro traces.
struct KalmanParameters {
    float gyroNoise = 0.02f;               // rad/s/√Hz
    float gyroBiasWalk = 0.0005f;          // rad/s²/√Hz
    float arkitRotationNoise = 0.002f;     // rad
    float arkitRotationNoisePerRate = 0;   // rad per rad/s
    float accelerationNoise = 2;           // m/s²·√s
    float arkitPositionNoise = 0.005f;     // m
    float resetInnovationDegrees = 10;
    /// Share of angular acceleration used past the newest IMU sample.
    float angularAccelerationWeight = 0.75f;
    double angularAccelerationWindow = 0.015;
};

/// One Core Motion sample in ARKit camera axes (HoloKitGyroSample).
struct GyroSample {
    double timestamp = 0;
    simd_float3 cameraAngularVelocity = {0, 0, 0};   // rad/s
    simd_float3 cameraAcceleration = {0, 0, 0};      // m/s², gravity removed
};

struct Prediction {
    simd_float4x4 transform;
    double anchorTimestamp = 0;
    std::optional<double> newestIMUTimestamp;
    double extrapolated = 0;
    simd_float3 angularVelocity = {0, 0, 0};   // camera axes, bias-corrected
    simd_float3 velocity = {0, 0, 0};          // world
};

/// Cardboard-style Kalman filter with ARKit as the absolute measurement
/// (HoloKitKalmanPredictor): error-state EKF over orientation and gyro bias,
/// linear Kalman filter over position and velocity; corrected at each ARKit
/// frame's capture time, then integrated and extrapolated to the target.
class KalmanPredictor {
public:
    TrackingParameters parameters;
    KalmanParameters noise;

    KalmanPredictor() = default;
    KalmanPredictor(TrackingParameters parameters, KalmanParameters noise)
        : parameters(parameters), noise(noise) {}

    void reset() { state_.reset(); }
    void observe(double timestamp, simd_float4x4 cameraTransform,
                 const std::vector<GyroSample>& samples);
    std::optional<Prediction> predict(double target, const std::vector<GyroSample>& samples) const;

    std::optional<double> anchorTimestamp() const {
        return state_ ? std::optional<double>(state_->time) : std::nullopt;
    }
    simd_float3 gyroBias() const { return state_ ? state_->bias : simd_float3{0, 0, 0}; }
    float lastInnovationDegrees() const { return lastInnovationDegrees_; }
    int resets() const { return resets_; }

    struct Point { double time; simd_float3 rate; simd_float3 acceleration; };
    static simd_float3 angularAcceleration(const std::vector<Point>& points);

private:
    struct State {
        double time;
        simd_quatf orientation;
        simd_float3 bias = {0, 0, 0};
        simd_float3 position;
        simd_float3 velocity = {0, 0, 0};
        simd_float3x3 a = simd_diagonal_matrix(simd_make_float3(1e-4f, 1e-4f, 1e-4f));
        simd_float3x3 b = simd_diagonal_matrix(simd_make_float3(0, 0, 0));
        simd_float3x3 c = simd_diagonal_matrix(simd_make_float3(1e-4f, 1e-4f, 1e-4f));
        simd_float3 linear = {1e-4f, 0, 1};   // (pp, pv, vv)
        State(double time, simd_quatf orientation, simd_float3 position)
            : time(time), orientation(orientation), position(position) {}
    };

    std::vector<Point> timeline(const std::vector<GyroSample>& samples) const;
    double integrate(State& s, double end, const std::vector<Point>& points, bool withCovariance) const;
    void step(State& s, simd_float3 rate, simd_float3 acceleration, float dt, bool withCovariance) const;
    void correctRotation(State& s, simd_quatf measured, float speed);
    void correctPosition(State& s, simd_float3 measured) const;

    std::optional<State> state_;
    float lastInnovationDegrees_ = 0;
    int resets_ = 0;
};

/// Carries the last pose forward with the predicted velocities and low-passes
/// only frame-to-frame jumps (HoloKitPoseSmoother).
class PoseSmoother {
public:
    double timeConstant = 0;
    std::optional<double> positionTimeConstant;

    void reset() { hasLast_ = false; }
    simd_float4x4 smooth(simd_float4x4 transform, simd_float3 angularVelocity,
                         simd_float3 velocity, double time);

private:
    bool hasLast_ = false;
    double lastTime_ = 0;
    simd_quatf lastOrientation_ = simd_quaternion(0.f, 0.f, 0.f, 1.f);
    simd_float3 lastPosition_ = {0, 0, 0};
};

/// Unity (left-handed, z forward) <-> ARKit camera pose (right-handed, -z forward).
/// Valid in HoloKit's stereo mode, where Unity's LandscapeLeft is ARKit's native
/// landscape-right camera orientation, so only handedness differs.
simd_float4x4 arkitTransformFromUnity(simd_float3 position, simd_quatf rotation);
void unityPoseFromARKit(simd_float4x4 transform, simd_float3* position, simd_quatf* rotation);

/// Core Motion device axes -> ARKit camera axes (x = -deviceY, y = deviceX, z = deviceZ).
inline simd_float3 cameraAxes(simd_float3 device) {
    return simd_make_float3(-device.y, device.x, device.z);
}

}  // namespace holokit
