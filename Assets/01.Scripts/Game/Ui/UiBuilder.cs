using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TrainSudoku.Game
{
    /// <summary>
    /// Builds the uGUI hierarchy in code so the screens need no scene or prefab authoring. Portrait reference layout
    /// of 1080x1920; the canvas scaler keeps it readable on PC windows of any shape.
    /// </summary>
    public static class UiBuilder
    {
        public static readonly Color Background = new Color(0.09f, 0.11f, 0.15f);
        public static readonly Color Card = new Color(0.16f, 0.19f, 0.25f, 0.98f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f);
        public static readonly Color Accent = new Color(0.95f, 0.58f, 0.18f);
        public static readonly Color AccentPressed = new Color(0.80f, 0.45f, 0.10f);
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

        /// <summary>Screen-space overlay canvas plus an EventSystem driven by the Input System.</summary>
        public static Canvas CreateCanvas(string name, InputActionAsset actions)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
                var module = eventSystem.AddComponent<InputSystemUIInputModule>();
                if (actions != null) module.actionsAsset = actions;
            }

            return canvas;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

        public static Button Button(Transform parent, string label, UnityAction onClick, AudioCue cue = AudioCue.UiClick, float height = 120f, bool primary = true)
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
            button.onClick.AddListener(() =>
            {
                AudioCuePlayer.Play(cue);
                onClick?.Invoke();
            });

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            var text = Label(rect, "Label", label, Mathf.RoundToInt(height * 0.36f), TextColor, TextAnchor.MiddleCenter, height);
            Stretch((RectTransform)text.transform);
            text.fontStyle = FontStyle.Bold;

            return button;
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

        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
