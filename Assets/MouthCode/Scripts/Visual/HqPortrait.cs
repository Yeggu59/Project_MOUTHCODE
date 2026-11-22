using UnityEngine;

namespace MouthCode
{
    /// <summary>
    /// 본부 사령관 초상 (코드로 그림). 대사가 나올 때 입을 뻐끔거린다.
    /// 실제 그림이 생기면 GameRoot.hqPortraitClosed/Open 스프라이트로 바꾸면 된다.
    /// </summary>
    public static class HqPortrait
    {
        const int S = 128;
        static Sprite closedSprite, openSprite;

        public static Sprite Closed => closedSprite != null ? closedSprite : (closedSprite = Draw(false));
        public static Sprite Open => openSprite != null ? openSprite : (openSprite = Draw(true));

        static readonly Color Ink = new Color(0.85f, 1f, 0.85f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Dark = new Color(0.03f, 0.06f, 0.04f);

        static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }

        static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
        {
            var q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
            return (q.magnitude - 1f) * Mathf.Min(r.x, r.y);
        }

        static Color Over(Color under, Color over, float a)
        {
            over.a *= a;
            float oa = over.a + under.a * (1f - over.a);
            if (oa < 1e-5f) return new Color(0, 0, 0, 0);
            var c = (over * over.a + under * under.a * (1f - over.a)) / oa;
            c.a = oa;
            return c;
        }

        static Sprite Draw(bool mouthOpen)
        {
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "HQ" };
            var px = new Color[S * S];
            float aa = 1.3f / S;
            float line = 0.028f;
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                    var col = new Color(0, 0, 0, 0);

                    // 얼굴 (어두운 바탕 + 외곽선)
                    float face = Ellipse(p, new Vector2(0.5f, 0.42f), new Vector2(0.28f, 0.31f));
                    col = Over(col, Dark, Mathf.Clamp01(-face / aa + 0.5f));
                    col = Over(col, Ink, Mathf.Clamp01((line - Mathf.Abs(face)) / aa + 0.5f));

                    // 귀
                    foreach (float ex in new[] { 0.2f, 0.8f })
                    {
                        float ear = Ellipse(p, new Vector2(ex, 0.43f), new Vector2(0.05f, 0.07f));
                        col = Over(col, Ink, Mathf.Clamp01((line * 0.8f - Mathf.Abs(ear)) / aa + 0.5f));
                    }

                    // 군모: 윗부분 + 챙 + 금색 배지
                    if (p.y > 0.62f && p.y < 0.86f && Mathf.Abs(p.x - 0.5f) < 0.33f - (p.y - 0.62f) * 0.35f)
                        col = Over(col, new Color(0.12f, 0.3f, 0.18f), 1f);
                    float capTop = Seg(p, new Vector2(0.25f, 0.86f), new Vector2(0.75f, 0.86f));
                    float capL = Seg(p, new Vector2(0.17f, 0.62f), new Vector2(0.25f, 0.86f));
                    float capR = Seg(p, new Vector2(0.83f, 0.62f), new Vector2(0.75f, 0.86f));
                    float visor = Seg(p, new Vector2(0.12f, 0.61f), new Vector2(0.88f, 0.61f));
                    float capD = Mathf.Min(Mathf.Min(capTop, capL), Mathf.Min(capR, visor * 0.8f));
                    col = Over(col, Ink, Mathf.Clamp01((line - capD) / aa + 0.5f));
                    float badge = (p - new Vector2(0.5f, 0.74f)).magnitude - 0.045f;
                    col = Over(col, Gold, Mathf.Clamp01(-badge / aa + 0.5f));

                    // 화난 눈썹 + 눈
                    float browL = Seg(p, new Vector2(0.32f, 0.55f), new Vector2(0.45f, 0.51f));
                    float browR = Seg(p, new Vector2(0.68f, 0.55f), new Vector2(0.55f, 0.51f));
                    col = Over(col, Ink, Mathf.Clamp01((line * 1.2f - Mathf.Min(browL, browR)) / aa + 0.5f));
                    foreach (float ex in new[] { 0.4f, 0.6f })
                    {
                        float eye = (p - new Vector2(ex, 0.46f)).magnitude - 0.03f;
                        col = Over(col, Ink, Mathf.Clamp01(-eye / aa + 0.5f));
                    }

                    // 코
                    float nose = Seg(p, new Vector2(0.5f, 0.44f), new Vector2(0.47f, 0.36f));
                    col = Over(col, Ink, Mathf.Clamp01((line * 0.8f - nose) / aa + 0.5f));

                    // 입 (열림/닫힘) — 콧수염보다 먼저 그려 수염이 위로 덮게
                    if (mouthOpen)
                    {
                        float m = Ellipse(p, new Vector2(0.5f, 0.24f), new Vector2(0.07f, 0.06f));
                        col = Over(col, new Color(0.5f, 0.1f, 0.08f), Mathf.Clamp01(-m / aa + 0.5f));
                        col = Over(col, Ink, Mathf.Clamp01((line * 0.8f - Mathf.Abs(m)) / aa + 0.5f));
                    }
                    else
                    {
                        float m = Seg(p, new Vector2(0.43f, 0.25f), new Vector2(0.57f, 0.25f));
                        col = Over(col, Ink, Mathf.Clamp01((line * 0.8f - m) / aa + 0.5f));
                    }

                    // 큰 콧수염 (금색)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float st = Ellipse(p, new Vector2(0.5f + side * 0.09f, 0.31f), new Vector2(0.1f, 0.035f));
                        col = Over(col, Gold, Mathf.Clamp01(-st / aa + 0.5f));
                        float tip = Seg(p, new Vector2(0.5f + side * 0.17f, 0.31f), new Vector2(0.5f + side * 0.24f, 0.36f));
                        col = Over(col, Gold, Mathf.Clamp01((0.022f - tip) / aa + 0.5f));
                    }

                    px[y * S + x] = col;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
