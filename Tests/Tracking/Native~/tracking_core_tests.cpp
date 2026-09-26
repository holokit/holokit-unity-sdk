// Host tests for Runtime/iOS/NativeCode/HoloKitTrackingCore (the iOS tracking
// core). Build and run on a Mac:  Tests/Tracking/Tools~/run_native_tests.sh
//
// 1. Synthetic motion with known truth (ports of holokit-swift-sdk's
//    KalmanPredictorTests).
// 2. Smoother and Unity <-> ARKit conversion.
// 3. Parity: replays an iPhone 15 Pro trace exactly like the Swift SDK's
//    HoloKitPredictionBenchmark and compares with fixtures/swift_expected.json.
#include "HoloKitTrackingCore.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <map>
#include <sstream>
#include <string>
#include <vector>

using namespace holokit;

static int failures = 0;
#define CHECK(condition, ...) do { if (!(condition)) { failures++; std::printf("FAIL %s:%d: ", __FILE__, __LINE__); \
    std::printf(__VA_ARGS__); std::printf("\n"); } } while (0)

// MARK: - Helpers

static double angleDegrees(simd_quatf a, simd_quatf b) {
    simd_quatf relative = simd_mul(simd_inverse(simd_normalize(a)), simd_normalize(b));
    double sine = simd_length(simd_imag(relative));
    return 2 * std::atan2(sine, std::fabs(double(simd_real(relative)))) * 180 / M_PI;
}
static double angleDegrees(simd_float4x4 a, simd_float4x4 b) {
    return angleDegrees(simd_quaternion(a), simd_quaternion(b));
}
static simd_float3 translation(simd_float4x4 m) { return simd_make_float3(m.columns[3].x, m.columns[3].y, m.columns[3].z); }

// Constant angular velocity; constant world acceleration from rest.
static const simd_float3 kRate = {0, 2, 0.5f};
static const simd_float3 kAcceleration = {1.5f, 0, -1};

static simd_float4x4 truth(double t) {
    simd_float4x4 m = simd_matrix4x4(simd_quaternion(simd_length(kRate) * float(t), simd_normalize(kRate)));
    m.columns[3] = simd_make_float4(0.5f * kAcceleration * float(t * t), 1);
    return m;
}

static std::vector<GyroSample> imu(double start, double end, simd_float3 bias = {0, 0, 0}) {
    std::vector<GyroSample> samples;
    for (double t = start + 0.005; t <= end + 1e-9; t += 0.005) {
        GyroSample sample;
        sample.timestamp = t;
        sample.cameraAngularVelocity = kRate + bias;
        sample.cameraAcceleration = simd_act(simd_inverse(simd_quaternion(truth(t))), kAcceleration);
        samples.push_back(sample);
    }
    return samples;
}

static void run(KalmanPredictor& filter, double end, simd_float3 bias = {0, 0, 0}) {
    for (double t = 0; t <= end + 1e-9; t += 1.0 / 60) filter.observe(t, truth(t), imu(std::max(0.0, t - 0.05), t, bias));
}

// MARK: - Kalman (ports of KalmanPredictorTests)

static void testBridgesNinetyMilliseconds() {
    KalmanPredictor filter;
    run(filter, 1);
    double anchor = *filter.anchorTimestamp(), target = anchor + 0.090;
    auto prediction = filter.predict(target, imu(anchor - 0.05, target));
    CHECK(prediction, "no prediction");
    CHECK(angleDegrees(prediction->transform, truth(target)) < 0.05, "rotation %.4f",
          angleDegrees(prediction->transform, truth(target)));
    CHECK(simd_distance(translation(prediction->transform), translation(truth(target))) < 0.002, "position");
    CHECK(simd_distance(translation(truth(anchor)), translation(truth(target))) > 0.1, "holding would be far off");
}

