using UnityEngine;

namespace MouthCode
{
    /// <summary>
    /// UI 재질(종이·금속·CRT 화면·도장·테이프 등)을 코드로 그린다. 외부 그림 파일 없이 동작한다.
    /// 반복 무늬(Tile*)는 RawImage + TileUV로 깔고, 테두리 있는 것은 9-slice 스프라이트로 쓴다.
    /// </summary>
    public static class UISkin
    {
        // 반복 무늬
        public static Texture2D Grain { get; private set; }     // 종이 결
        public static Texture2D Brushed { get; private set; }   // 금속 결
        public static Texture2D DeskTex { get; private set; }   // 책상
        public static Texture2D Scan { get; private set; }      // CRT 주사선
        public static Texture2D Hazard { get; private set; }    // 노랑·검정 경고 줄무늬
        public static Texture2D Dash { get; private set; }      // 점선
        // 늘려 쓰는 것
        public static Texture2D Lamp { get; private set; }      // 스탠드 조명
        public static Texture2D Glare { get; private set; }     // 화면 반사광
        // 스프라이트
        public static Sprite PaperEdge { get; private set; }
        public static Sprite Shadow { get; private set; }
        public static Sprite Metal { get; private set; }
        public static Sprite Crt { get; private set; }
        public static Sprite Glow { get; private set; }
        public static Sprite Circle { get; private set; }
        public static Sprite StampFrame { get; private set; }
        public static Sprite Tape { get; private set; }
        public static Sprite Border { get; private set; }
        public static Sprite KeyCap { get; private set; }
        public static Sprite Dome { get; private set; }
        public static Sprite DomeLit { get; private set; }

        public static void Init()
        {
            if (Grain != null) return;
            Grain = MakeGrain();
            Brushed = MakeBrushed();
            DeskTex = MakeDesk();
            Scan = MakeScan();
            Hazard = MakeHazard();
            Dash = MakeDash();
            Lamp = MakeLamp();
            Glare = MakeGlare();
            PaperEdge = MakePaperEdge();
            Shadow = MakeShadow();
            Metal = MakeMetal();
            Crt = MakeScreen();
            Glow = MakeGlow();
            Circle = MakeCircle();
            StampFrame = MakeStamp();
            Tape = MakeTape();
            Border = MakeBorder();
            KeyCap = MakeKeyCap();
            Dome = MakeDome(false);
            DomeLit = MakeDome(true);
        }

        // ───────────────────────── 공통 ─────────────────────────

        static Texture2D NewTex(int w, int h, bool repeat, FilterMode filter = FilterMode.Bilinear, string name = "skin")
            => new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = filter,
                name = name,
                hideFlags = HideFlags.DontSave,
            };

