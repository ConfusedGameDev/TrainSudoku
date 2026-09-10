using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainSudoku.Game
{
    /// <summary>
    /// One stroked icon, drawn with <see cref="Painter2D"/> from SVG path data (D16, work order 7.2). Resolution
    /// independent, tintable, no atlas to rebuild, and no preview package in the manifest.
    /// </summary>
    /// <remarks>
    /// The paths are Lucide's, at a 24-unit view box and 1.5 stroke, and most are one or two segments. Colour comes
    /// from the element's resolved <c>color</c>, so an icon follows USS like text does — add
    /// <see cref="UiShell.LineTextClass"/> and the shell tints it with the active line.
    /// </remarks>
    public class Icon : VisualElement
    {
        private string[] _paths = Array.Empty<string>();
        private float _strokeWidth = 1.5f;
        private string _filledPath;

        public Icon()
        {
            generateVisualContent += Draw;
            // The stroke is described in view-box units, so the element must know how big it is to scale it.
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        protected Icon(params string[] paths) : this() => _paths = paths ?? Array.Empty<string>();

        /// <summary>Stroke weight in view-box units. Lucide draws at 1.5 on a 24 box.</summary>
        public float StrokeWidth
        {
            get => _strokeWidth;
            set { _strokeWidth = value; MarkDirtyRepaint(); }
        }

        /// <summary>A path filled rather than stroked, for the solid star of an earned rating.</summary>
        public string FilledPath
        {
            get => _filledPath;
            set { _filledPath = value; MarkDirtyRepaint(); }
        }

        protected void SetPaths(params string[] paths)
        {
            _paths = paths ?? Array.Empty<string>();
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            var painter = context.painter2D;
            var colour = resolvedStyle.color;
            var scale = Mathf.Min(rect.width, rect.height) / 24f;

            if (!string.IsNullOrEmpty(_filledPath))
            {
                painter.fillColor = colour;
                painter.BeginPath();
                VectorPath.Replay(painter, _filledPath, 24f, rect);
                painter.ClosePath();
                painter.Fill();
            }

            if (_paths.Length == 0) return;

            painter.strokeColor = colour;
            painter.lineWidth = Mathf.Max(1f, _strokeWidth * scale);
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            // Each path is stroked on its own, so an open segment cannot be joined to the next by accident.
            foreach (var path in _paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                painter.BeginPath();
                VectorPath.Replay(painter, path, 24f, rect);
                painter.Stroke();
            }
        }
    }

    /// <summary>
    /// The icons this UI uses. Path data is Lucide's, taken from the Direction 2 mockup where that drawing used one,
    /// so the shipped icon and the pitched icon are the same shape.
    /// </summary>
    public static class Icons
    {
        // ---- present in the mockup ----

        /// <summary>A locked line or station. The most-used icon in the network map.</summary>
        public const string Lock = "M4 11h16v10H4z";
        public const string LockShackle = "M8 11V7a4 4 0 0 1 8 0v4";

        public const string Star = "M12 2l3.1 6.3 6.9 1-5 4.9 1.2 6.9L12 17.8 5.8 21l1.2-6.9-5-4.9 6.9-1z";
        public const string ArrowRightShaft = "M5 12h13";
        public const string ArrowRightHead = "M13 6l6 6-6 6";
        public const string Check = "M4 13l5 5L20 7";
        public const string ChevronLeft = "M15 5 8 12l7 7";
        public const string SpeakerBody = "M11 5 6 9H2v6h4l5 4z";
        public const string SpeakerWave = "M15.5 8.5a5 5 0 0 1 0 7";
        public const string SkipTriangle = "M9 18V6l10 6z";
        public const string SkipBar = "M5 5v14";

        // ---- the screens need these, and the mockup never drew them ----

        public const string PauseLeft = "M9 5v14";
        public const string PauseRight = "M15 5v14";
        public const string CloseA = "M6 6l12 12";
        public const string CloseB = "M18 6L6 18";
        public const string ClockFace = "M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18z";
        public const string ClockHands = "M12 7v5l3 2";
        public const string RetryArc = "M20 12a8 8 0 1 1-2.3-5.6";
        public const string RetryHead = "M20 4v5h-5";

        public static Icon Lucide(params string[] paths) => new Icon2(paths);

        /// <summary>A locked padlock: a filled-looking body plus its shackle.</summary>
        public static Icon Padlock() => Lucide(Lock, LockShackle);

        /// <summary>A star. <paramref name="earned"/> fills it; an unearned one is drawn as an outline.</summary>
        public static Icon StarIcon(bool earned)
        {
            var icon = earned ? Lucide() : Lucide(Star);
            if (earned) icon.FilledPath = Star;
            return icon;
        }

        public static Icon ArrowRight() => Lucide(ArrowRightShaft, ArrowRightHead);
        public static Icon Tick() => Lucide(Check);
        public static Icon Back() => Lucide(ChevronLeft);
        public static Icon Speaker() => Lucide(SpeakerBody, SpeakerWave);
        public static Icon Skip() => Lucide(SkipBar, SkipTriangle);
        public static Icon Pause() => Lucide(PauseLeft, PauseRight);
        public static Icon Close() => Lucide(CloseA, CloseB);
        public static Icon Clock() => Lucide(ClockFace, ClockHands);
        public static Icon Retry() => Lucide(RetryArc, RetryHead);

        /// <summary>The line glyph beside a signage header: a ring with a single station marked on it.</summary>
        public static Icon LineRing() => Lucide("M12 3a9 9 0 1 1 0 18 9 9 0 0 1 0-18z", "M12 1a2.2 2.2 0 1 1 0 4.4 2.2 2.2 0 0 1 0-4.4z");

        /// <summary>Concrete <see cref="Icon"/> so the base can stay open for icons with their own drawing.</summary>
        private sealed class Icon2 : Icon
        {
            public Icon2(string[] paths) : base(paths) { }
        }
    }
}
