// SPDX-FileCopyrightText: Copyright 2023-2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS
using HoloKit.iOS;
using UnityEngine;
using UnityEngine.UI;

// Demonstrates the two recorder audio presets.
// Environment: the recorder and this analysis share the raw microphone (no voice processing).
// Game And Microphone: a game tone is recorded on its own track, the microphone uses echo cancellation.
public class RecordingAudioDemo : MonoBehaviour
{
    [SerializeField] HoloKitMicrophoneCapture m_Microphone;
    [Tooltip("Must match the recorder's processing: Environment for raw input, EchoCancellation for Game And Microphone.")]
    [SerializeField] MicrophoneProcessing m_Processing = MicrophoneProcessing.Environment;
    [Tooltip("Plays a short 660 Hz beep every second through the game mix.")]
    [SerializeField] bool m_PlayGameTone;
    [SerializeField] Text m_LevelText;

    readonly float[] m_Samples = new float[2048];
    AudioSource m_Source;

    void OnEnable()
    {
        if (m_Microphone != null) m_Microphone.Acquire(this, m_Processing);
        if (m_PlayGameTone) PlayGameTone();
    }

    void OnDisable()
    {
        if (m_Microphone != null) m_Microphone.Release(this);
        if (m_Source != null) m_Source.Stop();
    }

    void PlayGameTone()
    {
        const int rate = 48000;
        var data = new float[rate];
        for (int i = 0; i < rate / 10; i++)
            data[i] = 0.3f * Mathf.Sin(2 * Mathf.PI * 660 * i / rate);
        var clip = AudioClip.Create("Game Beep", rate, 1, rate, false);
        clip.SetData(data, 0);
        m_Source = gameObject.GetComponent<AudioSource>();
        if (m_Source == null) m_Source = gameObject.AddComponent<AudioSource>();
        m_Source.clip = clip;
        m_Source.loop = true;
        m_Source.Play();
    }

    void Update()
    {
        if (m_LevelText == null || m_Microphone == null) return;
        if (!m_Microphone.IsReady)
        {
            m_LevelText.text = m_Microphone.LastError ?? "Microphone starting…";
            return;
        }
        int count = m_Microphone.ReadLatestSamples(m_Samples);
        float sum = 0;
        for (int i = 0; i < count; i++) sum += m_Samples[i] * m_Samples[i];
        float rms = count > 0 ? Mathf.Sqrt(sum / count) : 0;
        m_LevelText.text = $"{m_Processing} mic level {20 * Mathf.Log10(Mathf.Max(rms, 1e-6f)):F0} dBFS";
    }
}
#endif
