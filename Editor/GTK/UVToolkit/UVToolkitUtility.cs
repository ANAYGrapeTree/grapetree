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
            var clipPoly = new List<Vector2>(triB);

            for (int edge = 0; edge < 3; edge++)
            {
                var cp1 = triA[edge];
                var cp2 = triA[(edge + 1) % 3];
                var normal = new Vector2(-(cp2.y - cp1.y), cp2.x - cp1.x);

                var input = new List<Vector2>(clipPoly);
                clipPoly.Clear();
                if (input.Count == 0) break;

                var start = input[^1];
                bool startInside = Vector2.Dot(start - cp1, normal) >= 0;

                foreach (var end in input)
                {
                    bool endInside = Vector2.Dot(end - cp1, normal) >= 0;
                    if (endInside)
                    {
                        if (!startInside)
                        {
                            float t = Vector2.Dot(cp1 - start, normal) / Vector2.Dot(end - start, normal);
                            clipPoly.Add(Vector2.Lerp(start, end, t));
                        }
                        clipPoly.Add(end);
                    }
                    else if (startInside)
                    {
                        float t = Vector2.Dot(cp1 - start, normal) / Vector2.Dot(end - start, normal);
                        clipPoly.Add(Vector2.Lerp(start, end, t));
                    }
                    start = end;
                    startInside = endInside;
                }
            }

            if (clipPoly.Count < 3) return 0f;
            float area = 0f;
            for (int i = 0; i < clipPoly.Count; i++)
            {
                int j = (i + 1) % clipPoly.Count;
                area += clipPoly[i].x * clipPoly[j].y;
                area -= clipPoly[j].x * clipPoly[i].y;
            }
            return Mathf.Abs(area) / 2f;
        }
    }
}
