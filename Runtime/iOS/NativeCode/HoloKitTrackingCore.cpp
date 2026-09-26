// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT
//
// Line-by-line port of holokit-swift-sdk's HoloKitKalmanPredictor.swift and
// HoloKitPoseSmoother.swift. Keep the two in step; Tests/Tracking/Native~
// replays a device trace through this file and checks it against the Swift SDK.
#include "HoloKitTrackingCore.h"

#include <algorithm>
#include <cmath>

namespace holokit {
namespace {

const simd_float3x3 kIdentity = matrix_identity_float3x3;

simd_float3x3 diagonal(float value) { return simd_diagonal_matrix(simd_make_float3(value, value, value)); }

simd_float3x3 skew(simd_float3 v) {
    return simd_matrix_from_rows(simd_make_float3(0, -v.z, v.y),
                                 simd_make_float3(v.z, 0, -v.x),
                                 simd_make_float3(-v.y, v.x, 0));
}

simd_float3x3 add(simd_float3x3 a, simd_float3x3 b) { return simd_add(a, b); }
simd_float3x3 scale(float s, simd_float3x3 m) { return simd_mul(s, m); }
simd_float3x3 symmetrize(simd_float3x3 m) { return scale(0.5f, add(m, simd_transpose(m))); }

simd_quatf rotationBy(simd_quatf orientation, simd_float3 rate, float dt) {
    float angle = simd_length(rate) * dt;
    if (angle <= 1e-9f) return orientation;
    return simd_normalize(simd_mul(orientation, simd_quaternion(angle, simd_normalize(rate))));
}

simd_float3 translation(simd_float4x4 m) { return simd_make_float3(m.columns[3].x, m.columns[3].y, m.columns[3].z); }

simd_float4x4 transformOf(simd_quatf orientation, simd_float3 position) {
    simd_float4x4 m = simd_matrix4x4(orientation);
    m.columns[3] = simd_make_float4(position, 1);
    return m;
}

}  // namespace

// MARK: - Measurement

void KalmanPredictor::observe(double timestamp, simd_float4x4 cameraTransform,
                              const std::vector<GyroSample>& samples) {
    simd_quatf measured = simd_normalize(simd_quaternion(cameraTransform));
    simd_float3 position = translation(cameraTransform);
    if (!state_ || !(timestamp > state_->time) || !(timestamp - state_->time < 0.25)) {
        if (!state_ || timestamp > state_->time) state_.emplace(timestamp, measured, position);
        return;
    }
    State current = *state_;
    auto points = timeline(samples);
    double reached = integrate(current, timestamp, points, true);
    if (reached < timestamp - 0.02) {
        // The IMU stream does not cover this interval; restart from ARKit.
        State fresh(timestamp, measured, position);
        fresh.bias = current.bias;
        state_ = fresh;
        resets_ += 1;
        return;
    }
    if (reached < timestamp) {
        simd_float3 rate = points.empty() ? simd_float3{0, 0, 0} : points.back().rate - current.bias;
        step(current, rate, simd_float3{0, 0, 0}, float(timestamp - reached), true);
    }
    current.time = timestamp;
    float speed = points.empty() ? 0 : simd_length(points.back().rate - current.bias);
    correctRotation(current, measured, speed);
    if (lastInnovationDegrees_ > noise.resetInnovationDegrees) {
        state_.emplace(timestamp, measured, position);
        resets_ += 1;
        return;
    }
    correctPosition(current, position);
    state_ = current;
}

std::optional<Prediction> KalmanPredictor::predict(double target,
                                                   const std::vector<GyroSample>& samples) const {
    if (!state_) return std::nullopt;
    State current = *state_;
    double anchor = current.time;
    auto points = timeline(samples);
    double reached = integrate(current, target, points, false);
    auto lastAtOrBefore = [&](double time) -> const Point* {
        for (auto it = points.rbegin(); it != points.rend(); ++it)
            if (it->time <= time) return &*it;
        return nullptr;
    };
    double extrapolated = 0;
    const Point* last = lastAtOrBefore(reached);
    simd_float3 targetRate = last ? last->rate - current.bias : simd_float3{0, 0, 0};
    if (reached < target) {
        double span = std::min(target - reached,
                               points.empty() ? target - reached : parameters.maximumExtrapolation);
        simd_float3 rate = last ? last->rate - current.bias : simd_float3{0, 0, 0};
        simd_float3 acceleration = last ? last->acceleration : simd_float3{0, 0, 0};
        std::vector<Point> window;
        for (const auto& point : points)
            if (point.time <= reached && point.time > reached - noise.angularAccelerationWindow)
                window.push_back(point);
        simd_float3 spin = angularAcceleration(window) * noise.angularAccelerationWeight;
        // Constant angular acceleration (constant rate when the weight is 0),
        // integrated in short steps at the midpoint rate.
        int steps = std::max(1, int(std::ceil(span / 0.002)));
        double dt = span / double(steps);
        for (int index = 0; index < steps; index++) {
            float middle = float((double(index) + 0.5) * dt);
            step(current, rate + spin * middle, acceleration, float(dt), false);
        }
        extrapolated = span;
        targetRate = rate + spin * float(span);
    }
    Prediction result;
    result.transform = transformOf(current.orientation, current.position);
    result.anchorTimestamp = anchor;
    for (auto it = points.rbegin(); it != points.rend(); ++it) {
        if (it->time > anchor && it->time <= target) { result.newestIMUTimestamp = it->time; break; }
    }
    result.extrapolated = extrapolated;
    result.angularVelocity = targetRate;
    result.velocity = current.velocity;
    return result;
}

simd_float3 KalmanPredictor::angularAcceleration(const std::vector<Point>& points) {
    if (points.size() < 2) return simd_float3{0, 0, 0};
    double meanT = 0;
    simd_float3 meanRate = {0, 0, 0};
    for (const auto& point : points) { meanT += point.time; meanRate += point.rate; }
    meanT /= double(points.size());
    meanRate /= float(points.size());
    simd_float3 numerator = {0, 0, 0};
    float denominator = 0;
    for (const auto& point : points) {
        float dt = float(point.time - meanT);
        numerator += dt * (point.rate - meanRate);
        denominator += dt * dt;
    }
    return denominator > 1e-9f ? numerator / denominator : simd_float3{0, 0, 0};
}

// MARK: - Process model

std::vector<KalmanPredictor::Point> KalmanPredictor::timeline(const std::vector<GyroSample>& samples) const {
    simd_quatf toCamera = simd_length(parameters.imuToCamera) > 1e-6f
        ? simd_normalize(simd_quaternion(parameters.imuToCamera)) : simd_quaternion(0.f, 0.f, 0.f, 1.f);
    std::vector<Point> points;
    points.reserve(samples.size());
    for (const auto& sample : samples) {
        points.push_back({sample.timestamp + parameters.imuTimeOffset,
                          simd_act(toCamera, sample.cameraAngularVelocity),
                          simd_act(toCamera, sample.cameraAcceleration)});
    }
    std::stable_sort(points.begin(), points.end(),
                     [](const Point& a, const Point& b) { return a.time < b.time; });
    return points;
}

double KalmanPredictor::integrate(State& s, double end, const std::vector<Point>& points,
                                  bool withCovariance) const {
    const Point* previous = nullptr;
    for (const auto& point : points)
        if (point.time <= s.time) previous = &point;
    for (const auto& point : points) {
        if (!(point.time > s.time && point.time <= end)) continue;
        simd_float3 rate = previous ? (previous->rate + point.rate) / 2 : point.rate;
        simd_float3 acceleration = previous ? (previous->acceleration + point.acceleration) / 2
                                            : point.acceleration;
        step(s, rate - s.bias, acceleration, float(point.time - s.time), withCovariance);
        s.time = point.time;
        previous = &point;
    }
    return s.time;
}

void KalmanPredictor::step(State& s, simd_float3 rate, simd_float3 acceleration, float dt,
                           bool withCovariance) const {
    if (!(dt > 0)) return;
    simd_float3 worldAcceleration = simd_act(s.orientation, acceleration) * parameters.accelerationWeight;
    s.position += s.velocity * dt + 0.5f * worldAcceleration * dt * dt;
    s.velocity += worldAcceleration * dt;
    s.orientation = rotationBy(s.orientation, rate, dt);
    if (!withCovariance) return;
    // F = [[Φ, -dt·I], [0, I]], Φ ≈ I - [ω]×dt.
    simd_float3x3 phi = simd_sub(kIdentity, skew(rate * dt));
    float g = -dt;
    simd_float3x3 top = add(simd_mul(phi, s.a), scale(g, simd_transpose(s.b)));
    simd_float3x3 right = add(simd_mul(phi, s.b), scale(g, s.c));
    s.a = add(simd_mul(top, simd_transpose(phi)), scale(g, right));
    s.b = right;
    s.a = add(s.a, diagonal(noise.gyroNoise * noise.gyroNoise * dt));
    s.c = add(s.c, diagonal(noise.gyroBiasWalk * noise.gyroBiasWalk * dt));
    s.a = symmetrize(s.a);
    // Constant-velocity model with white acceleration noise.
    float pp = s.linear.x, pv = s.linear.y, vv = s.linear.z;
    float q = noise.accelerationNoise * noise.accelerationNoise;
    s.linear = simd_make_float3(pp + 2 * dt * pv + dt * dt * vv + q * dt * dt * dt / 3,
                                pv + dt * vv + q * dt * dt / 2,
                                vv + q * dt);
}

void KalmanPredictor::correctRotation(State& s, simd_quatf measured, float speed) {
    simd_quatf difference = simd_mul(simd_inverse(s.orientation), measured);
    if (simd_real(difference) < 0) difference = simd_quaternion(-difference.vector);
    simd_float3 imag = simd_imag(difference);
    float sine = simd_length(imag);
    simd_float3 residual = sine > 1e-7f
        ? imag / sine * (2 * std::atan2(sine, simd_real(difference))) : imag * 2;
    lastInnovationDegrees_ = simd_length(residual) * 180 / float(M_PI);
    float sigma = noise.arkitRotationNoise + noise.arkitRotationNoisePerRate * speed;
    simd_float3x3 inverse = simd_inverse(add(s.a, diagonal(sigma * sigma)));
    simd_float3x3 gainRotation = simd_mul(s.a, inverse);
    simd_float3x3 gainBias = simd_mul(simd_transpose(s.b), inverse);
    simd_float3 correction = simd_mul(gainRotation, residual);
    float angle = simd_length(correction);
    if (angle > 1e-9f)
        s.orientation = simd_normalize(simd_mul(s.orientation, simd_quaternion(angle, correction / angle)));
    s.bias += simd_mul(gainBias, residual);
    simd_float3x3 keep = simd_sub(kIdentity, gainRotation);
    simd_float3x3 c = simd_sub(s.c, simd_mul(gainBias, s.b));
    simd_float3x3 b = simd_mul(keep, s.b);
    simd_float3x3 a = simd_mul(keep, s.a);
    s.a = symmetrize(a);
    s.b = b;
    s.c = symmetrize(c);
}

void KalmanPredictor::correctPosition(State& s, simd_float3 measured) const {
    float pp = s.linear.x, pv = s.linear.y, vv = s.linear.z;
    float r = noise.arkitPositionNoise * noise.arkitPositionNoise;
    float innovation = pp + r;
    float kp = pp / innovation, kv = pv / innovation;
    simd_float3 residual = measured - s.position;
    s.position += kp * residual;
    s.velocity += kv * residual;
    s.linear = simd_make_float3((1 - kp) * pp, (1 - kp) * pv, vv - kv * pv);
}

// MARK: - Smoother

simd_float4x4 PoseSmoother::smooth(simd_float4x4 transform, simd_float3 angularVelocity,
                                   simd_float3 velocity, double time) {
    simd_quatf orientation = simd_normalize(simd_quaternion(transform));
    simd_float3 position = translation(transform);
    double positionConstant = positionTimeConstant.value_or(timeConstant);
    if ((timeConstant > 0 || positionConstant > 0) && hasLast_ && time > lastTime_ &&
        time - lastTime_ < 0.05) {
        float dt = float(time - lastTime_);
        simd_quatf carried = rotationBy(lastOrientation_, angularVelocity, dt);
        simd_float3 carriedPosition = lastPosition_ + velocity * dt;
        if (timeConstant > 0)
            orientation = simd_slerp(carried, orientation, float(1 - std::exp(-double(dt) / timeConstant)));
        if (positionConstant > 0) {
            float blend = float(1 - std::exp(-double(dt) / positionConstant));
            position = carriedPosition + (position - carriedPosition) * blend;
        }
    }
    hasLast_ = true;
    lastTime_ = time;
    lastOrientation_ = orientation;
    lastPosition_ = position;
    return transformOf(orientation, position);
}

// MARK: - Unity convention

simd_float4x4 arkitTransformFromUnity(simd_float3 position, simd_quatf rotation) {
    simd_float4 v = rotation.vector;
    simd_quatf arkit = simd_quaternion(simd_make_float4(-v.x, -v.y, v.z, v.w));
    return transformOf(simd_normalize(arkit), simd_make_float3(position.x, position.y, -position.z));
}

void unityPoseFromARKit(simd_float4x4 transform, simd_float3* position, simd_quatf* rotation) {
    simd_float3 p = translation(transform);
    simd_float4 v = simd_normalize(simd_quaternion(transform)).vector;
    *position = simd_make_float3(p.x, p.y, -p.z);
    *rotation = simd_quaternion(simd_make_float4(-v.x, -v.y, v.z, v.w));
}

}  // namespace holokit
