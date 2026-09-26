// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace HoloKit.Samples.LowLatencyTracking
{
    /// <summary>
    /// Checks world-locking against a real table: detected horizontal planes show as a
    /// dot grid (8 mm dots every 5 cm), and 3.5 cm cubes stand on the table in 4 columns
    /// from 30 cm in front of the wearer to its far edge. Lag, drift or shake shows as
    /// cubes sliding over or floating above the table while the head moves.
    /// </summary>
    public class TrackingGrid : MonoBehaviour
    {
        [SerializeField] ARPlaneManager m_PlaneManager;
        [SerializeField] HoloKitCameraManager m_CameraManager;
        [SerializeField] LowLatencyTrackingManager m_Tracking;
        [SerializeField] Material m_CubeMaterial;
        [SerializeField] Material m_DotMaterial;

        const float DotSpacing = 0.05f, DotSize = 0.008f, CubeSize = 0.035f;
        const float ColumnSpacing = 0.10f, RowSpacing = 0.12f, FirstRow = 0.30f;
        const int Columns = 4, Rows = 12;

        readonly Dictionary<TrackableId, (GameObject dots, float area)> m_Planes = new();
        readonly List<GameObject> m_Cubes = new();
        bool m_PendingPlacement = true;
        float m_LastRebuild;
        string m_Status = "Look at a table";

        void Start()
        {
            if (m_CameraManager == null) m_CameraManager = FindFirstObjectByType<HoloKitCameraManager>();
            if (m_Tracking == null) m_Tracking = FindFirstObjectByType<LowLatencyTrackingManager>();
            m_PlaneManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(mesh.GetComponent<Collider>());
            for (int i = 0; i < Columns * Rows; i++)
            {
                var cube = Instantiate(mesh, transform);
                cube.name = $"Cube {i}";
                cube.transform.localScale = Vector3.one * CubeSize;
                // Bright, distinct hues: optical see-through shows only light.
                cube.GetComponent<Renderer>().material = new Material(m_CubeMaterial)
                    { color = Color.HSVToRGB(i / (float)(Columns * Rows), 0.75f, 1) };
                cube.SetActive(false);
                m_Cubes.Add(cube);
            }
            Destroy(mesh);
        }

        /// Places the cubes again where the wearer looks next.
        public void Replace() => m_PendingPlacement = true;

        void Update()
        {
            if (Time.time - m_LastRebuild > 1)
            {
                m_LastRebuild = Time.time;
                foreach (var plane in m_PlaneManager.trackables) UpdateDots(plane);
            }
            if (m_PendingPlacement) TryPlace();
        }

        void UpdateDots(ARPlane plane)
        {
            float area = plane.size.x * plane.size.y;
            if (m_Planes.TryGetValue(plane.trackableId, out var entry) && area < entry.area * 1.1f) return;
            var polygon = new List<Vector2>(plane.boundary);
            if (polygon.Count < 3) return;
            Vector2 low = polygon[0], high = polygon[0];
            foreach (var p in polygon) { low = Vector2.Min(low, p); high = Vector2.Max(high, p); }
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            const float h = DotSize / 2;
            for (float x = Mathf.Ceil(low.x / DotSpacing) * DotSpacing; x <= high.x; x += DotSpacing)
            for (float z = Mathf.Ceil(low.y / DotSpacing) * DotSpacing; z <= high.y; z += DotSpacing)
            {
                if (vertices.Count > 60000 || !Contains(polygon, new Vector2(x, z))) continue;
                int b = vertices.Count;
                vertices.Add(new Vector3(x - h, 0.001f, z - h));
                vertices.Add(new Vector3(x - h, 0.001f, z + h));
                vertices.Add(new Vector3(x + h, 0.001f, z + h));
                vertices.Add(new Vector3(x + h, 0.001f, z - h));
                indices.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var dots = entry.dots;
            if (dots == null)
            {
                dots = new GameObject("Plane Dots");
                dots.transform.SetParent(plane.transform, false);
                dots.AddComponent<MeshFilter>();
                dots.AddComponent<MeshRenderer>().sharedMaterial = m_DotMaterial;
            }
            var dotMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            dotMesh.SetVertices(vertices);
            dotMesh.SetTriangles(indices, 0);
            dots.GetComponent<MeshFilter>().sharedMesh = dotMesh;
            m_Planes[plane.trackableId] = (dots, area);
        }

        void TryPlace()
        {
            Transform head = m_CameraManager.CenterEyePose;
            var gaze = new Ray(head.position, head.forward);
            foreach (var plane in m_PlaneManager.trackables)
            {
                var surface = new Plane(plane.transform.up, plane.transform.position);
                if (!surface.Raycast(gaze, out float distance) || distance > 2.5f) continue;
                Vector3 hit = gaze.GetPoint(distance);
                if (hit.y > head.position.y - 0.15f) continue;   // a table or floor, below the eyes
                var polygon = new List<Vector2>(plane.boundary);
                Vector3 local = plane.transform.InverseTransformPoint(hit);
                if (!Contains(polygon, new Vector2(local.x, local.z))) continue;
                Place(plane, polygon, head);
                m_PendingPlacement = false;
                return;
            }
        }

        void Place(ARPlane plane, List<Vector2> polygon, Transform head)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            int placed = 0;
            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                var cube = m_Cubes[row * Columns + column];
                Vector3 p = head.position + right * ((column - (Columns - 1) / 2f) * ColumnSpacing)
                                          + forward * (FirstRow + row * RowSpacing);
                p.y = plane.transform.position.y;
                Vector3 local = plane.transform.InverseTransformPoint(p);
                bool inside = Contains(polygon, new Vector2(local.x, local.z));
                cube.SetActive(inside);
                if (!inside) continue;
                cube.transform.SetPositionAndRotation(p + Vector3.up * CubeSize / 2, Quaternion.LookRotation(forward));
                placed++;
            }
            m_Status = $"{placed} cubes on the table";
        }

        /// Even-odd point-in-polygon test in plane coordinates.
        public static bool Contains(List<Vector2> polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        void OnGUI()
        {
            string timing = m_Tracking != null && m_Tracking.IsPredicting
                ? $"predicting +{m_Tracking.PredictionLeadTime * 1000:F1} ms (display {m_Tracking.PresentLatency * 1000:F1} ms) · {1f / Time.smoothDeltaTime:F0} fps"
                : "not predicting (Stereo only)";
            GUI.skin.label.fontSize = 28;
            GUI.skin.button.fontSize = 28;
            GUI.Label(new Rect(420, 30, 1600, 40), $"{m_Status} · {m_Planes.Count} planes · {timing}");
            if (GUI.Button(new Rect(420, 75, 180, 50), "Re-place")) Replace();
        }
    }
}
