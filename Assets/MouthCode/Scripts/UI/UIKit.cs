using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MouthCode
{
    /// <summary>
    /// 코드로 uGUI를 조립하는 도우미. 테마 색·폰트·재질도 여기서 관리.
    /// 화면은 세 가지 재질로 나뉜다: 종이(기밀 문서), 금속(무전기 본체), CRT(무전기 화면).
    /// </summary>
    public static class UIKit
    {
        /// <summary>본문 픽셀체 (갈무리11)</summary>
        public static Font Font { get; private set; }
        /// <summary>굵은 픽셀체 (갈무리11 Bold)</summary>
        public static Font FontBold { get; private set; }
        /// <summary>제목·도장용 (검은고딕)</summary>
        public static Font Display { get; private set; }
        public static Sprite White { get; private set; }
        public static Sprite Round { get; private set; }
        static bool pixelFont;

        // ── CRT 화면 위 색 (녹색 인광) ──
        public static readonly Color Bg = new Color(0.035f, 0.06f, 0.045f);
        public static readonly Color Panel = new Color(0.06f, 0.11f, 0.08f, 0.95f);
        public static readonly Color PanelLine = new Color(0.25f, 0.55f, 0.35f, 0.8f);
        public static readonly Color Ink = new Color(0.78f, 1f, 0.8f);
        public static readonly Color Dim = new Color(0.45f, 0.66f, 0.5f);
        public static readonly Color Accent = new Color(1f, 0.84f, 0.3f);
        public static readonly Color Danger = new Color(1f, 0.38f, 0.32f);
        public static readonly Color Good = new Color(0.45f, 1f, 0.5f);
        public static readonly Color ButtonBg = new Color(0.1f, 0.2f, 0.13f);

        // ── 종이 위 색 ──
        public static readonly Color PaperCol = new Color(0.9f, 0.86f, 0.74f);
        public static readonly Color CardCol = new Color(0.97f, 0.95f, 0.88f);
        public static readonly Color PInk = new Color(0.15f, 0.14f, 0.12f);
        public static readonly Color PDim = new Color(0.44f, 0.4f, 0.32f);
        public static readonly Color PRed = new Color(0.74f, 0.13f, 0.1f);
        public static readonly Color PGreen = new Color(0.12f, 0.44f, 0.19f);
        public static readonly Color PAmber = new Color(0.7f, 0.43f, 0.04f);
        public static readonly Color PBlue = new Color(0.12f, 0.22f, 0.58f);
        public static readonly Color Highlighter = new Color(1f, 0.9f, 0.2f, 0.55f);

        // ── 금속(무전기 본체) 위 색 ──
        public static readonly Color MetalTint = new Color(0.42f, 0.46f, 0.37f);
        public static readonly Color MetalText = new Color(0.9f, 0.92f, 0.82f);
        public static readonly Color MetalDim = new Color(0.66f, 0.7f, 0.6f);
        public static readonly Color KeyTint = new Color(0.3f, 0.32f, 0.28f);
        public static readonly Color DeskText = new Color(0.66f, 0.63f, 0.55f);

        public static void Init()
        {
            if (Font == null)
            {
                Font = Resources.Load<Font>("Fonts/Galmuri11");
                pixelFont = Font != null;
                if (Font == null) Font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
                FontBold = Resources.Load<Font>("Fonts/Galmuri11-Bold");
                if (FontBold == null) FontBold = Font;
                Display = Resources.Load<Font>("Fonts/BlackHanSans-Regular");
                if (Display == null) Display = FontBold;
                Font.textureRebuilt += OnFontTexture;
                OnFontTexture(Font);
                OnFontTexture(FontBold);
            }
            if (White == null)
                White = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            if (Round == null) Round = MakeRound();
            UISkin.Init();
        }

        /// <summary>픽셀체는 글자 텍스처를 선명하게(Point) 샘플링한다.</summary>
        static void OnFontTexture(Font f)
        {
            if (!pixelFont || f == null || (f != Font && f != FontBold)) return;
            var tex = f.material != null ? f.material.mainTexture : null;
            if (tex != null) tex.filterMode = FilterMode.Point;
        }

        /// <summary>픽셀체는 12px 격자의 배수일 때 가장 또렷하다.</summary>
        public static int PixelSize(int size)
        {
            if (!pixelFont || size < 12) return size;
            return Mathf.Max(12, Mathf.FloorToInt((size + 5) / 12f) * 12);
        }

        static Sprite MakeRound()
        {
            const int S = 32; const float R = 10f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R, S - R), cy = Mathf.Clamp(y + 0.5f, R, S - R);
                    float d = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude;
                    px[y * S + x] = new Color(1, 1, 1, Mathf.Clamp01(R - d + 0.5f));
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(R + 1, R + 1, R + 1, R + 1));
        }

        // ───────────────────────── 기본 ─────────────────────────

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static Image Box(Transform parent, Color c, string name = "Box", bool rounded = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = rounded ? Round : White;
            if (rounded) img.type = Image.Type.Sliced;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>레이아웃 계산에서 빠지는 배경 층 (부모를 꽉 채움).</summary>
        public static Image Layer(RectTransform parent, Sprite s, Color c, bool sliced, string name = "Layer")
        {
            var img = Box(parent, c, name);
            img.sprite = s;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            Ignore(img);
            Stretch(img.rectTransform);
            return img;
        }

        /// <summary>반복 무늬 배경 층.</summary>
        public static RawImage LayerTile(RectTransform parent, Texture tex, float tileW, float tileH, Color c, string name = "Tile")
        {
            var rt = Rect(name, parent);
            var r = rt.gameObject.AddComponent<RawImage>();
            r.texture = tex;
            r.color = c;
            r.raycastTarget = false;
            Ignore(r);
            Stretch(rt);
            var tile = rt.gameObject.AddComponent<TileUV>();
            tile.tileW = tileW; tile.tileH = tileH;
            tile.Apply();
            return r;
        }

        public static LayoutElement Ignore(Component c)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            return le;
        }

        static VerticalLayoutGroup AddVLG(GameObject go, float spacing, int pad, TextAnchor align)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        // ───────────────────────── 재질 패널 ─────────────────────────

        /// <summary>기밀 문서 종이 (그림자·종이결·누렇게 바랜 가장자리) + 안쪽 세로 레이아웃.</summary>
        public static VerticalLayoutGroup Paper(Transform parent, float spacing, int pad, TextAnchor align = TextAnchor.UpperCenter, Color? tint = null, bool shadow = true)
        {
            var rt = Rect("Paper", parent);
            if (shadow) Stretch(Layer(rt, UISkin.Shadow, new Color(0, 0, 0, 0.62f), true, "Shadow").rectTransform, -18, -10, -18, -36);
            Layer(rt, White, tint ?? PaperCol, false, "Sheet");
            LayerTile(rt, UISkin.Grain, 256, 256, Color.white, "Grain");
            Layer(rt, UISkin.PaperEdge, Color.white, true, "Edge");
            return AddVLG(rt.gameObject, spacing, pad, align);
        }

        /// <summary>테이프로 붙인 작은 카드 (암호 글자 카드·사진 등).</summary>
        public static VerticalLayoutGroup Card(Transform parent, float spacing, int pad, TextAnchor align = TextAnchor.MiddleCenter, bool tape = true, float tapeAngle = 0f)
        {
            var rt = Rect("Card", parent);
            Stretch(Layer(rt, UISkin.Shadow, new Color(0, 0, 0, 0.42f), true, "Shadow").rectTransform, -10, -4, -10, -16);
            Layer(rt, White, CardCol, false, "Sheet");
            LayerTile(rt, UISkin.Grain, 256, 256, new Color(1, 1, 1, 0.6f), "Grain");
            Layer(rt, UISkin.Border, new Color(PInk.r, PInk.g, PInk.b, 0.12f), true, "Edge");
            if (tape) Tape(rt, new Vector2(0.5f, 1f), new Vector2(0, 2), 84, tapeAngle);
            Size(rt, -1, -1, 0, 0); // 가로 레이아웃 안에서 늘어나지 않게
            return AddVLG(rt.gameObject, spacing, pad, align);
        }

        /// <summary>마스킹 테이프 한 조각.</summary>
        public static Image Tape(RectTransform parent, Vector2 anchor, Vector2 pos, float width, float angle)
        {
            var img = Box(parent, Color.white, "Tape");
            img.sprite = UISkin.Tape;
            Ignore(img);
            Anchor(img.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(width, width * 0.32f));
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            return img;
        }

        /// <summary>무전기 본체 금속판 (나사·빛 반사·결).</summary>
        public static VerticalLayoutGroup Device(Transform parent, float spacing, int pad, TextAnchor align = TextAnchor.UpperCenter)
        {
            var rt = Rect("Device", parent);
            Stretch(Layer(rt, UISkin.Shadow, new Color(0, 0, 0, 0.7f), true, "Shadow").rectTransform, -16, -8, -16, -30);
            Layer(rt, UISkin.Metal, MetalTint, true, "Metal");
            var brushed = LayerTile(rt, UISkin.Brushed, 256, 64, Color.white, "Brushed");
            Stretch(brushed.rectTransform, 8, 8, 8, 8);
            return AddVLG(rt.gameObject, spacing, pad, align);
        }

        /// <summary>CRT 화면 (유리 테두리 + 주사선 + 반사광이 내용 위에 덮임).</summary>
        public static VerticalLayoutGroup Crt(Transform parent, float spacing, int pad, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Rect("Crt", parent);
            Layer(rt, UISkin.Crt, Color.white, true, "Glass");
            var over = Rect("CrtOverlay", rt);
            Ignore(over);
            Stretch(over, 5, 5, 5, 5);
            over.gameObject.AddComponent<KeepLast>();
            LayerTile(over, UISkin.Scan, 2, 3, Color.white, "Scan");
            var glare = over.gameObject.AddComponent<RawImage>();
            glare.texture = UISkin.Glare;
            glare.raycastTarget = false;
            return AddVLG(rt.gameObject, spacing, pad, align);
        }

        /// <summary>예전 CRT 스타일 패널 (테두리 + 안쪽 세로 레이아웃).</summary>
        public static VerticalLayoutGroup PanelBox(Transform parent, float spacing, int pad, TextAnchor align = TextAnchor.UpperCenter)
        {
            var img = Box(parent, Panel, "Panel", true);
            var ol = img.gameObject.AddComponent<Outline>();
            ol.effectColor = PanelLine; ol.effectDistance = new Vector2(2, -2);
            return AddVLG(img.gameObject, spacing, pad, align);
        }

        /// <summary>잉크 선 테두리 상자 (문서 안의 칸).</summary>
        public static VerticalLayoutGroup InkBox(Transform parent, float spacing, int pad, TextAnchor align = TextAnchor.UpperLeft, Color? line = null)
        {
            var rt = Rect("InkBox", parent);
            Layer(rt, UISkin.Border, line ?? new Color(PInk.r, PInk.g, PInk.b, 0.7f), true, "Border");
            return AddVLG(rt.gameObject, spacing, pad, align);
        }

        // ───────────────────────── 글자 ─────────────────────────

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            bool bold = style == FontStyle.Bold || style == FontStyle.BoldAndItalic;
            t.font = bold ? FontBold : Font;
            // 픽셀체는 가짜 굵기·기울임을 쓰지 않는다 (굵게는 Bold 폰트 파일로)
            t.fontStyle = pixelFont ? FontStyle.Normal : style;
            t.text = text; t.fontSize = PixelSize(size); t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.lineSpacing = 1.1f;
            return t;
        }

        /// <summary>제목용 굵은 글씨 (검은고딕).</summary>
        public static Text Title(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var t = Label(parent, text, size, color, anchor);
            t.font = Display;
            t.fontSize = size;
            t.fontStyle = FontStyle.Normal;
            t.lineSpacing = 1f;
            return t;
        }

        // ───────────────────────── 레이아웃 ─────────────────────────

        public static VerticalLayoutGroup VBox(Transform parent, float spacing, TextAnchor align, bool expandWidth = true)
        {
            var rt = Rect("VBox", parent);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth; v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HBox(Transform parent, float spacing, TextAnchor align)
        {
            var rt = Rect("HBox", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        public static GridLayoutGroup Grid(Transform parent, Vector2 cell, Vector2 spacing, int columns)
        {
            var rt = Rect("Grid", parent);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell; g.spacing = spacing;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = Mathf.Max(1, columns);
            g.childAlignment = TextAnchor.UpperCenter;
            return g;
        }

        public static LayoutElement Size(Component c, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            // 크기를 고정하면 늘어나지도 않게 한다 (레이아웃 그룹이 flexible=1을 보고하는 경우 방지)
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; le.flexibleWidth = 0; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; le.flexibleHeight = 0; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static RectTransform Spacer(Transform parent, float w, float h)
        {
            var rt = Rect("Spacer", parent);
            Size(rt, w, h);
            return rt;
        }

        /// <summary>가로 레이아웃에서 남는 폭을 차지하는 빈칸.</summary>
        public static RectTransform Flex(Transform parent)
        {
            var rt = Rect("Flex", parent);
            Size(rt, 0, -1, 1);
            return rt;
        }

        /// <summary>문서용 가로줄 (실선 또는 점선).</summary>
        public static Graphic Rule(Transform parent, Color c, float thickness = 2f, bool dashed = false)
        {
            if (!dashed)
            {
                var img = Box(parent, c, "Rule");
                Size(img, -1, thickness, 1);
                return img;
            }
            var rt = Rect("Dash", parent);
            var r = rt.gameObject.AddComponent<RawImage>();
            r.texture = UISkin.Dash; r.color = c; r.raycastTarget = false;
            Size(r, -1, thickness, 1);
            var tile = rt.gameObject.AddComponent<TileUV>();
            tile.tileW = 16; tile.tileH = thickness;
            return r;
        }

        // ───────────────────────── 버튼 ─────────────────────────

        static Button Clickable(GameObject go, Action onClick)
        {
            var b = go.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            b.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                UiSound.Click();
                onClick?.Invoke();
            });
            return b;
        }

        /// <summary>문서 위 잉크 버튼. primary는 검게 칠해져 있고, 나머지는 선만 있다가 올리면 칠해진다.</summary>
        public static Button InkButton(Transform parent, string label, Action onClick, float w = 360, float h = 72, int fontSize = 24, bool primary = false)
        {
            var rt = Rect("InkButton", parent);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Round; bg.type = Image.Type.Sliced; bg.raycastTarget = true;
            var border = Layer(rt, UISkin.Border, PInk, true, "Border");
            var t = Label(rt, label, fontSize, primary ? PaperCol : PInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 12, 4, 12, 4);
            var fx = rt.gameObject.AddComponent<ButtonFx>();
            fx.bg = bg; fx.label = t; fx.content = t.rectTransform;
            var clear = new Color(PInk.r, PInk.g, PInk.b, 0f);
            fx.bgNormal = primary ? PInk : clear;
            fx.bgHover = primary ? PRed : PInk;
            fx.bgDown = primary ? PRed * 0.8f : PInk * 0.8f;
            fx.textNormal = primary ? PaperCol : PInk;
            fx.textHover = PaperCol;
            fx.Apply();
            var b = Clickable(rt.gameObject, onClick);
            Size(rt, w, h);
            border.color = primary ? PInk : new Color(PInk.r, PInk.g, PInk.b, 0.85f);
            return b;
        }

        /// <summary>무전기 본체의 고무 키 버튼.</summary>
        public static Button KeyButton(Transform parent, string label, Action onClick, float w = 240, float h = 64, int fontSize = 24)
        {
            var rt = Rect("KeyButton", parent);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UISkin.KeyCap; bg.type = Image.Type.Sliced; bg.raycastTarget = true;
            var t = Label(rt, label, fontSize, MetalText, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 10, 2, 10, 6);
            var fx = rt.gameObject.AddComponent<ButtonFx>();
            fx.bg = bg; fx.label = t; fx.content = t.rectTransform;
            fx.bgNormal = KeyTint;
            fx.bgHover = new Color(0.42f, 0.46f, 0.38f);
            fx.bgDown = new Color(0.22f, 0.24f, 0.2f);
            fx.textNormal = MetalText;
            fx.textHover = Color.white;
            fx.Apply();
            var b = Clickable(rt.gameObject, onClick);
            Size(rt, w, h);
            return b;
        }

        /// <summary>CRT 화면 안의 버튼 (녹색 선, 올리면 반전).</summary>
        public static Button CrtButton(Transform parent, string label, Action onClick, float w = 240, float h = 60, int fontSize = 24)
        {
            var rt = Rect("CrtButton", parent);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Round; bg.type = Image.Type.Sliced; bg.raycastTarget = true;
            Layer(rt, UISkin.Border, Ink, true, "Border");
            var t = Label(rt, label, fontSize, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 10, 2, 10, 2);
            var fx = rt.gameObject.AddComponent<ButtonFx>();
            fx.bg = bg; fx.label = t; fx.content = t.rectTransform;
            fx.bgNormal = new Color(Ink.r, Ink.g, Ink.b, 0.06f);
            fx.bgHover = Ink;
            fx.bgDown = Ink * 0.75f;
            fx.textNormal = Ink;
            fx.textHover = new Color(0.02f, 0.08f, 0.04f);
            fx.Apply();
            var b = Clickable(rt.gameObject, onClick);
            Size(rt, w, h);
            return b;
        }

        /// <summary>호환용: 문서 잉크 버튼.</summary>
        public static Button Button(Transform parent, string label, Action onClick, float w = 360, float h = 76, int fontSize = 30)
            => InkButton(parent, label, onClick, w, h, fontSize);

        // ───────────────────────── 장식·계기 ─────────────────────────

        /// <summary>빨간 고무도장. floating이면 레이아웃 밖에서 글자 크기에 맞춰 스스로 크기를 잡는다.</summary>
        public static (RectTransform rt, Text text) Stamp(Transform parent, string text, int size, Color color, float angle, bool floating = false)
        {
            var rt = Rect("Stamp", parent);
            var frame = rt.gameObject.AddComponent<Image>();
            frame.sprite = UISkin.StampFrame; frame.type = Image.Type.Sliced;
            frame.color = new Color(color.r, color.g, color.b, 0.88f);
            frame.raycastTarget = false;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            int padX = Mathf.RoundToInt(size * 0.55f + 10), padY = Mathf.RoundToInt(size * 0.2f + 8);
            h.padding = new RectOffset(padX, padX, padY, padY);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var t = Title(rt, text, size, new Color(color.r, color.g, color.b, 0.9f));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.lineSpacing = 0.8f;
            if (floating)
            {
                Ignore(rt);
                var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            return (rt, t);
        }

        /// <summary>금속판에 새긴 이름표.</summary>
        public static Text Plate(Transform parent, string text, int fontSize = 24)
        {
            var rt = Rect("Plate", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UISkin.KeyCap; img.type = Image.Type.Sliced;
            img.color = new Color(0.72f, 0.73f, 0.64f);
            img.raycastTarget = false;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(18, 18, 6, 10);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return Label(rt, text, fontSize, new Color(0.12f, 0.13f, 0.11f), TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        /// <summary>노랑·검정 경고 줄무늬 띠.</summary>
        public static RawImage HazardStrip(RectTransform parent, float height, bool top)
        {
            var r = LayerTile(parent, UISkin.Hazard, height * 1.4f, height * 1.4f, Color.white, "Hazard");
            var rt = r.rectTransform;
            rt.anchorMin = new Vector2(0, top ? 1 : 0); rt.anchorMax = new Vector2(1, top ? 1 : 0);
            rt.pivot = new Vector2(0.5f, top ? 1 : 0);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = Vector2.zero;
            return r;
        }

        /// <summary>테이프로 붙인 사진 (본부 사령관 초상 등). 사진 Image를 돌려준다.</summary>
        public static Image Polaroid(Transform parent, Sprite photo, string caption, float size, float angle, int captionSize = 24)
        {
            var card = Card(parent, 6, 12, TextAnchor.UpperCenter, true, -angle * 1.5f);
            card.padding = new RectOffset(12, 12, 12, 8);
            var frame = Box(card.transform, new Color(0.03f, 0.08f, 0.05f), "Photo");
            Size(frame, size, size);
            var img = Box(frame.transform, Color.white, "Face");
            img.sprite = photo;
            img.preserveAspect = true;
            Stretch(img.rectTransform, 4, 4, 4, 4);
            if (!string.IsNullOrEmpty(caption))
            {
                var c = Label(card.transform, caption, captionSize, PInk, TextAnchor.MiddleCenter);
                Size(c, size, captionSize + 8);
            }
            card.transform.localRotation = Quaternion.Euler(0, 0, angle);
            return img;
        }

        public static Image Icon(Transform parent, Sprite s, float size)
        {
            var img = Box(parent, Color.white, "Icon");
            img.sprite = s;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            Size(img, size, size);
            return img;
        }

        /// <summary>화면 그림 (스펙트로그램 등) — 어두운 화면 틀 안에 넣는다.</summary>
        public static RawImage Raw(Transform parent, Texture tex, float w, float h)
        {
            var rt = Rect("Raw", parent);
            var frame = rt.gameObject.AddComponent<Image>();
            frame.sprite = UISkin.Crt; frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            Size(rt, w, h);
            var inner = Rect("Tex", rt);
            Stretch(inner, 6, 6, 6, 6);
            var r = inner.gameObject.AddComponent<RawImage>();
            r.texture = tex;
            r.raycastTarget = false;
            return r;
        }

        public static Image Bar(Transform parent, Color back, Color fill, float w, float h, out Image fillImg)
        {
            var bg = Box(parent, back, "Bar");
            Size(bg, w, h);
            fillImg = Box(bg.transform, fill, "Fill");
            Stretch(fillImg.rectTransform);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;
            return bg;
        }

        /// <summary>종이 위 서명란 (밑줄 + 파란 볼펜 글씨).</summary>
        public static InputField PaperInput(Transform parent, string initial, float w, float h, int fontSize = 36, string placeholder = "이름을 적어라")
        {
            var rt = Rect("Input", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = White;
            img.color = new Color(1f, 0.96f, 0.6f, 0.18f);
            img.raycastTarget = true;
            Size(rt, w, h);
            var line = Box(rt, PInk, "Underline");
            line.rectTransform.anchorMin = new Vector2(0, 0); line.rectTransform.anchorMax = new Vector2(1, 0);
            line.rectTransform.pivot = new Vector2(0.5f, 0); line.rectTransform.sizeDelta = new Vector2(0, 3);
            var text = Label(rt, "", fontSize, PBlue, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 16, 4, 16, 8);
            text.supportRichText = false;
            var ph = Label(rt, placeholder, fontSize, new Color(PDim.r, PDim.g, PDim.b, 0.6f), TextAnchor.MiddleCenter);
            Stretch(ph.rectTransform, 16, 4, 16, 8);
            var field = rt.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = 12;
            field.text = initial;
            field.caretColor = PRed;
            field.customCaretColor = true;
            field.caretWidth = 3;
            field.selectionColor = Highlighter;
            field.transition = Selectable.Transition.None;
            return field;
        }

        /// <summary>호환용 입력칸.</summary>
        public static InputField Input(Transform parent, string initial, float w, float h, int fontSize = 34)
            => PaperInput(parent, initial, w, h, fontSize);
    }

    /// <summary>마우스로 누르고 있는 동안 Held = true (화면 무전 버튼).</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool Held { get; private set; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Held = true; }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Held = false; }
        void OnDisable() { Held = false; }
    }

    /// <summary>버튼 마우스 반응 (색 바꿈 + 눌리면 글자가 살짝 내려감).</summary>
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Graphic bg;
        public Text label;
        public RectTransform content;
        public Color bgNormal, bgHover, bgDown, textNormal, textHover;
        bool hover, down;

        public void OnPointerEnter(PointerEventData e) { hover = true; Apply(); UiSound.Hover(); }
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; Apply(); }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { down = true; Apply(); } }
        public void OnPointerUp(PointerEventData e) { down = false; Apply(); }
        void OnDisable() { hover = down = false; Apply(); }

        public void Apply()
        {
            if (bg != null) bg.color = down ? bgDown : hover ? bgHover : bgNormal;
            if (label != null) label.color = hover || down ? textHover : textNormal;
            if (content != null)
            {
                var p = content.anchoredPosition;
                content.anchoredPosition = new Vector2(p.x, down ? -3f : 0f);
            }
        }
    }

    /// <summary>RawImage의 반복 무늬를 크기에 맞춰 깐다.</summary>
    [RequireComponent(typeof(RawImage))]
    public class TileUV : UIBehaviour
    {
        public float tileW = 256, tileH = 256;

        protected override void OnEnable() { base.OnEnable(); Apply(); }
        protected override void OnRectTransformDimensionsChange() { Apply(); }

        public void Apply()
        {
            var r = GetComponent<RawImage>();
            if (r == null) return;
            var s = ((RectTransform)transform).rect.size;
            r.uvRect = new Rect(0, 0, Mathf.Max(0.01f, s.x / tileW), Mathf.Max(0.01f, s.y / tileH));
        }
    }

    /// <summary>등장 연출: 아래에서 살짝 밀려 올라오며 나타난다 (책상에 서류를 내려놓는 느낌).</summary>
    public class SlideIn : MonoBehaviour
    {
        public float duration = 0.22f;
        public float distance = 70f;
        RectTransform rt;
        CanvasGroup group;
        Vector2 basePos;
        float t;

        void Start()
        {
            rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
            group = gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            Apply(0f);
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            Apply(1f - (1f - k) * (1f - k) * (1f - k));
            if (k >= 1f) Destroy(this);
        }

        void Apply(float k)
        {
            rt.anchoredPosition = basePos + new Vector2(0f, -distance * (1f - k));
            group.alpha = k;
        }
    }

    /// <summary>형제 중 맨 위(마지막)에 그려지게 유지 (CRT 주사선 덮개용).</summary>
    public class KeepLast : MonoBehaviour
    {
        void LateUpdate()
        {
            var p = transform.parent;
            if (p != null && transform.GetSiblingIndex() != p.childCount - 1) transform.SetAsLastSibling();
        }
    }

    /// <summary>칸으로 나뉜 LED 막대 (타이머·음량계).</summary>
    public class SegBar : MonoBehaviour
    {
        Image[] segs;
        public Color offColor = new Color(1f, 1f, 1f, 0.07f);

        public static SegBar Create(Transform parent, int count, float w, float h, float gap, Color off)
        {
            var hb = UIKit.HBox(parent, gap, TextAnchor.MiddleLeft);
            UIKit.Size(hb, w, h);
            var bar = hb.gameObject.AddComponent<SegBar>();
            bar.offColor = off;
            bar.segs = new Image[count];
            float segW = (w - gap * (count - 1)) / count;
            for (int i = 0; i < count; i++)
            {
                bar.segs[i] = UIKit.Box(hb.transform, off, "Seg");
                UIKit.Size(bar.segs[i], segW, h);
            }
            return bar;
        }

        public void Set(float k, Func<float, Color> colorAt)
        {
            int n = segs.Length;
            int lit = Mathf.Clamp(Mathf.CeilToInt(k * n - 0.001f), 0, n);
            for (int i = 0; i < n; i++)
                segs[i].color = i < lit ? colorAt(n > 1 ? i / (float)(n - 1) : 1f) : offColor;
        }
    }

    /// <summary>무전기 LED (어두운 소켓 + 번지는 빛 + 밝은 심).</summary>
    public class Led : MonoBehaviour
    {
        Image glow, core;

        public static Led Create(Transform parent, float size, Color color, bool on = true)
        {
            var rt = UIKit.Rect("Led", parent);
            UIKit.Size(rt, size, size);
            var socket = UIKit.Box(rt, new Color(0.04f, 0.04f, 0.035f), "Socket");
            socket.sprite = UISkin.Circle;
            UIKit.Anchor(socket.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 0.8f);
            var led = rt.gameObject.AddComponent<Led>();
            led.glow = UIKit.Box(rt, color, "Glow");
            led.glow.sprite = UISkin.Glow;
            UIKit.Anchor(led.glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 2.2f);
            led.core = UIKit.Box(rt, color, "Core");
            led.core.sprite = UISkin.Circle;
            UIKit.Anchor(led.core.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 0.5f);
            led.Set(color, on ? 1f : 0f);
            return led;
        }

        public void Set(Color c, float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            glow.color = new Color(c.r, c.g, c.b, 0.75f * intensity);
            var lit = Color.Lerp(c, Color.white, 0.5f);
            var off = c * 0.22f;
            var k = Color.Lerp(off, lit, intensity);
            k.a = 1f;
            core.color = k;
        }
    }
}
