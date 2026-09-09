using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Builds the uGUI hierarchy in code. The <see cref="GameManager"/> runs it once in the Editor ("Generate Scene
    /// Objects") and the result is saved in the scene; at runtime it only runs when the scene has not been generated.
    /// Widgets are created here and referenced by serialized fields; click handlers are attached later by each panel's
    /// <c>Wire</c> step, because lambdas do not survive serialization. Portrait reference layout of 1080x1920.
    /// </summary>
    public static class UiBuilder
    {
        public static readonly Color Background = new Color(0.09f, 0.11f, 0.15f);
        public static readonly Color Card = new Color(0.16f, 0.19f, 0.25f, 0.98f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f);
        public static readonly Color Accent = new Color(0.95f, 0.58f, 0.18f);
        public static readonly Color Secondary = new Color(0.30f, 0.36f, 0.46f);
        public static readonly Color TextColor = new Color(0.96f, 0.96f, 0.97f);
        public static readonly Color Muted = new Color(0.66f, 0.69f, 0.75f);
        public static readonly Color Success = new Color(0.35f, 0.80f, 0.45f);

        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>Screen-space overlay canvas scaled from the portrait reference resolution.</summary>
        public static Canvas CreateCanvas(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>The scene's EventSystem driven by the Input System, created under <paramref name="parent"/> if none exists.</summary>
        public static EventSystem EnsureEventSystem(InputActionAsset actions, Transform parent)
        {
            var existing = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(parent, false);
            var module = go.AddComponent<InputSystemUIInputModule>();
            if (actions != null) module.actionsAsset = actions;
            return go.GetComponent<EventSystem>();
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Fixed-size element pinned to one anchor point; <paramref name="offset"/> moves it inwards from that corner or edge.</summary>
        public static RectTransform Anchor(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Full-screen rectangle, optionally painted. Painted panels block clicks to whatever is beneath.</summary>
        public static RectTransform FullScreen(Transform parent, string name, Color? background)
        {
            var rect = Stretch(Rect(parent, name));
            if (background.HasValue)
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.color = background.Value;
            }

            return rect;
        }

        public static RectTransform Column(Transform parent, string name, float spacing, RectOffset padding, TextAnchor alignment)
        {
            var rect = Rect(parent, name);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        /// <summary>Fixed-height horizontal strip. It reports no flexible height so a parent column cannot stretch it.</summary>
        public static RectTransform Row(Transform parent, string name, float spacing, RectOffset padding, float height)
        {
            var rect = Rect(parent, name);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0f;
            return rect;
        }

        /// <summary>A card centred on screen that grows with its content. Used by the overlay screens.</summary>
        public static RectTransform CenteredCard(Transform parent, string name, float width, float spacing)
        {
            var rect = Column(parent, name, spacing, new RectOffset(48, 48, 48, 48), TextAnchor.UpperCenter);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Card;
            return rect;
        }

        public static Text Label(Transform parent, string name, string text, int fontSize, Color color, TextAnchor alignment, float preferredHeight)
        {
            var rect = Rect(parent, name);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = fontSize;
            label.color = color;
            label.text = text;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
            element.flexibleHeight = 0f;
            return label;
        }

        /// <summary>An empty element that soaks up leftover space in a layout group.</summary>
        public static LayoutElement Spacer(Transform parent, float flexibleHeight = 1f, float flexibleWidth = 0f)
        {
            var element = Rect(parent, "Spacer").gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = flexibleHeight;
            element.flexibleWidth = flexibleWidth;
            return element;
        }

        /// <summary>A button with a centred label. Attach its handler later with <see cref="Wire"/>.</summary>
        public static Button Button(Transform parent, string label, float height = 120f, bool primary = true, int fontSize = 0)
        {
            var rect = Rect(parent, label);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = primary ? Accent : Secondary;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            button.colors = colors;

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0f;

            if (fontSize <= 0) fontSize = Mathf.RoundToInt(height * 0.36f);
            var text = Label(rect, "Label", label, fontSize, TextColor, TextAnchor.MiddleCenter, height);
            Stretch((RectTransform)text.transform);
            text.fontStyle = FontStyle.Bold;

            return button;
        }

        /// <summary>Runtime click handler: plays the cue, then runs the action.</summary>
        public static void Wire(Button button, AudioCue cue, UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                AudioCuePlayer.Play(cue);
                action?.Invoke();
            });
        }

        /// <summary>Vertical scroll list. Returns the content rectangle to add rows to.</summary>
        public static RectTransform ScrollList(Transform parent, string name, float spacing)
        {
            var rect = Rect(parent, name);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewport = Stretch(Rect(rect, "Viewport"));
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);

            var content = Column(viewport, "Content", spacing, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        /// <summary>Destroys a GameObject in play mode or edit mode.</summary>
        public static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
