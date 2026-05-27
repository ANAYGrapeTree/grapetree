using UnityEngine;
using System.Collections.Generic;

namespace GTK.UVToolkit
{
    public static class UVToolkitUtility
    {
        public static Color EvaluateHeatColor(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.5f)
                return Color.Lerp(Color.blue, Color.yellow, t * 2f);
            return Color.Lerp(Color.yellow, Color.red, (t - 0.5f) * 2f);
        }

        public static float SutherlandHodgmanArea(Vector2[] triA, Vector2[] triB)
        {
            var clip = new List<Vector2>(triB);

            for (int edge = 0; edge < 3; edge++)
            {
                var cp1 = triA[edge];
                var cp2 = triA[(edge + 1) % 3];
                float nx = -(cp2.y - cp1.y);
                float ny = cp2.x - cp1.x;

                var input = new List<Vector2>(clip);
                clip.Clear();
                if (input.Count == 0) break;

                int n = input.Count;
                var s = input[n - 1];
                bool sIn = (s.x - cp1.x) * nx + (s.y - cp1.y) * ny >= 0f;
                for (int i = 0; i < n; i++)
                {
                    var e = input[i];
                    bool eIn = (e.x - cp1.x) * nx + (e.y - cp1.y) * ny >= 0f;
                    if (eIn)
                    {
                        if (!sIn)
                        {
                            float d = (e.x - s.x) * nx + (e.y - s.y) * ny;
                            if (d != 0f)
                            {
                                float t = ((cp1.x - s.x) * nx + (cp1.y - s.y) * ny) / d;
                                if (t > 0f && t < 1f)
                                    clip.Add(new Vector2(s.x + (e.x - s.x) * t, s.y + (e.y - s.y) * t));
                            }
                        }
                        clip.Add(e);
                    }
                    else if (sIn)
                    {
                        float d = (e.x - s.x) * nx + (e.y - s.y) * ny;
                        if (d != 0f)
                        {
                            float t = ((cp1.x - s.x) * nx + (cp1.y - s.y) * ny) / d;
                            if (t > 0f && t < 1f)
                                clip.Add(new Vector2(s.x + (e.x - s.x) * t, s.y + (e.y - s.y) * t));
                        }
                    }
                    s = e;
                    sIn = eIn;
                }
            }

            if (clip.Count < 3) return 0f;
            float area = 0f;
            for (int i = 0; i < clip.Count; i++)
            {
                int j = (i + 1) % clip.Count;
                area += clip[i].x * clip[j].y - clip[j].x * clip[i].y;
            }
            return Mathf.Abs(area) / 2f;
        }
    }
}
