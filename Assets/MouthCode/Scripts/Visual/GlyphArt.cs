using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthCode
{
    /// <summary>
    /// 임시 암호 글자 생성기. 시드로 괴상한 도형 글자를 만든다 (가끔 눈알이 달림).
    /// GlyphDef.sprite / SfxDef.icon에 실제 스프라이트가 들어오면 그걸 쓴다.
    /// </summary>
    public static class GlyphArt
    {
        const int Size = 128;
        static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();
        static readonly Color LightInk = new Color(0.85f, 1f, 0.85f);   // CRT 화면 위 (녹색 인광)
        static readonly Color DarkInk = new Color(0.13f, 0.12f, 0.1f);   // 종이 위 (검은 잉크)

        /// <param name="paper">true면 종이에 찍힌 검은 잉크 버전</param>
        public static Sprite Get(GlyphDef g, bool paper = false) => g.sprite != null ? g.sprite : Generate(g.shapeSeed, false, paper);
        public static Sprite Get(SfxDef s, bool paper = false) => s.icon != null ? s.icon : Generate(s.shapeSeed, true, paper);

        abstract class Shape { public abstract float Dist(Vector2 p); }

        class Seg : Shape
        {
            public Vector2 a, b;
            public override float Dist(Vector2 p)
            {
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                return (a + ab * t - p).magnitude;
            }
        }

        class Arc : Shape
        {
            public Vector2 c; public float r, a0, span;
            public override float Dist(Vector2 p)
            {
                var d = p - c;
                float ang = Mathf.Atan2(d.y, d.x);
                float rel = Mathf.Repeat(ang - a0, Mathf.PI * 2f);
                if (rel <= span) return Mathf.Abs(d.magnitude - r);
                var e0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                var e1 = c + new Vector2(Mathf.Cos(a0 + span), Mathf.Sin(a0 + span)) * r;
                return Mathf.Min((p - e0).magnitude, (p - e1).magnitude);
            }
        }

        class Dot : Shape
        {
            public Vector2 c; public float r;
            public override float Dist(Vector2 p) => Mathf.Max(0f, (p - c).magnitude - r);
        }

        public static Sprite Generate(int seed, bool framed, bool paper = false)
        {
            int key = seed * 4 + (framed ? 1 : 0) + (paper ? 2 : 0);
            var Ink = paper ? DarkInk : LightInk;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            Vector2 P(float lo = 0.2f, float hi = 0.8f) => new Vector2(Mathf.Lerp(lo, hi, R()), Mathf.Lerp(lo, hi, R()));

            var shapes = new List<Shape>();
            int count = 2 + rng.Next(3);
            for (int i = 0; i < count; i++)
            {
                switch (rng.Next(5))
                {
                    case 0: // 꺾인 선
                    {
                        var pts = 2 + rng.Next(3);
                        var prev = P();
                        for (int k = 0; k < pts; k++) { var nx = P(); shapes.Add(new Seg { a = prev, b = nx }); prev = nx; }
                        break;
                    }
                    case 1: // 고리/호
                        shapes.Add(new Arc { c = P(0.35f, 0.65f), r = Mathf.Lerp(0.1f, 0.25f, R()), a0 = R() * 6.28f, span = Mathf.Lerp(2.2f, 6.29f, R()) });
                        break;
                    case 2: // 점
                        shapes.Add(new Dot { c = P(), r = Mathf.Lerp(0.03f, 0.06f, R()) });
                        break;
                    case 3: // 지그재그
                    {
                        float y = Mathf.Lerp(0.25f, 0.75f, R());
                        float amp = Mathf.Lerp(0.05f, 0.12f, R());
                        int teeth = 3 + rng.Next(3);
                        var prev = new Vector2(0.22f, y);
                        for (int k = 1; k <= teeth; k++)
                        {
                            var nx = new Vector2(Mathf.Lerp(0.22f, 0.78f, k / (float)teeth), y + ((k & 1) == 1 ? amp : -amp));
                            shapes.Add(new Seg { a = prev, b = nx }); prev = nx;
                        }
                        break;
                    }
                    default: // 갈고리
                    {
                        var a = P(); var b = P();
                        shapes.Add(new Seg { a = a, b = b });
                        shapes.Add(new Arc { c = b + new Vector2(0.07f, 0f), r = 0.07f, a0 = 3.14f, span = 3.4f });
                        break;
                    }
                }
            }

            bool eyes = R() < 0.4f;
            Vector2 eyeC = new Vector2(Mathf.Lerp(0.38f, 0.62f, R()), Mathf.Lerp(0.6f, 0.75f, R()));
            float eyeGap = Mathf.Lerp(0.09f, 0.13f, R());
            float eyeR = Mathf.Lerp(0.065f, 0.085f, R());
            Vector2 look = new Vector2(R() - 0.5f, R() - 0.5f).normalized * eyeR * 0.45f;
            bool cyclops = R() < 0.25f;

            float thick = framed ? 0.035f : 0.045f;
            float scale = framed ? 0.72f : 1f; // 프레임 안쪽으로 축소
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Glyph_" + seed + (paper ? "_p" : ""),
            };
            var px = new Color[Size * Size];
            float aa = 1.2f / Size;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);
                    var q = (p - new Vector2(0.5f, 0.5f)) / scale + new Vector2(0.5f, 0.5f);
                    float d = 9f;
                    foreach (var s in shapes) d = Mathf.Min(d, s.Dist(q));
                    float a = Mathf.Clamp01((thick - d * scale) / aa + 0.5f);
                    var col = new Color(Ink.r, Ink.g, Ink.b, a);

                    if (framed)
                    {
                        // 효과음 아이콘은 노란 둥근 사각 테두리로 글자와 구분
                        var c = p - new Vector2(0.5f, 0.5f);
                        var qd = new Vector2(Mathf.Abs(c.x), Mathf.Abs(c.y)) - new Vector2(0.38f, 0.38f);
                        float box = new Vector2(Mathf.Max(qd.x, 0), Mathf.Max(qd.y, 0)).magnitude + Mathf.Min(Mathf.Max(qd.x, qd.y), 0f) - 0.06f;
                        float fa = Mathf.Clamp01((0.025f - Mathf.Abs(box)) / aa + 0.5f);
                        var frameCol = paper ? new Color(0.72f, 0.16f, 0.1f, fa) : new Color(1f, 0.85f, 0.3f, fa);
                        col = Over(col, frameCol);
                    }

                    if (eyes)
                    {
                        var e1 = eyeC + new Vector2(-eyeGap, 0f);
                        var e2 = eyeC + new Vector2(eyeGap, 0f);
                        if (cyclops) { e1 = eyeC; e2 = new Vector2(-9, -9); }
                        col = DrawEye(col, p, e1, cyclops ? eyeR * 1.4f : eyeR, look, aa);
                        col = DrawEye(col, p, e2, eyeR, look, aa);
                    }
                    px[y * Size + x] = col;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            Cache[key] = sprite;
            return sprite;
        }

        static Color DrawEye(Color under, Vector2 p, Vector2 c, float r, Vector2 look, float aa)
        {
            float d = (p - c).magnitude;
            float outline = Mathf.Clamp01((r + 0.012f - d) / aa + 0.5f);
            under = Over(under, new Color(0.05f, 0.08f, 0.06f, outline));
            float white = Mathf.Clamp01((r - d) / aa + 0.5f);
            under = Over(under, new Color(1f, 1f, 1f, white));
            float pd = (p - (c + look)).magnitude;
            float pupil = Mathf.Clamp01((r * 0.45f - pd) / aa + 0.5f);
            under = Over(under, new Color(0.02f, 0.02f, 0.02f, pupil));
            return under;
        }

        static Color Over(Color under, Color over)
        {
            float a = over.a + under.a * (1f - over.a);
            if (a < 1e-5f) return new Color(0, 0, 0, 0);
            var c = (over * over.a + under * under.a * (1f - over.a)) / a;
            c.a = a;
            return c;
        }
    }
}