static void testLearnsGyroBias() {
    const simd_float3 bias = {0.02f, -0.015f, 0.01f};
    KalmanPredictor filter;
    run(filter, 30, bias);
    CHECK(simd_distance(filter.gyroBias(), bias) < 0.005, "bias (%f %f %f)", filter.gyroBias().x,
          filter.gyroBias().y, filter.gyroBias().z);
    double anchor = *filter.anchorTimestamp(), target = anchor + 0.090;
    auto prediction = filter.predict(target, imu(anchor - 0.05, target, bias));
    CHECK(prediction && angleDegrees(prediction->transform, truth(target)) < 0.05, "biased prediction");
}

static void testExtrapolatesPastNewestSample() {
    KalmanPredictor filter;
    run(filter, 1);
    double anchor = *filter.anchorTimestamp();
    auto prediction = filter.predict(anchor + 0.08, imu(anchor - 0.05, anchor + 0.06));
    CHECK(prediction && std::fabs(prediction->extrapolated - 0.02) < 0.003, "extrapolated %f",
          prediction ? prediction->extrapolated : -1);
    CHECK(prediction && angleDegrees(prediction->transform, truth(anchor + 0.08)) < 0.05, "extrapolated pose");
}

static void testRelocalizationResets() {
    KalmanPredictor filter;
    run(filter, 1);
    double t = *filter.anchorTimestamp() + 1.0 / 60;
    simd_float4x4 jumped = simd_mul(simd_matrix4x4(simd_quaternion(float(M_PI / 4), simd_make_float3(0, 0, 1))), truth(t));
    filter.observe(t, jumped, imu(t - 0.05, t));
    CHECK(filter.resets() == 1, "resets %d", filter.resets());
    auto prediction = filter.predict(t, {});
    CHECK(prediction && angleDegrees(prediction->transform, jumped) < 0.01, "reset pose");
}

static void testMissingIMUFallsBackToARKit() {
    KalmanPredictor filter;
    filter.observe(0, truth(0), {});
    filter.observe(0.1, truth(0.1), {});
    auto prediction = filter.predict(0.1, {});
    CHECK(prediction && angleDegrees(prediction->transform, truth(0.1)) < 0.01, "fallback pose");
}

// MARK: - Smoother and conversion

static void testSmootherPassesSteadyRotationAndDampsJumps() {
    PoseSmoother smoother;
    smoother.timeConstant = 0.01;
    smoother.positionTimeConstant = 0.03;
    const simd_float3 rate = {0, 1, 0};
    double worst = 0;
    for (int i = 0; i < 120; i++) {
        double t = i / 120.0;
        simd_float4x4 pose = simd_matrix4x4(simd_quaternion(float(t), simd_make_float3(0, 1, 0)));
        worst = std::max(worst, angleDegrees(smoother.smooth(pose, rate, {0, 0, 0}, t), pose));
    }
    CHECK(worst < 1e-3, "steady turn changed by %f deg", worst);
    // A 1° jump is only partly followed on the next 8.3 ms frame.
    simd_float4x4 jumped = simd_matrix4x4(simd_quaternion(float(1.0 + M_PI / 180), simd_make_float3(0, 1, 0)));
    simd_float4x4 out = smoother.smooth(jumped, rate, {0, 0, 0}, 1.0);
    double toJump = angleDegrees(out, jumped);
    CHECK(toJump > 0.3 && toJump < 0.9, "damped jump residual %f", toJump);
}

static void testUnityConversionRoundTripsAndFlipsHandedness() {
    simd_float3 unityPosition = {0.1f, 0.2f, 0.3f};
    simd_quatf unityRotation = simd_quaternion(0.7f, simd_normalize(simd_make_float3(0.2f, 1, 0.1f)));
    simd_float4x4 arkit = arkitTransformFromUnity(unityPosition, unityRotation);
    CHECK(std::fabs(arkit.columns[3].z + 0.3f) < 1e-6, "z flips");
    // Unity's forward (+z) is ARKit's camera forward (-z).
    simd_float3 unityForward = simd_act(unityRotation, simd_make_float3(0, 0, 1));
    simd_float3 arkitForward = simd_act(simd_quaternion(arkit), simd_make_float3(0, 0, -1));
    CHECK(simd_distance(arkitForward, simd_make_float3(unityForward.x, unityForward.y, -unityForward.z)) < 1e-5,
          "forward maps");
    simd_float3 position; simd_quatf rotation;
    unityPoseFromARKit(arkit, &position, &rotation);
    CHECK(simd_distance(position, unityPosition) < 1e-6 && angleDegrees(rotation, unityRotation) < 1e-3, "round trip");
}

