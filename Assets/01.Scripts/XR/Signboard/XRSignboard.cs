using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEngine;
using UnityEngine.UIElements;
using static TrainSudoku.XR.XRSignageUi;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The standing signboard behind the platform's far edge (XR-PRD 6.2, 8): the masthead on the network, the line on
    /// its map, the station, clock and stars to beat in play, "Paused", and the arrival. A world-space UI Toolkit panel
    /// in the phone's station-signage language — paper card, ink type, the amber LED strip — built by XR itself (X20)
    /// from the blocks it shares with the wrist menu (<see cref="XRSignageUi"/>).
    /// </summary>
    /// <remarks>
    /// It stands on two posts and turns about the vertical to face the player's head, like the clue signs, so it reads
    /// from any edge, and leans back to the eyes. Its buttons are pressed with a fingertip through
    /// <see cref="XRPanelTouch"/> and nothing else: XRI's poke never pressed one on the headset (the XR7 headset check),
    /// and rays went altogether after the first XR8 check (<see cref="XRTouchOnly"/>).
    ///
    /// The ways back (to the line map in play, to the network on a line) moved to the wrist menu at XR8. The sign keeps
    /// RESUME on the paused card, for a return from a focus loss, when the wrist menu is closed.
    ///
    /// A world-space <see cref="UIDocument"/> that is switched off loses whatever was built into it, so every view is
    /// built afresh into the live root each time it is shown, and hiding the board switches the whole sign off.
    ///
    /// Copy is English until XR10 brings the XR String Table; the words are the phone's where the phone has them.
    /// </remarks>
    public sealed class XRSignboard : MonoBehaviour
    {
        /// <summary>The panel in UI pixels, and what the panel settings map those to: 100 pixels to a unit.</summary>
        private const float PanelWidth = 1260f;
        private const float PanelHeight = 720f;
        private const float PixelsPerUnit = 100f;

        /// <summary>
        /// The panel's width in the room at the standard 6 cm cell: 42 cm, a little narrower than a 6x6 board, with its
        /// height following (24 cm). It is sized in cells from that, so it grows and shrinks with the board when the
        /// handle scales it (X6, revised 2026-09-14), whatever size the board had when the sign was made.
        /// </summary>
        private const float WidthMetres = 0.42f;
        private const float StandardCell = 0.06f;

        /// <summary>How far back the card may lean to face the eyes, in degrees.</summary>
        private const float MaxLean = 50f;

        /// <summary>How high the panel's bottom edge stands above the surface, and how far behind the far edge, in cells.</summary>
        private const float Lift = 1.1f;
        private const float Beyond = 0.5f;
        private const float PostWidth = 0.08f;

        /// <summary>A dimmed star target: the run can no longer earn it.</summary>
        private const float SpentOpacity = 0.3f;

        /// <summary>
        /// The verdict stamp, timed as the phone's arrival (work order 9): a short wait, then down from 2.4 times its size
        /// and 22 degrees off square, under-size on impact, and a damped rattle to rest 4 degrees off square.
        /// </summary>
        private const float StampDelay = 0.3f;
        private const float StampPressSeconds = 0.26f;
        private const float StampFrom = 2.4f;
        private const float StampHit = 0.88f;
        private const float StampImpact = 0.62f;
        private const float StampShakeSeconds = 0.34f;
        private const float StampShakeDegrees = 3.2f;
        private const float StampShakeCycles = 2.5f;
        private const float StampRest = -4f;
        private const float StampEntryTilt = -22f;

        private XRSignageAssets _assets;
        private UIDocument _document;
        private Transform _panel;
        private Label _clock;
        private string _clockText;
        private readonly List<(int Stars, VisualElement Row)> _targets = new List<(int Stars, VisualElement Row)>();
        private int _tier = -1;

        /// <summary>The view's buttons, for a fingertip to press.</summary>
        private readonly XRPanelTouch _touch = new XRPanelTouch("sign", PixelsPerUnit);

        /// <summary>The verdict stamp while it is being pressed on, and when that starts; null once it rests.</summary>
        private VisualElement _stamp;
        private float _stampAt;

        public static XRSignboard Create(Transform boardRoot, XRSignageAssets assets)
        {
            var go = new GameObject("Signboard");
            go.transform.SetParent(boardRoot, false);
            var sign = go.AddComponent<XRSignboard>();
            sign._assets = assets;

            // The panel's width in cells, which is where the posts stand.
            const float cell = StandardCell;
            var widthCells = WidthMetres / cell;
            foreach (var side in new[] { -1f, 1f })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Post";
                Kill(post.GetComponent<Collider>());
                post.transform.SetParent(go.transform, false);
                post.transform.localPosition = new Vector3(side * widthCells * 0.38f, Lift / 2f, 0f);
                post.transform.localScale = new Vector3(PostWidth, Lift / 2f, PostWidth);
                post.GetComponent<MeshRenderer>().sharedMaterial = XRBoardMaterials.SignPole;
            }

            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform, false);
            sign._panel = panel.transform;
            panel.transform.localPosition = new Vector3(0f, Lift, 0f);
            // UI pixels to metres, through the board root's scale: the panel is sized in the room, not in cells.
            panel.transform.localScale = Vector3.one * (WidthMetres / (PanelWidth / PixelsPerUnit) / cell);

            sign._document = panel.AddComponent<UIDocument>();
            sign._document.panelSettings = assets != null ? assets.PanelSettings : null;
            sign._document.worldSpaceSizeMode = WorldSpaceSizeMode.Fixed;
            sign._document.worldSpaceSize = new Vector2(PanelWidth, PanelHeight);
            sign._document.pivot = Pivot.BottomCenter;

            go.SetActive(false);
            return sign;
        }

        /// <summary>Stands the sign behind a far edge <paramref name="farEdge"/> cells from the board root's origin.</summary>
        public void PlaceBehind(float farEdge) => transform.localPosition = new Vector3(0f, 0f, farEdge + Beyond);

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Turns about the vertical to face the player, so the sign reads from any edge, and leans the card back to the eyes.</summary>
        private void LateUpdate()
        {
            PressStamp();
            var head = Camera.main;
            if (head == null || transform.parent == null) return;
            var up = transform.parent.up;
            var away = Vector3.ProjectOnPlane(transform.position - head.transform.position, up);
            if (away.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.LookRotation(away, up);
            Lean(head.transform.position, up);
            _touch.Update(_panel, _document);
        }

        /// <summary>
        /// Leans the card back about its bottom edge until it faces the eyes, like a lectern; the posts stay upright.
        /// Seen from above across the platform, an upright card took rays at a glancing angle and its lowest button was
        /// hard to hit (the XR7 headset check).
        /// </summary>
        private void Lean(Vector3 eyes, Vector3 up)
        {
            if (_panel == null) return;
            // The document's pivot is its bottom centre, on the panel's origin; its height follows the board's scale.
            var height = PanelHeight / PixelsPerUnit * _panel.lossyScale.y;
            var toEyes = eyes - (_panel.position + up * (height / 2f));
            var rise = Vector3.Dot(toEyes, up);
            var run = Vector3.ProjectOnPlane(toEyes, up).magnitude;
            var lean = Mathf.Clamp(Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0f, MaxLean);
            // A positive turn about the card's own x tips its top away from the player, and its face up to them.
            _panel.localRotation = Quaternion.Euler(lean, 0f, 0f);
        }

        // ------------------------------------------------------------------ the views

        /// <summary>The network (6.2): the masthead, TSUGI over NEXT STATION beside the app's roundel.</summary>
        public void ShowMasthead(int stars)
        {
            var card = Begin();
            var row = Row(card);
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;

            var mark = new VisualElement();
            Size(mark, 230f, 230f);
            Round(mark, 115f);
            mark.style.marginRight = 44f;
            if (_assets != null && _assets.Mark != null) mark.style.backgroundImage = _assets.Mark;
            row.Add(mark);

            var words = new VisualElement();
            row.Add(words);
            // Proper nouns of the masthead: never in a string table (the phone's rule, kept).
            words.Add(Text("TSUGI", 200f, true, XRPalette.Ink, 10f));
            words.Add(Text("NEXT STATION", 60f, true, XRPalette.InkDim, 12f));

            var strip = Led(card);
            strip.Add(Text("NETWORK", 64f, true, XRPalette.Led, 6f));
            strip.Add(Spacer());
            strip.Add(StarTally(stars));
            Put(card, Text("Choose a line on the platform", 42f, false, XRPalette.InkDim)).style.marginTop = 18f;
        }

        /// <summary>One line's map (6.2): its code and name, and how far along it the player is, on the LED strip.</summary>
        public void ShowLine(LineDefinition line, int cleared, int stations, int stars)
        {
            var card = Begin();
            var row = Row(card);
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;
            row.Add(Badge(line, 180f));

            var words = new VisualElement();
            words.style.marginLeft = 40f;
            row.Add(words);
            words.Add(Text(line != null ? line.DisplayName.ToUpperInvariant() : "", 118f, true, XRPalette.Ink, 4f));
            words.Add(Text("LINE MAP", 56f, true, XRPalette.InkDim, 10f));

            var strip = Led(card);
            strip.Add(Text($"{cleared} / {stations} CLEARED", 64f, true, XRPalette.Led, 6f));
            strip.Add(Spacer());
            strip.Add(StarTally(stars));

            Put(card, Text("Choose a station on the platform", 42f, false, XRPalette.InkDim)).style.marginTop = 18f;
        }

        /// <summary>Play (6.2): the station, the clock and the stars still there to beat.</summary>
        public void ShowStation(LevelDefinition station, LineDefinition line, int stationIndex)
        {
            var card = Begin();
            StationHead(card, line, stationIndex);

            var strip = Led(card);
            strip.style.flexGrow = 1;
            strip.style.marginTop = 24f;
            strip.Add(Text(station != null ? station.DisplayName.ToUpperInvariant() : "", 92f, true, XRPalette.Led, 4f));
            strip.Add(Spacer());
            _clock = Text("00:00", 150f, true, XRPalette.Led, 4f);
            strip.Add(_clock);

            var times = station != null ? station.StarTimes : null;
            if (times == null || times.Count < 2 || times[0] <= 0) return;
            var targets = Row(card);
            targets.style.marginTop = 22f;
            targets.style.justifyContent = Justify.Center;
            for (var stars = 3; stars >= 2; stars--)
            {
                var target = Row(targets);
                target.style.alignItems = Align.Center;
                target.style.marginLeft = target.style.marginRight = 36f;
                for (var i = 0; i < stars; i++) target.Add(new XRStarGlyph(54f, XRPalette.Warn));
                Put(target, Text(ProgressTracker.FormatTime(times[3 - stars]), 60f, true, XRPalette.Ink, 2f)).style.marginLeft = 16f;
                _targets.Add((stars, target));
            }
        }

        /// <summary>Pause (6.2): the station, "Paused" and the stopped clock on the LED strip, and the way back into play.</summary>
        /// <param name="resume">
        /// Back into play. The wrist menu carries it too; the sign has it for a return from a focus loss, which comes back
        /// paused with the menu closed (6.3).
        /// </param>
        public void ShowPaused(LevelDefinition station, LineDefinition line, int stationIndex, Action resume)
        {
            var card = Begin();
            var head = StationHead(card, line, stationIndex);
            Put(head, Text(station != null ? station.DisplayName.ToUpperInvariant() : "", 60f, true, XRPalette.Ink, 3f)).style.marginLeft = 24f;
            head.Add(Spacer());
            head.Add(Button("RESUME", resume, true));

            var strip = Led(card);
            strip.style.flexGrow = 1;
            strip.style.marginTop = 24f;
            strip.Add(Text("PAUSED", 120f, true, XRPalette.Led, 12f));
            strip.Add(Spacer());
            _clock = Text("00:00", 150f, true, XRPalette.Led, 4f);
            strip.Add(_clock);

            Put(card, Text("Retry, the line map and settings are on the wrist menu", 42f, false, XRPalette.InkDim)).style.marginTop = 18f;
        }

        /// <summary>The clock, and the star targets the run has fallen out of reach of, dimmed. Cheap to call every frame.</summary>
        public void UpdateClock(double elapsed, int tier)
        {
            if (_clock == null) return;
            var text = ProgressTracker.FormatTime(elapsed);
            if (text != _clockText)
            {
                _clockText = text;
                _clock.text = text;
            }

            if (tier == _tier) return;
            _tier = tier;
            foreach (var (stars, row) in _targets) row.style.opacity = tier >= stars ? 1f : SpentOpacity;
        }

        /// <summary>The arrival (6.2): the stars, this run against the best, the verdict stamped on, and where to go next.</summary>
        public void ShowArrival(LevelDefinition station, LineDefinition line, CompletionResult result, bool lineComplete,
            Action next, Action retry, Action map)
        {
            var card = Begin();
            var head = Row(card);
            head.style.alignItems = Align.Center;
            head.Add(Badge(line, 96f));
            var words = new VisualElement();
            words.style.marginLeft = 28f;
            head.Add(words);
            words.Add(Text("ARRIVAL", 46f, true, XRPalette.InkDim, 10f));
            words.Add(Text(station != null ? station.DisplayName.ToUpperInvariant() : "", 96f, true, XRPalette.Ink, 3f));
            head.Add(Spacer());

            var stars = Row(head);
            for (var i = 0; i < 3; i++) stars.Add(new XRStarGlyph(84f, i < result.Stars ? XRPalette.Warn : XRPalette.ClosedLight));

            var strip = Led(card);
            strip.style.marginTop = 22f;
            strip.Add(Text($"THIS RUN  {ProgressTracker.FormatTime(result.Time)}", 60f, true, XRPalette.Led, 4f));
            strip.Add(Spacer());
            strip.Add(Text($"BEST  {ProgressTracker.FormatTime(result.BestTime)}", 60f, true, XRPalette.Led, 4f));

            // The verdict is stamped on beside the new-best flag, as on the phone's arrival.
            var verdict = Row(card);
            verdict.style.marginTop = 16f;
            verdict.style.alignItems = Align.Center;
            if (result.IsNewBest)
            {
                var best = Text("NEW BEST", 44f, true, XRPalette.Ink, 8f);
                best.style.backgroundColor = XRPalette.Warn;
                best.style.paddingLeft = best.style.paddingRight = 16f;
                Round(best, 10f);
                verdict.Add(best);
            }

            verdict.Add(Spacer());
            var (punctuality, ink) = result.Stars >= 3 ? ("ON TIME", XRPalette.Success)
                : result.Stars == 2 ? ("SLIGHT DELAY", XRPalette.Warn) : ("DELAYED", XRPalette.Stop);
            verdict.Add(Stamp(punctuality, ink));

            var buttons = Row(card);
            buttons.style.marginTop = StyleKeyword.Auto;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.Add(Button("LINE MAP", map, false));
            buttons.Add(Button("RUN AGAIN", retry, false));
            buttons.Add(Button(lineComplete ? "TO THE NETWORK" : "NEXT STATION", next, true));
        }

        /// <summary>The top row in play and paused: the line's badge and the station's code.</summary>
        private VisualElement StationHead(VisualElement card, LineDefinition line, int stationIndex)
        {
            var head = Row(card);
            head.style.alignItems = Align.Center;
            head.Add(Badge(line, 96f));
            Put(head, Text(line != null ? $"{line.Code}{stationIndex + 1:00}" : "", 56f, true, XRPalette.InkDim, 6f)).style.marginLeft = 24f;
            return head;
        }

        // ------------------------------------------------------------------ the verdict stamp

        /// <summary>The verdict stamp: a paper box ruled in the verdict's colour, pressed on by <see cref="PressStamp"/>.</summary>
        private VisualElement Stamp(string text, Color ink)
        {
            var stamp = new VisualElement { pickingMode = PickingMode.Ignore };
            stamp.style.flexShrink = 0;
            stamp.style.alignItems = Align.Center;
            stamp.style.backgroundColor = XRPalette.Paper;
            stamp.style.borderTopWidth = stamp.style.borderBottomWidth = stamp.style.borderLeftWidth = stamp.style.borderRightWidth = 7f;
            stamp.style.borderTopColor = stamp.style.borderBottomColor = stamp.style.borderLeftColor = stamp.style.borderRightColor = ink;
            Round(stamp, 12f);
            stamp.style.paddingTop = stamp.style.paddingBottom = 8f;
            stamp.style.paddingLeft = stamp.style.paddingRight = 34f;
            // Room for the tilted corners inside the card.
            stamp.style.marginRight = 24f;
            stamp.Add(Text(text, 72f, true, ink, 8f));
            stamp.style.opacity = 0f;
            _stamp = stamp;
            _stampAt = Time.unscaledTime + StampDelay;
            return stamp;
        }

        /// <summary>Down fast from <see cref="StampFrom"/> past rest to <see cref="StampHit"/>, out to rest, then the rattle.</summary>
        private void PressStamp()
        {
            if (_stamp == null) return;
            var elapsed = Time.unscaledTime - _stampAt;
            if (elapsed < 0f) return;

            var press = Mathf.Clamp01(elapsed / StampPressSeconds);
            var shake = Mathf.Clamp01((elapsed - StampPressSeconds) / StampShakeSeconds);
            var scale = press < StampImpact
                ? Mathf.Lerp(StampFrom, StampHit, EaseIn(press / StampImpact))
                : Mathf.Lerp(StampHit, 1f, EaseOut((press - StampImpact) / (1f - StampImpact)));
            var angle = press < 1f
                ? Mathf.Lerp(StampEntryTilt, StampRest, EaseIn(Mathf.Min(press / StampImpact, 1f)))
                : StampRest + Mathf.Sin(shake * StampShakeCycles * 2f * Mathf.PI) * StampShakeDegrees * (1f - shake) * (1f - shake);

            _stamp.style.opacity = Mathf.Clamp01(press * 3f);
            _stamp.style.scale = new Scale(Vector3.one * scale);
            _stamp.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            // At rest: nothing more to do until the next arrival.
            if (shake >= 1f) _stamp = null;
        }

        private static float EaseIn(float t) => t * t;
        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        // ------------------------------------------------------------------ building blocks

        /// <summary>Switches the sign on and clears the card for a new view.</summary>
        private VisualElement Begin()
        {
            gameObject.SetActive(true);
            _clock = null;
            _clockText = null;
            _stamp = null;
            _touch.Clear();
            _targets.Clear();
            _tier = -1;

            var root = _document.rootVisualElement;
            root.Clear();
            return Card(root, 10f, 36f, 38f, 48f);
        }

        private Label Text(string text, float size, bool heading, Color colour, float letterSpacing = 0f) =>
            XRSignageUi.Text(_assets, text, size, heading, colour, letterSpacing);

        /// <summary>A sign button, registered for a fingertip.</summary>
        private Button Button(string text, Action clicked, bool primary)
        {
            var button = XRSignageUi.Button(_assets, text, clicked, primary);
            _touch.Add(button, clicked);
            return button;
        }

        private VisualElement Badge(LineDefinition line, float size) => XRSignageUi.Badge(_assets, line, size);

        private VisualElement StarTally(int stars) => XRSignageUi.StarTally(_assets, stars);

        /// <summary>Destroy in play mode, DestroyImmediate in edit mode, so this can be built from an editor probe.</summary>
        private static void Kill(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }

    /// <summary>A filled five-pointed star. Drawn rather than typed: Barlow has no star glyph.</summary>
    public sealed class XRStarGlyph : VisualElement
    {
        private readonly Color _colour;

        public XRStarGlyph(float size, Color colour)
        {
            _colour = colour;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var centre = rect.center;
            var outer = Mathf.Min(rect.width, rect.height) / 2f;
            var inner = outer * 0.45f;
            var painter = context.painter2D;
            painter.fillColor = _colour;
            painter.BeginPath();
            for (var k = 0; k < 10; k++)
            {
                // Point up: panel y grows downward, so the first point is at -90 degrees.
                var angle = (-90f + 36f * k) * Mathf.Deg2Rad;
                var radius = k % 2 == 0 ? outer : inner;
                var point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (k == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }

            painter.ClosePath();
            painter.Fill();
        }
    }
}
