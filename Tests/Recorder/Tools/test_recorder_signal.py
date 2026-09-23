import unittest
import numpy as np
from recorder_signal import tone_onsets


class ToneOnsetTests(unittest.TestCase):
    def signal(self, amplitude=1, frequency=1200, starts=(.437, 1.737, 3.037)):
        rate = 16000
        times = np.arange(rate * 5) / rate
        pcm = np.random.default_rng(42).normal(0, amplitude * .08, len(times))
        for start in starts:
            mask = (times >= start) & (times < start + .150)
            pcm[mask] += amplitude * np.sin(2 * np.pi * frequency * times[mask])
        return pcm

    def test_gain_independent_onsets_preserve_leading_gap(self):
        for gain in (1, 1e-4, 1e-7):
            with self.subTest(gain=gain):
                onsets, _, _ = tone_onsets(self.signal(gain))
                np.testing.assert_allclose(onsets, [.437, 1.737, 3.037], atol=.015)

    def test_silence_noise_and_wrong_tone_do_not_trigger(self):
        for pcm in (np.zeros(80000), np.random.default_rng(7).normal(0, 1, 80000),
                    self.signal(frequency=440)):
            self.assertEqual(len(tone_onsets(pcm)[0]), 0)

    def test_initial_partial_pulse_has_no_onset(self):
        onsets, _, _ = tone_onsets(self.signal(starts=(-.05, 1.237)))
        np.testing.assert_allclose(onsets, [1.237], atol=.015)

    def test_short_input(self):
        self.assertEqual(len(tone_onsets(np.zeros(10))[0]), 0)


if __name__ == '__main__':
    unittest.main()