// MARK: - Parity with the Swift SDK on a device trace

struct Frame { double t, received; simd_quatf q; simd_float3 p; bool normal; };
struct Render { double start, presented; };

static std::vector<std::vector<double>> readCSV(const std::string& path) {
    std::ifstream file(path);
    std::vector<std::vector<double>> rows;
    std::string line;
    std::getline(file, line);   // header
    while (std::getline(file, line)) {
        std::vector<double> row;
        std::stringstream stream(line);
        std::string cell;
        while (std::getline(stream, cell, ',')) row.push_back(std::strtod(cell.c_str(), nullptr));
        rows.push_back(row);
    }
    return rows;
}

static double percentile(std::vector<double> values, double fraction) {
    std::sort(values.begin(), values.end());
    double position = double(values.size() - 1) * fraction;
    size_t lower = size_t(std::floor(position)), upper = size_t(std::ceil(position));
    return values[lower] + (values[upper] - values[lower]) * (position - double(lower));
}

// Top-level object: the last occurrence, after "bands" reuses the same names.
static double jsonNumber(const std::string& json, const std::string& object, const std::string& key) {
    size_t start = json.rfind("\"" + object + "\"");
    size_t at = json.find("\"" + key + "\"", start);
    return std::strtod(json.c_str() + json.find(':', at) + 1, nullptr);
}

