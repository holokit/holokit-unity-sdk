// Host test for the exact queue/timeline header used by the iOS audio backend.
#include "HoloKitAudioBuffer.h"
#include <cassert>
#include <thread>
#include <vector>

int main() {
    holokit::AudioPacketQueue queue;
    float samples[2048];
    for (int i = 0; i < 2048; i++) samples[i] = float(i);
    assert(!queue.push(samples, 1025, 2, 48000, 0));
    assert(!queue.push(samples, 10, 3, 48000, 0));
    assert(!queue.push(samples, 10, 1, 0, 0));
    for (int cycle = 0; cycle < 10; cycle++) {
        for (int i = 0; i < 32; i++) assert(queue.push(samples, 1024, 2, 48000, i));
        assert(!queue.push(samples, 1024, 2, 48000, 32));
        for (int i = 0; i < 32; i++) {
            auto packet = queue.front();
            assert(packet && packet->hostTime == i && packet->samples[2047] == 2047);
            queue.pop();
        }
        assert(!queue.front());
    }
    constexpr int Count = 100000;
    std::thread producer([&] {
        for (int i = 0; i < Count; i++)
            while (!queue.push(samples, 256, 2, 48000, i)) std::this_thread::yield();
    });
    for (int i = 0; i < Count; i++) {
        const holokit::AudioPacket* packet;
        while (!(packet = queue.front())) std::this_thread::yield();
        assert(packet->hostTime == i && packet->samples[511] == 511);
        queue.pop();
    }
    producer.join();
    assert(!queue.front());
    holokit::AudioPacket packet;
    packet.hostTime = 1000.0;
    packet.frames = 1024; packet.sampleRate = 48000;
    auto range = holokit::framesInRecording(packet, 1000.005, 1000.015);
    assert(range.begin == 240 && range.end == 720);
    range = holokit::framesInRecording(packet, 999.0, 1001.0);
    assert(range.begin == 0 && range.end == 1024);
    range = holokit::framesInRecording(packet, 1001.0, 1002.0);
    assert(range.begin == 1024 && range.end == 1024);
    // A mic buffer arriving 20 ms later still starts 20 ms later on the shared timeline.
    packet.hostTime = 1000.020;
    range = holokit::framesInRecording(packet, 1000.0, 1000.030);
    assert(range.begin == 0 && range.end == 480);
}
