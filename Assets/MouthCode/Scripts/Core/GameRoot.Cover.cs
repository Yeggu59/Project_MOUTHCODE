using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MouthCode
{
    /// <summary>
    /// 스토어(itch.io)용 표지 그림. 게임 UI 재질 그대로 한 장면을 조립한다.
    /// 자동 테스트 인자 -mouthcodeCover 로 띄워 스크린샷을 찍는다 (630x500 비율, 예: 1260x1000 창).
    /// </summary>
    public partial class GameRoot
    {
        void ShowCoverForTest() => Run(CoverRoutine());

        IEnumerator CoverRoutine()
        {
            onTitle = true;
            var root = NewScreen("Cover");

            // ── 기밀 서류 ──
            var doc = UIKit.Paper(root, 6, 56, TextAnchor.UpperLeft);
            doc.childForceExpandWidth = false;
            var docRt = (RectTransform)doc.transform;
            UIKit.Anchor(docRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-40, 190), new Vector2(1560, 980));
            UIKit.Label(doc.transform, "TOP SECRET   ·   국방 통신 사령부   ·   문서번호 MC-2611-07", 36, UIKit.PDim, TextAnchor.MiddleLeft);
            UIKit.Title(doc.transform, "마우스코드", 236, UIKit.PInk, TextAnchor.MiddleLeft);
            UIKit.Title(doc.transform, "M O U T H   C O D E", 104, UIKit.PRed, TextAnchor.MiddleLeft);
            UIKit.Spacer(doc.transform, 10, 6);
            UIKit.Title(doc.transform, "READ THE CIPHER. OUT LOUD.", 56, UIKit.PInk, TextAnchor.MiddleLeft);
            UIKit.Spacer(doc.transform, 10, 26);

            // 암호 글자 카드
            var cards = UIKit.HBox(doc.transform, 34, TextAnchor.MiddleLeft);
            float[] tilt = { -5f, 3f, -2f, 6f };
            string[] sounds = { "뿌", "르", "딱", "깡" };
            for (int i = 0; i < sounds.Length; i++)
            {
                var g = content.glyphs.Find(x => x.sound == sounds[i]) ?? content.glyphs[i % content.glyphs.Count];
                var card = UIKit.Card(cards.transform, 0, 12, TextAnchor.MiddleCenter, true, tilt[i] * 1.5f);
                UIKit.Size(card, 200, 248);
                UIKit.Icon(card.transform, GlyphArt.Get(g, true), 150);
                UIKit.Size(UIKit.Label(card.transform, g.sound, 48, UIKit.PInk, TextAnchor.MiddleCenter, FontStyle.Bold), 180, 62);
                card.transform.localRotation = Quaternion.Euler(0, 0, tilt[i]);
            }

            var (stamp, _) = UIKit.Stamp(docRt, "1급 기밀\n<size=34>TOP SECRET</size>", 84, UIKit.PRed, 11f, true);
            stamp.anchorMin = stamp.anchorMax = new Vector2(1, 1);
            stamp.pivot = new Vector2(0.5f, 0.5f);
            stamp.anchoredPosition = new Vector2(-260, -250);

            // ── 사령관 사진 ──
            var face = UIKit.Polaroid(root, HqPortrait.Open, "본부 사령관", 300, 7f, 36);
            var faceCard = (RectTransform)face.transform.parent.parent;
            UIKit.Anchor(faceCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(660, -150), new Vector2(324, 372));

            // ── 무전기 조작부 ──
            var dev = UIKit.Device(root, 0, 26, TextAnchor.MiddleLeft);
            UIKit.Anchor((RectTransform)dev.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -585), new Vector2(1760, 330));
            var row = UIKit.HBox(dev.transform, 40, TextAnchor.MiddleLeft);

            var pttBox = UIKit.Rect("PTT", row.transform);
            UIKit.Size(pttBox, 250, 250);
            var glow = UIKit.Box(pttBox, new Color(1f, 0.25f, 0.15f, 0.7f), "Glow");
            glow.sprite = UISkin.Glow;
            UIKit.Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 460));
            var dome = UIKit.Box(pttBox, Color.white, "Dome");
            dome.sprite = UISkin.DomeLit;
            UIKit.Stretch(dome.rectTransform);
            UIKit.Stretch(UIKit.Title(dome.transform, "ON AIR", 50, new Color(1f, 0.95f, 0.9f, 0.9f)).rectTransform);

            var crt = UIKit.Crt(row.transform, 10, 26, TextAnchor.MiddleLeft);
            UIKit.Size(crt, 1340, 260);
            UIKit.Label(crt.transform, "<color=#ff6655>●</color> 송신 중…   또박또박, 크게", 48, UIKit.Ink, TextAnchor.MiddleLeft);
            var wave = Track(WaveTexture(640, 96));
            var waveRt = UIKit.Rect("Wave", crt.transform);
            var waveRaw = waveRt.gameObject.AddComponent<RawImage>();
            waveRaw.texture = wave;
            waveRaw.raycastTarget = false;
            UIKit.Size(waveRaw, 1280, 130);

            // 표지는 정지 화면. 빛만 살짝 숨쉬게 둔다
            float t = 0;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                if (glow != null) glow.color = new Color(1f, 0.25f, 0.15f, 0.6f + 0.15f * Mathf.Sin(t * 4f));
                yield return null;
            }
        }

        /// <summary>표지용 가짜 음성 파형 (녹색 선).</summary>
        static Texture2D WaveTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            var rng = new System.Random(7);
            float prevY = h / 2f;
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                // 음절 세 덩어리처럼 보이는 포락선
                float env = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI * 3.2f + 0.3f)) * (0.55f + 0.45f * Mathf.Sin(u * 9f));
                float v = env * (Mathf.Sin(u * 180f) * 0.6f + Mathf.Sin(u * 77f + 1f) * 0.3f + ((float)rng.NextDouble() - 0.5f) * 0.3f);
                float y = h / 2f + v * h * 0.42f;
                int y0 = Mathf.FloorToInt(Mathf.Min(prevY, y)) - 1, y1 = Mathf.CeilToInt(Mathf.Max(prevY, y)) + 1;
                for (int yy = Mathf.Max(0, y0); yy <= Mathf.Min(h - 1, y1); yy++)
                    px[yy * w + x] = new Color(0.45f, 1f, 0.5f, 1f);
                if (x % 40 == 0)
                    for (int yy = 0; yy < h; yy++) if (px[yy * w + x].a == 0) px[yy * w + x] = new Color(0.45f, 1f, 0.5f, 0.12f);
                prevY = y;
            }
            for (int x = 0; x < w; x++) if (px[(h / 2) * w + x].a == 0) px[(h / 2) * w + x] = new Color(0.45f, 1f, 0.5f, 0.25f);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
