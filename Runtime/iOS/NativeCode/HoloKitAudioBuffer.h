// SPDX-License-Identifier: MIT
#pragma once
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstring>

namespace holokit {
// One producer per source (Unity DSP or native microphone), one writer consumer.
// Audio callbacks copy into preallocated storage; they never wait for an encoder.
struct AudioPacket {
    static constexpr uint32_t MaxFrames = 1024;
    double hostTime = 0;
    float gain = 1;
    uint32_t frames = 0, channels = 0, sampleRate = 0;
    float samples[MaxFrames * 2];
};

class AudioPacketQueue {
    static constexpr uint32_t Capacity = 32;
    std::array<AudioPacket, Capacity> packets;
    alignas(64) std::atomic<uint32_t> read {0};
    alignas(64) std::atomic<uint32_t> write {0};
public:
    bool push(const float* samples, uint32_t frames, uint32_t channels,
              uint32_t sampleRate, double hostTime, float gain = 1) {
        if (!samples || !frames || frames > AudioPacket::MaxFrames ||
            !channels || channels > 2 || !sampleRate || !std::isfinite(hostTime)) return false;
        uint32_t end = write.load(std::memory_order_relaxed);
        if (end - read.load(std::memory_order_acquire) == Capacity) return false;
        auto& packet = packets[end % Capacity];
        packet.hostTime = hostTime;
        packet.gain = gain;
        packet.frames = frames;
        packet.channels = channels;
        packet.sampleRate = sampleRate;
        std::memcpy(packet.samples, samples, frames * channels * sizeof(float));
        write.store(end + 1, std::memory_order_release);
        return true;
    }
    const AudioPacket* front() const {
        uint32_t begin = read.load(std::memory_order_relaxed);
        return begin == write.load(std::memory_order_acquire) ? nullptr : &packets[begin % Capacity];
    }
    void pop() { read.fetch_add(1, std::memory_order_release); }
    // Only call after producer and consumer have stopped (between recordings).
    void reset() { read.store(0); write.store(0); }
};

// Every source uses the same monotonic recording origin. Never reset a track's
// timestamp to its first callback: that erases real capture offsets and gaps.
struct AudioFrameRange { uint32_t begin, end; };
inline AudioFrameRange framesInRecording(const AudioPacket& packet, double start, double stop) {
    auto index = [&](double time) {
        double frames = std::ceil((time - packet.hostTime) * packet.sampleRate - 1e-7);
        return uint32_t(std::clamp(frames, 0.0, double(packet.frames)));
    };
    return { index(start), index(stop) };
}
}
