#if UNITY_IOS
using HoloKit.iOS;
using NUnit.Framework;
using UnityEngine;

public class ARKitCameraCompatibilityTests
{
    // Column-major ARKit projection for a portrait 1206x2622 viewport of a 1920x1440 image
    // (68.15 deg vertical, 34.66 deg horizontal field of view), zNear 0.1, zFar 20.
    static float[] PortraitProjection()
    {
        float sx = 1 / Mathf.Tan(34.66f * 0.5f * Mathf.Deg2Rad), sy = 1 / Mathf.Tan(68.15f * 0.5f * Mathf.Deg2Rad);
        float n = 0.1f, f = 20f;
        var p = new float[16];
        p[0] = sx; p[5] = sy; p[8] = 0.01f; p[9] = -0.02f;
        p[10] = -(f + n) / (f - n); p[11] = -1; p[14] = -2 * f * n / (f - n);
        return p;
    }

    static float Fov(float focal) => 2 * Mathf.Atan(1 / focal) * Mathf.Rad2Deg;

    [Test]
    public void ProjectionMatrix_KeepsARKitFieldOfViewAndPrincipalPoint()
    {
        Matrix4x4 m = ARKitCameraCompatibility.ProjectionMatrix(PortraitProjection(), 0.1f, 20f);
        Assert.That(Fov(m.m00), Is.EqualTo(34.66f).Within(0.01f));
        Assert.That(Fov(m.m11), Is.EqualTo(68.15f).Within(0.01f));
        Assert.That(m.m02, Is.EqualTo(0.01f).Within(1e-6f));
        Assert.That(m.m12, Is.EqualTo(-0.02f).Within(1e-6f));
        Assert.That(m.m32, Is.EqualTo(-1f));
    }

    [Test]
    public void ProjectionMatrix_UsesCameraClipPlanes()
    {
        Matrix4x4 m = ARKitCameraCompatibility.ProjectionMatrix(PortraitProjection(), 0.05f, 100f);
        Matrix4x4 reference = Matrix4x4.Perspective(68.15f, Mathf.Tan(34.66f * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(68.15f * 0.5f * Mathf.Deg2Rad), 0.05f, 100f);
        Assert.That(m.m22, Is.EqualTo(reference.m22).Within(1e-5f));
        Assert.That(m.m23, Is.EqualTo(reference.m23).Within(1e-5f));
    }
}
#endif