        static Sprite Sp(Texture2D t, float border)
            => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(border, border, border, border));

        static Texture2D Fill(Texture2D t, System.Func<int, int, Color> f)
        {
            var px = new Color[t.width * t.height];
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                    px[y * t.width + x] = f(x, y);
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665 + 1013904223);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>반복되는 값 노이즈 (periodX/Y 칸마다 이어짐).</summary>
        static float Noise(float x, float y, int periodX, int periodY, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float H(int a, int b) => Hash(((a % periodX) + periodX) % periodX, ((b % periodY) + periodY) % periodY, seed);
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
        }

        /// <summary>둥근 사각형 거리 (안쪽 음수).</summary>
        static float RRect(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static Vector2 P(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);

        /// <summary>셰이더식 smoothstep(e0, e1, x): x가 e0→e1로 갈 때 0→1. (Unity의 Mathf.SmoothStep은 보간 함수라 다름)</summary>
        static float Edge(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // ───────────────────────── 반복 무늬 ─────────────────────────

        static Texture2D MakeGrain()
        {
            const int S = 256;
            return Fill(NewTex(S, S, true, FilterMode.Bilinear, "Grain"), (x, y) =>
            {
                float n = 0.55f * Noise(x / 32f, y / 32f, 8, 8, 1) + 0.3f * Noise(x / 8f, y / 8f, 32, 32, 2) + 0.15f * Hash(x, y, 3);
                float fiber = Noise(x / 64f, y / 2f, 4, 128, 4);
                float a = Mathf.Clamp01((n - 0.3f) * 0.2f) + (fiber > 0.82f ? 0.045f : 0f);
                if (Hash(x, y, 5) > 0.9975f) a += 0.25f; // 잡티
                return new Color(0.32f, 0.23f, 0.12f, a);
            });
        }

        static Texture2D MakeBrushed()
        {
            return Fill(NewTex(256, 64, true, FilterMode.Bilinear, "Brushed"), (x, y) =>
            {
                float n = 0.6f * Noise(x / 64f, y, 4, 64, 7) + 0.4f * Noise(x / 16f, y * 0.5f, 16, 32, 8);
                return new Color(n, n, n, 0.09f);
            });
        }

        static Texture2D MakeDesk()
        {
            const int S = 256;
            return Fill(NewTex(S, S, true, FilterMode.Bilinear, "Desk"), (x, y) =>
            {
                float n = 0.5f * Noise(x / 32f, y / 32f, 8, 8, 11) + 0.3f * Noise(x / 8f, y / 8f, 32, 32, 12) + 0.2f * Hash(x, y, 13);
                // 나뭇결
                float grain = Mathf.Sin((y + Noise(x / 64f, y / 16f, 4, 16, 14) * 24f) * 0.19f) * 0.5f + 0.5f;
                float v = 0.085f + (n - 0.5f) * 0.03f + grain * 0.012f;
                return new Color(v * 1.08f, v, v * 0.86f, 1f);
            });
        }

        static Texture2D MakeScan()
        {
            var t = NewTex(2, 3, true, FilterMode.Point, "Scan");
            var a = new Color(0, 0, 0, 0.3f); var b = new Color(0, 0, 0, 0f); var c = new Color(0, 0, 0, 0.1f);
            t.SetPixels(new[] { a, a, b, b, c, c });
            t.Apply();
            return t;
        }

        static Texture2D MakeHazard()
        {
            const int S = 64;
            var yellow = new Color(0.96f, 0.76f, 0.12f);
            var black = new Color(0.1f, 0.1f, 0.08f);
            return Fill(NewTex(S, S, true, FilterMode.Bilinear, "Hazard"), (x, y) =>
            {
                float u = Mathf.Repeat((x + y + 1f) / 32f, 1f);
                float edge = Mathf.Min(Mathf.Abs(u - 0.5f), Mathf.Min(u, 1f - u)) * 32f; // 줄 경계까지 픽셀 거리
                float k = u < 0.5f ? 1f : 0f;
                var col = Color.Lerp(black, yellow, k);
                if (edge < 1f) col = Color.Lerp(Color.Lerp(black, yellow, 0.5f), col, edge);
                float wear = Noise(x / 8f, y / 8f, 8, 8, 21);
                col *= 0.85f + 0.15f * wear;
                col.a = 1f;
                return col;
            });
        }

        static Texture2D MakeDash()
        {
            return Fill(NewTex(16, 2, true, FilterMode.Point, "Dash"), (x, y) => new Color(1, 1, 1, x < 9 ? 1f : 0f));
        }

        static Texture2D MakeLamp()
        {
            const int S = 128;
            return Fill(NewTex(S, S, false, FilterMode.Bilinear, "Lamp"), (x, y) =>
            {
                float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.58f;
                float r = Mathf.Sqrt(dx * dx * 0.7f + dy * dy) / 0.62f;
                float a = 0.2f * Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, r), 1.6f);
                return new Color(1f, 0.88f, 0.64f, a);
            });
        }

        static Texture2D MakeGlare()
        {
            const int S = 128;
            return Fill(NewTex(S, S, false, FilterMode.Bilinear, "Glare"), (x, y) =>
            {
                float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                float sheen = Mathf.Clamp01(1.15f - (u + (1f - v)) * 0.9f);
                float center = 1f - Mathf.Clamp01(new Vector2(u - 0.5f, (v - 0.5f) * 1.4f).magnitude * 1.8f);
                return new Color(0.75f, 1f, 0.8f, sheen * sheen * 0.07f + center * 0.035f);
            });
        }

        // ───────────────────────── 스프라이트 ─────────────────────────

        static Sprite MakePaperEdge()
        {
            const int S = 64; const float B = 22;
            var t = Fill(NewTex(S, S, false, FilterMode.Bilinear, "PaperEdge"), (x, y) =>
            {
                float d = Mathf.Min(Mathf.Min(x + 0.5f, S - x - 0.5f), Mathf.Min(y + 0.5f, S - y - 0.5f));
                float a = 0.2f * (1f - Edge(0f, B - 2f, d));
                a += (Hash(x, y, 31) - 0.5f) * 0.03f;
                if (d < 1.2f) a = Mathf.Max(a, 0.4f);
                return new Color(0.36f, 0.27f, 0.14f, Mathf.Clamp01(a));
            });
            return Sp(t, B);
        }

        static Sprite MakeShadow()
        {
            const int S = 80; const float B = 34;
            var t = Fill(NewTex(S, S, false, FilterMode.Bilinear, "Shadow"), (x, y) =>
            {
                float d = RRect(P(x, y), new Vector2(S / 2f, S / 2f), new Vector2(S / 2f - B, S / 2f - B), 4f);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / (B - 2f)));
                return new Color(0, 0, 0, a * a);
            });
            return Sp(t, B);
        }

        static Sprite MakeMetal()
        {
            const int S = 96; const float B = 30;
            var c = new Vector2(S / 2f, S / 2f);
            var screws = new[] { new Vector2(14, 14), new Vector2(S - 14, 14), new Vector2(14, S - 14), new Vector2(S - 14, S - 14) };
            var t = Fill(NewTex(S, S, false, FilterMode.Bilinear, "Metal"), (x, y) =>
            {
                var p = P(x, y);
                float sdf = RRect(p, c, new Vector2(S / 2f - 1, S / 2f - 1), 10f);
                float alpha = Mathf.Clamp01(0.5f - sdf);
                if (alpha <= 0f) return new Color(0, 0, 0, 0);
                float edge = -sdf;
                float nx = (p.x - c.x) / c.x, ny = (p.y - c.y) / c.y;
                float dir = Mathf.Clamp((-nx + ny) * 3f, -1f, 1f); // 왼쪽 위에서 빛
                float v = 0.78f + 0.26f * dir * (1f - Edge(0f, 5f, edge));
                if (edge < 1.3f) v = 0.12f;
                for (int i = 0; i < screws.Length; i++)
                {
                    var s = screws[i];
                    float ds = (p - s).magnitude;
                    if (ds < 7f)
                    {
                        var l = (p - s) / 6f;
                        v = 0.62f + 0.3f * Mathf.Clamp01(-l.x * 0.6f + l.y * 0.8f) * 0.8f + 0.15f;
                        float ang = 0.6f + i * 1.1f;
                        var n = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        if (Mathf.Abs(Vector2.Dot(p - s, n)) < 1.1f && ds < 5f) v = 0.22f;
                        if (ds > 5.6f) v = 0.25f;
                    }
                }
                return new Color(v, v, v, alpha);
            });
            return Sp(t, B);
        }

        static Sprite MakeScreen()
        {
            const int S = 96; const float B = 34;
            var c = new Vector2(S / 2f, S / 2f);
            var glass = new Color(0.03f, 0.085f, 0.05f);
            var t = Fill(NewTex(S, S, false, FilterMode.Bilinear, "Screen"), (x, y) =>
            {
                var p = P(x, y);
                float sdf = RRect(p, c, new Vector2(S / 2f - 1, S / 2f - 1), 16f);
                float alpha = Mathf.Clamp01(0.5f - sdf);
                if (alpha <= 0f) return new Color(0, 0, 0, 0);
                float edge = -sdf;
                Color col;
                if (edge < 5f)
                {
                    // 화면 테두리 (아래쪽에 살짝 반사광)
                    float hl = y < S / 2 ? 0.12f * (1f - edge / 5f) : 0.02f;
                    col = new Color(0.02f + hl, 0.025f + hl, 0.02f + hl);
                }
                else
                {
                    float f = Edge(5f, 30f, edge);
                    col = glass * (0.3f + 0.7f * f);
                }
                col.a = alpha;
                return col;
            });
            return Sp(t, B);
        }

        static Sprite MakeGlow()
        {
            const int S = 64;
            var t = Fill(NewTex(S, S, false, FilterMode.Bilinear, "Glow"), (x, y) =>
            {
                float d = (P(x, y) - new Vector2(S / 2f, S / 2f)).magnitude / (S / 2f);
                float a = Mathf.Exp(-d * d * 5f) * (1f - Edge(0.85f, 1f, d));
                return new Color(1, 1, 1, a);
            });
            return Sp(t, 0);
        }

        static Sprite MakeCircle()
        {
            const int S = 64;
            var t = Fill(NewTex(S, S, false, FilterMode.Bilinear, "Circle"), (x, y) =>
            {
                float d = (P(x, y) - new Vector2(S / 2f, S / 2f)).magnitude;
                return new Color(1, 1, 1, Mathf.Clamp01(S / 2f - 1f - d + 0.5f));
            });
            return Sp(t, 0);
        }

        static Sprite MakeStamp()
        {
            const int W = 160, H = 96; const float B = 30;
            var c = new Vector2(W / 2f, H / 2f);
            var t = Fill(NewTex(W, H, false, FilterMode.Bilinear, "Stamp"), (x, y) =>
            {
                var p = P(x, y);
                float sdf = RRect(p, c, new Vector2(W / 2f - 3, H / 2f - 3), 12f);
                float outer = Mathf.Clamp01(3.2f - Mathf.Abs(sdf + 3.2f) + 0.5f);
                float inner = Mathf.Clamp01(1.3f - Mathf.Abs(sdf + 11f) + 0.5f);
                float a = Mathf.Max(outer, inner);
                // 잉크가 덜 묻은 자국
                float n = Noise(x / 5f, y / 5f, 64, 64, 41);
                if (n < 0.3f) a *= 0.2f;
                else if (n < 0.38f) a *= 0.6f;
                if (Hash(x, y, 42) > 0.93f) a *= 0.3f;
                return new Color(1, 1, 1, a);
            });
            return Sp(t, B);
        }

        static Sprite MakeTape()
        {
            const int W = 112, H = 36;
            var t = Fill(NewTex(W, H, false, FilterMode.Bilinear, "Tape"), (x, y) =>
            {
                float cutL = 3f + 2.2f * Mathf.Abs(Mathf.Sin(y * 1.7f)) + Hash(0, y, 51) * 2f;
                float cutR = 3f + 2.2f * Mathf.Abs(Mathf.Sin(y * 1.3f + 1f)) + Hash(1, y, 52) * 2f;
                if (x < cutL || x > W - 1 - cutR) return new Color(0, 0, 0, 0);
                float streak = Noise(x / 3f, y / 18f, 64, 4, 53);
                float a = 0.72f + streak * 0.12f;
                if (y == 1 || y == H - 2) a += 0.08f;
                return new Color(0.94f, 0.9f, 0.74f, a);
            });
            return Sp(t, 0);
        }

        static Sprite MakeBorder()
        {
            const int S = 48; const float B = 14;
            var c = new Vector2(S / 2f, S / 2f);
            var t = Fill(NewTex(S, S, false, FilterMode.Bilinear, "Border"), (x, y) =>
            {
                float sdf = RRect(P(x, y), c, new Vector2(S / 2f - 1, S / 2f - 1), 6f);
                float a = Mathf.Clamp01(1.6f - Mathf.Abs(sdf + 1.8f) + 0.5f);
                a *= 0.82f + 0.18f * Hash(x, y, 61);
                return new Color(1, 1, 1, a);
            });
            return Sp(t, B);
        }

        static Sprite MakeKeyCap()
        {
            const int S = 64; const float B = 22;
            var c = new Vector2(S / 2f, S / 2f);
            var t = Fill(NewTex(S, S, false, FilterMode.Bilinear, "KeyCap"), (x, y) =>
            {
                var p = P(x, y);
                float sdf = RRect(p, c, new Vector2(S / 2f - 1, S / 2f - 1), 9f);
                float alpha = Mathf.Clamp01(0.5f - sdf);
                if (alpha <= 0f) return new Color(0, 0, 0, 0);
                float edge = -sdf;
                float v = Mathf.Lerp(0.66f, 0.92f, y / (float)S);
                if (y < 7) v = 0.42f;                              // 아래 두께
                if (edge < 1.3f) v = 0.1f;                         // 외곽선
                else if (edge < 3f && y > S - 8) v = 1f;           // 윗면 하이라이트
                return new Color(v, v, v, alpha);
            });
            return Sp(t, B);
        }

        static Sprite MakeDome(bool lit)
        {
            const int S = 256;
            var c = new Vector2(S / 2f, S / 2f);
            var L = new Vector3(-0.42f, 0.55f, 0.72f).normalized;
            var red = lit ? new Color(1f, 0.2f, 0.14f) : new Color(0.72f, 0.07f, 0.05f);
            var t = Fill(NewTex(S, S, false, FilterMode.Bilinear, lit ? "DomeLit" : "Dome"), (x, y) =>
            {
                var p = P(x, y);
                var d = p - c;
                float r = d.magnitude;
                float alpha = Mathf.Clamp01(S / 2f - 1f - r + 0.5f);
                if (alpha <= 0f) return new Color(0, 0, 0, 0);
                const float dome = 100f;
                Color col;
                if (r > dome + 4f)
                {
                    // 금속 링 (홈 무늬)
                    var nd = d / Mathf.Max(1e-3f, r);
                    float v = 0.32f + 0.32f * (Vector2.Dot(nd, new Vector2(-0.6f, 0.8f)) * 0.5f + 0.5f);
                    float ang = Mathf.Atan2(d.y, d.x);
                    v += 0.05f * Mathf.Sin(ang * 96f);
                    if (r > S / 2f - 3f) v = 0.08f;
                    col = new Color(v, v, v * 0.96f);
                }
                else if (r > dome)
                {
                    col = new Color(0.03f, 0.02f, 0.02f); // 틈
                }
                else
                {
                    var n = new Vector3(d.x / dome, d.y / dome, Mathf.Sqrt(Mathf.Max(0f, 1f - (r / dome) * (r / dome))));
                    float diff = Mathf.Clamp01(Vector3.Dot(n, L));
                    col = red * (0.3f + 0.85f * diff);
                    var hp = new Vector2(n.x + 0.36f, n.y - 0.42f);
                    float spec = Mathf.Exp(-hp.sqrMagnitude / 0.03f) * (lit ? 0.9f : 0.7f);
                    col += new Color(1f, 0.9f, 0.85f) * spec;
                    if (lit) col += new Color(0.25f, 0.04f, 0.02f) * (1f - r / dome);
                }
                col.a = alpha;
                return col;
            });
            return Sp(t, 0);
        }
    }
}
