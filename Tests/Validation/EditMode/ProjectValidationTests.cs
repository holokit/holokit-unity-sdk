// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System.Linq;
using HoloKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;

namespace HoloKit.Validation.Tests
{
    /// Each rule must detect a bad setting and its Fix must repair it. Settings are
    /// restored afterwards.
    public class ProjectValidationTests
    {
        SerializedObject m_Settings;
        bool m_Adjust;
        int m_Serious;
        string m_Microphone;
        string m_TargetIOS;
        bool m_ForceSpeakers;
        bool m_AndroidDefaultAPIs;
        GraphicsDeviceType[] m_AndroidAPIs;

        [SetUp]
        public void Save()
        {
            m_Settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            m_Adjust = m_Settings.FindProperty("adjustIOSFPSUsingThermalState").boolValue;
            m_Serious = m_Settings.FindProperty("thermalStateSeriousIOSFPS").intValue;
            m_Microphone = PlayerSettings.iOS.microphoneUsageDescription;
            m_TargetIOS = PlayerSettings.iOS.targetOSVersionString;
            m_ForceSpeakers = m_Settings.FindProperty("Force IOS Speakers When Recording").boolValue;
            m_AndroidDefaultAPIs = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            m_AndroidAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        }

        [TearDown]
        public void Restore()
        {
            m_Settings.Update();
            m_Settings.FindProperty("adjustIOSFPSUsingThermalState").boolValue = m_Adjust;
            m_Settings.FindProperty("thermalStateSeriousIOSFPS").intValue = m_Serious;
            m_Settings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.iOS.microphoneUsageDescription = m_Microphone;
            PlayerSettings.iOS.targetOSVersionString = m_TargetIOS;
            m_Settings.Update();
            m_Settings.FindProperty("Force IOS Speakers When Recording").boolValue = m_ForceSpeakers;
            m_Settings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, m_AndroidDefaultAPIs);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, m_AndroidAPIs);
        }

        void SetThermal(bool adjust, int seriousFPS)
        {
            m_Settings.Update();
            m_Settings.FindProperty("adjustIOSFPSUsingThermalState").boolValue = adjust;
            m_Settings.FindProperty("thermalStateSeriousIOSFPS").intValue = seriousFPS;
            m_Settings.ApplyModifiedPropertiesWithoutUndo();
        }

        static Unity.XR.CoreUtils.Editor.BuildValidationRule Rule(BuildTargetGroup group, string text) =>
            HoloKitProjectValidation.RulesFor(group).Single(r => r.Message.Contains(text));

        [Test]
        public void ThermalFrameRateCap_IsDetectedAndFixed()
        {
            var rule = Rule(BuildTargetGroup.iOS, "thermal state");
            Assert.IsTrue(rule.Error, "a 30 fps cap must stop the build");
            SetThermal(true, 30);
            Assert.IsFalse(rule.CheckPredicate(), "30 fps cap must fail");
            SetThermal(true, 60);
            Assert.IsTrue(rule.CheckPredicate(), "a 60 fps floor is acceptable");
            SetThermal(true, 30);
            rule.FixIt();
            Assert.IsTrue(rule.CheckPredicate());
        }

        [Test]
        public void MinimumIOSVersion_IsRequiredAndFixed()
        {
            var rule = Rule(BuildTargetGroup.iOS, "Target minimum iOS Version");
            Assert.IsTrue(rule.Error, "an older iOS target must stop the build");
            PlayerSettings.iOS.targetOSVersionString = "18.0";
            Assert.IsFalse(rule.CheckPredicate(), "iOS 18 is below the minimum");
            PlayerSettings.iOS.targetOSVersionString = "26.4";
            Assert.IsFalse(rule.CheckPredicate(), "iOS 26.4 is below the minimum");
            PlayerSettings.iOS.targetOSVersionString = "27";
            Assert.IsTrue(rule.CheckPredicate(), "\"27\" is 27.0");
            PlayerSettings.iOS.targetOSVersionString = "27.1";
            Assert.IsTrue(rule.CheckPredicate());
            PlayerSettings.iOS.targetOSVersionString = "18.0";
            rule.FixIt();
            Assert.AreEqual(HoloKitProjectValidation.MinimumIOSVersion, PlayerSettings.iOS.targetOSVersionString);
            Assert.IsTrue(rule.CheckPredicate());
        }

        [Test]
        public void MissingMicrophoneDescription_IsDetectedAndFixed()
        {
            var rule = Rule(BuildTargetGroup.iOS, "Microphone Usage Description");
            PlayerSettings.iOS.microphoneUsageDescription = "";
            Assert.IsFalse(rule.CheckPredicate());
            rule.FixIt();
            Assert.IsTrue(rule.CheckPredicate());
        }

        [Test]
        public void SpeakerRouteWhenRecording_IsDetectedAndFixed()
        {
            var rule = Rule(BuildTargetGroup.iOS, "Force iOS Speakers");
            m_Settings.Update();
            m_Settings.FindProperty("Force IOS Speakers When Recording").boolValue = false;
            m_Settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(rule.CheckPredicate());
            rule.FixIt();
            Assert.IsTrue(rule.CheckPredicate());
        }

        [Test]
        public void AndroidGraphicsAPIs_AreDetectedAndFixed()
        {
            var rule = Rule(BuildTargetGroup.Android, "OpenGLES3");
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);
            Assert.IsFalse(rule.CheckPredicate());
            rule.FixIt();
            Assert.IsTrue(rule.CheckPredicate());
        }
    }
}
