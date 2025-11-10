using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoCollectionRobot
{
    public static class DebugTools
    {
        private static GameObject s_root;
        private static Material s_lineMaterial;

        private static void EnsureRoot()
        {
            if (s_root != null) return;
            s_root = new GameObject("DebugTools_LineRoot");
            UnityEngine.Object.DontDestroyOnLoad(s_root);

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader != null)
            {
                s_lineMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
        }


        // 绘制圆环
        public static void DrawDetectionSphere(
            Vector3 center,
            float radius,
            float duration,
            int segments = 48,
            Color color = default,
            IEnumerable<Vector3> hitPositions = null,
            Color hitColor = default,
            bool drawLinesToHits = true,
            float heightOffset = 0f)
        {
            if (color == default) color = Color.green;
            if (hitColor == default) hitColor = Color.yellow;
            segments = Mathf.Max(8, segments);

            Vector3 centerWithOffset = center + Vector3.up * heightOffset;

            DrawWithLineRenderers(centerWithOffset, radius, duration, segments, color, hitPositions, hitColor, drawLinesToHits);
        }

        private static void DrawWithLineRenderers(
            Vector3 centerWithOffset,
            float radius,
            float duration,
            int segments,
            Color color,
            IEnumerable<Vector3> hitPositions,
            Color hitColor,
            bool drawLinesToHits)
        {
            EnsureRoot();

            // 圆环
            GameObject circleObj = new GameObject("DBG_Circle");
            circleObj.transform.SetParent(s_root.transform, true);
            var lr = circleObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr, color, Mathf.Max(0.01f, radius * 0.01f));
            lr.positionCount = segments + 1;
            float angleStep = 360f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Deg2Rad * (angleStep * i);
                Vector3 pt = centerWithOffset + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                lr.SetPosition(i, pt);
            }
            lr.loop = true;

            // 自动销毁圆对象（及其子对象）在 duration 秒后
            circleObj.AddComponent<RuntimeAutoDestroy>().lifetime = duration;

            // 命中点连线与标记
            if (hitPositions != null)
            {
                float crossSize = Mathf.Max(0.05f, radius * 0.02f);
                foreach (var rawPos in hitPositions)
                {
                    Vector3 pos = rawPos + Vector3.up * 0f;
                    if (drawLinesToHits)
                    {
                        GameObject lineObj = new GameObject("DBG_LineToHit");
                        lineObj.transform.SetParent(s_root.transform, true);
                        var lrLine = lineObj.AddComponent<LineRenderer>();
                        ConfigureLineRenderer(lrLine, hitColor, Mathf.Max(0.005f, radius * 0.005f));
                        lrLine.positionCount = 2;
                        lrLine.SetPosition(0, centerWithOffset);
                        lrLine.SetPosition(1, pos);
                        lineObj.AddComponent<RuntimeAutoDestroy>().lifetime = duration;
                    }

                    CreateCrossAt(pos, hitColor, crossSize, duration);
                }
            }
        }

        private static void CreateCrossAt(Vector3 pos, Color color, float size, float lifetime)
        {
            EnsureRoot();

            var upObj = new GameObject("DBG_Cross1");
            upObj.transform.SetParent(s_root.transform, true);
            var lr1 = upObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr1, color, size * 0.15f);
            lr1.positionCount = 2;
            lr1.SetPosition(0, pos + Vector3.up * size);
            lr1.SetPosition(1, pos - Vector3.up * size);
            upObj.AddComponent<RuntimeAutoDestroy>().lifetime = lifetime;

            var lrObj2 = new GameObject("DBG_Cross2");
            lrObj2.transform.SetParent(s_root.transform, true);
            var lr2 = lrObj2.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr2, color, size * 0.15f);
            lr2.positionCount = 2;
            lr2.SetPosition(0, pos + Vector3.right * size);
            lr2.SetPosition(1, pos - Vector3.right * size);
            lrObj2.AddComponent<RuntimeAutoDestroy>().lifetime = lifetime;

            var lrObj3 = new GameObject("DBG_Cross3");
            lrObj3.transform.SetParent(s_root.transform, true);
            var lr3 = lrObj3.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr3, color, size * 0.15f);
            lr3.positionCount = 2;
            lr3.SetPosition(0, pos + Vector3.forward * size);
            lr3.SetPosition(1, pos - Vector3.forward * size);
            lrObj3.AddComponent<RuntimeAutoDestroy>().lifetime = lifetime;
        }

        private static void ConfigureLineRenderer(LineRenderer lr, Color color, float width)
        {
            if (s_lineMaterial != null) lr.material = s_lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.widthMultiplier = 1f;
            lr.loop = false;
        }

        private class RuntimeAutoDestroy : MonoBehaviour
        {
            public float lifetime = 1f;
            private float _t;

            private void OnEnable()
            {
                _t = 0f;
            }

            private void Update()
            {
                _t += Time.deltaTime;
                if (_t >= lifetime)
                {
                    try
                    {
                        Destroy(gameObject);
                    }
                    catch { }
                }
            }
        }
    }
}