static void testMatchesSwiftSDKOnDeviceTrace(const std::string& fixtures) {
    std::vector<Frame> frames;
    for (auto& r : readCSV(fixtures + "/ar_frames.csv"))
        frames.push_back({r[0], r[1], simd_quaternion(float(r[2]), float(r[3]), float(r[4]), float(r[5])),
                          simd_make_float3(float(r[6]), float(r[7]), float(r[8])), r[9] > 0.5});
    std::vector<std::pair<double, GyroSample>> imuSamples;   // (received, sample)
    for (auto& r : readCSV(fixtures + "/imu.csv")) {
        GyroSample s; s.timestamp = r[0];
        s.cameraAngularVelocity = simd_make_float3(float(r[2]), float(r[3]), float(r[4]));
        s.cameraAcceleration = simd_make_float3(float(r[5]), float(r[6]), float(r[7]));
        imuSamples.push_back({r[1], s});
    }
    std::vector<Render> renders;
    for (auto& r : readCSV(fixtures + "/renders.csv")) renders.push_back({r[0], r[1]});
    CHECK(frames.size() > 300 && imuSamples.size() > 500 && renders.size() > 600, "fixture size");

    std::vector<Frame> normal, arrivals;
    for (auto& f : frames) if (f.normal) normal.push_back(f);
    arrivals = normal;
    std::sort(normal.begin(), normal.end(), [](auto& a, auto& b) { return a.t < b.t; });
    std::sort(arrivals.begin(), arrivals.end(), [](auto& a, auto& b) { return a.received < b.received; });
    std::sort(imuSamples.begin(), imuSamples.end(), [](auto& a, auto& b) { return a.first < b.first; });
    std::vector<double> gaps;
    for (size_t i = 1; i < normal.size(); i++) gaps.push_back(normal[i].t - normal[i - 1].t);
    std::sort(gaps.begin(), gaps.end());
    double maxSpan = std::max(0.05, 1.3 * gaps[gaps.size() / 2]);

    TrackingParameters parameters;
    KalmanPredictor filter(parameters, KalmanParameters());
    PoseSmoother smoother;
    smoother.timeConstant = parameters.outputSmoothing;
    smoother.positionTimeConstant = parameters.positionSmoothing;
    std::vector<GyroSample> delivered;
    size_t nextFrame = 0, nextSample = 0;
    const Frame* latest = nullptr;
    std::vector<double> rotation, position;
    for (auto& render : renders) {
        double now = render.start;
        while (nextSample < imuSamples.size() && imuSamples[nextSample].first <= now)
            delivered.push_back(imuSamples[nextSample++].second);
        if (delivered.size() > 600) delivered.erase(delivered.begin(), delivered.end() - 600);
        while (nextFrame < arrivals.size() && arrivals[nextFrame].received <= now) {
            latest = &arrivals[nextFrame++];
            simd_float4x4 m = simd_matrix4x4(latest->q);
            m.columns[3] = simd_make_float4(latest->p, 1);
            filter.observe(latest->t, m, delivered);
        }
        double target = render.presented > 0 ? render.presented + parameters.scanoutDelay
                                             : now + parameters.displayLatency;
        auto after = std::lower_bound(normal.begin(), normal.end(), target,
                                      [](const Frame& f, double t) { return f.t < t; });
        if (!latest || after == normal.begin() || after == normal.end()) continue;
        const Frame& a = *(after - 1); const Frame& b = *after;
        double span = b.t - a.t;
        if (!(span > 0 && span <= maxSpan)) continue;
        float fraction = float((target - a.t) / span);
        simd_quatf truthQ = simd_slerp(a.q, b.q, fraction);
        simd_float3 truthP = a.p + (b.p - a.p) * fraction;
        double speed = angleDegrees(a.q, b.q) / span;
        simd_float4x4 predicted = simd_matrix4x4(latest->q);
        predicted.columns[3] = simd_make_float4(latest->p, 1);
        if (auto p = filter.predict(target, delivered))
            predicted = smoother.smooth(p->transform, p->angularVelocity, p->velocity, target);
        if (speed < 20) continue;
        rotation.push_back(angleDegrees(simd_quaternion(predicted), truthQ));
        position.push_back(double(simd_distance(translation(predicted), truthP)) * 1000);
    }
    std::ifstream file(fixtures + "/swift_expected.json");
    std::string json((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    struct { const char* name; double cpp, swift, tolerance; } checks[] = {
        {"moving frames", double(rotation.size()), jsonNumber(json, "predicted", "count"), 0.5},
        {"rotation p50 deg", percentile(rotation, 0.5), jsonNumber(json, "predicted", "rotationP50Degrees"), 0.005},
        {"rotation p95 deg", percentile(rotation, 0.95), jsonNumber(json, "predicted", "rotationP95Degrees"), 0.01},
        {"position p50 mm", percentile(position, 0.5), jsonNumber(json, "predicted", "positionP50Millimeters"), 0.05},
        {"position p95 mm", percentile(position, 0.95), jsonNumber(json, "predicted", "positionP95Millimeters"), 0.1},
    };
    for (auto& check : checks) {
        std::printf("  parity %-17s C++ %9.4f  Swift %9.4f\n", check.name, check.cpp, check.swift);
        CHECK(std::fabs(check.cpp - check.swift) <= check.tolerance, "%s differs", check.name);
    }
}

int main(int argc, char** argv) {
    std::string fixtures = argc > 1 ? argv[1] : "fixtures";
    testBridgesNinetyMilliseconds();
    testLearnsGyroBias();
    testExtrapolatesPastNewestSample();
    testRelocalizationResets();
    testMissingIMUFallsBackToARKit();
    testSmootherPassesSteadyRotationAndDampsJumps();
    testUnityConversionRoundTripsAndFlipsHandedness();
    testMatchesSwiftSDKOnDeviceTrace(fixtures);
    std::printf(failures ? "%d FAILED\n" : "All tracking core tests passed\n", failures);
    return failures ? 1 : 0;
}
