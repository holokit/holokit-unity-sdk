"""Tone onsets on the original PCM timeline, including quiet raw microphones."""
import numpy as np


def tone_onsets(pcm, rate=16000, frequency=1200):
    # 20 ms coherent windows, sampled every 5 ms. Neighboring frequency bins
    # estimate noise without imposing a microphone/device-specific gain floor.
    width, hop = round(rate * .020), round(rate * .005)
    if len(pcm) < width:
        return np.array([]), np.array([]), np.array([])
    blocks = np.lib.stride_tricks.sliding_window_view(pcm, width)[::hop]
    frequencies = frequency + np.array([-300, -200, -100, 0, 100, 200, 300])
    carriers = np.exp(-2j * np.pi * np.outer(np.arange(width), frequencies) / rate)
    amplitudes = abs(blocks @ carriers) / width
    tone = amplitudes[:, 3]
    noise = np.median(amplitudes[:, [0, 1, 2, 4, 5, 6]], axis=1)
    mask = (tone > max(1e-9, float(np.percentile(tone, 99)) * .3)) & (tone > noise * 4)
    changes = np.diff(np.r_[False, mask, False].astype(int))
    starts, ends = np.flatnonzero(changes == 1), np.flatnonzero(changes == -1)
    # Reject isolated noise peaks. A pulse already high in the first window
    # has no observable onset. Center timestamps have at most 10 ms window blur.
    starts = starts[(starts > 0) & ((ends - starts) * hop / rate >= .040)]
    times = (np.arange(len(tone)) * hop + width / 2) / rate
    return times[starts], times, tone
