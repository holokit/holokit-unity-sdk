#if UNITY_EDITOR && UNITY_IOS
using HoloKit.iOS;
using UnityEditor;

namespace HoloKit.Editor
{
    [CustomEditor(typeof(HoloKitVideoRecorder))]
    sealed class HoloKitVideoRecorderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_recordIn4K"));
            var mode = serializedObject.FindProperty("_audioMode");
            EditorGUILayout.PropertyField(mode);
            bool game = mode.enumValueIndex == (int)RecordingAudioMode.GameAndMicrophone;
            bool microphone = true;
            if (game)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_gameAudioListener"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_gameGain"));
                var include = serializedObject.FindProperty("_includeMicrophone");
                EditorGUILayout.PropertyField(include);
                microphone = include.boolValue;
            }
            if (!game)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_environmentProcessing"),
                    new UnityEngine.GUIContent("Microphone Processing"));
            if (microphone)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_microphoneInput"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_microphoneGain"));
            }
            if (game && microphone)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_createMixedSharingCopy"));
            EditorGUILayout.HelpBox(game
                ? microphone
                    ? "Records separate game and echo-cancelled microphone tracks. Game playback is unchanged."
                    : "Records game audio only. Recording does not request microphone permission."
                : serializedObject.FindProperty("_environmentProcessing").enumValueIndex == (int)MicrophoneProcessing.Default
                    ? "Records environmental sound with iOS automatic gain (louder quiet sounds, like Unity's Microphone). Share this input with application analysis."
                    : "Records raw environmental sound without microphone playback. Share this input with application analysis.",
                MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            var recorder = (HoloKitVideoRecorder)target;
            if (!string.IsNullOrEmpty(recorder.LastError))
                EditorGUILayout.HelpBox(recorder.LastError, MessageType.Error);
        }
    }
}
#endif
