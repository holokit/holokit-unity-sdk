// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace HoloKit.UI.Tests
{
    /// CameraOffset places the eyes in Stereo: an error in it moves every virtual object by the same amount.
    public class PhoneModelListTests
    {
        const string IOSPhoneModelListGuid = "97e080f80a8ae49d78e255ae53b17e11";

        /// A Pro iPhone's AR camera sits in the top corner, a few millimetres below the display's top edge
        /// (14 Pro 7.9 mm, 15 Pro Max 11.4 mm). iPhone 15 Pro had it 32.6 mm down, mid-phone, which drew
        /// everything 2.3 cm off in Stereo; 16 Pro to 18 Pro had copied that value.
        [Test]
        public void ProCameraSitsJustBelowTheDisplayTop()
        {
            var list = AssetDatabase.LoadAssetAtPath<PhoneModelList>(AssetDatabase.GUIDToAssetPath(IOSPhoneModelListGuid));
            var pros = list.PhoneModels.Where(p => p.Description.Contains(" Pro") && p.ModelSpecs.ScreenResolution.y > 0).ToList();
            Assert.Greater(pros.Count, 5);
            foreach (var phone in pros)
            {
                var specs = phone.ModelSpecs;
                float displayHeight = specs.ScreenResolution.y / specs.ScreenDpi * 25.4f;
                float below = displayHeight + specs.CameraOffset.y * 1000f;
                Assert.That(below, Is.InRange(0f, 15f), $"{phone.ModelName} ({phone.Description}): camera {below:F1} mm below the display top");
            }
        }
    }
}
