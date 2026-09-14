using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using TrainSudoku.Game;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TrainSudoku.XR
{
    /// <summary>
    /// The standing signboard behind the platform's far edge (XR-PRD 6.2, 8): the masthead on the network, the line on
    /// its map, the station, clock and stars to beat in play, and the arrival. A world-space UI Toolkit panel in the
    /// phone's station-signage language — paper card, ink type, the amber LED strip — built by XR itself (X20).
    /// </summary>
    /// <remarks>
    /// It stands on two posts and turns about the vertical to face the player's head, like the clue signs, so it reads
    /// from any edge, and leans back to the eyes. Its buttons are clicked by ray, through the
    /// <see cref="XRSimpleInteractable"/> beside its <see cref="UIDocument"/> that XRI's UI Toolkit support asks for, and
    /// pressed with a fingertip, which the sign reads itself (<see cref="XRTouchPoints"/>): XRI's poke never pressed one
    /// on the headset (the XR7 headset check), so the panel has no poke filter.
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

        /// <summary>
        /// A fingertip press on a button, in metres in front of the card's face: more than <see cref="ArmGap"/> away it is
        /// ready, and coming to within <see cref="PressGap"/> over a button presses it, once, until it draws back. Within
        /// <see cref="HoverGap"/> the button under it swells a little. <see cref="ButtonSlop"/> widens every button, in UI
        /// pixels (3 mm), for tracking that is good to about a centimetre.
        /// </summary>
        private const float ArmGap = 0.02f;
        private const float PressGap = 0.006f;
        private const float HoverGap = 0.04f;
        private const float ButtonSlop = 10f;
        private const float HoverScale = 1.06f;

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

        /// <summary>The view's buttons and what each does, for a fingertip to press; which fingertips are ready; the one under a finger.</summary>
        private readonly List<(Button Button, Action Clicked)> _buttons = new List<(Button Button, Action Clicked)>();
        private readonly Dictionary<int, bool> _armedTips = new Dictionary<int, bool>();
        private readonly List<int> _goneTips = new List<int>();
        private Button _hovered;

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

            // The document comes before the interactable: XRI finds a UI Toolkit panel when the interactable registers.
            sign._document = panel.AddComponent<UIDocument>();
            sign._document.panelSettings = assets != null ? assets.PanelSettings : null;
            sign._document.worldSpaceSizeMode = WorldSpaceSizeMode.Fixed;
            sign._document.worldSpaceSize = new Vector2(PanelWidth, PanelHeight);
            sign._document.pivot = Pivot.BottomCenter;
            panel.AddComponent<XRSimpleInteractable>();

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
            TouchButtons();
        }

        /// <summary>
        /// The buttons answer a fingertip as well as a ray: ready more than <see cref="ArmGap"/> in front of the card,
        /// pressing within <see cref="PressGap"/> of its face over a button. One that comes to the face anywhere else has
        /// touched down, and must draw back before it can press.
        /// </summary>
        private void TouchButtons()
        {
            Button hovered = null;
            var tips = XRTouchPoints.Fingertips;
            var root = _document != null ? _document.rootVisualElement : null;
            if (_panel != null && root != null && _buttons.Count > 0)
            {
                var metres = Mathf.Max(1e-6f, _panel.lossyScale.z);
                var bounds = root.worldBound;
                foreach (var tip in tips)
                {
                    // Card space: the document's pivot is its bottom centre on the panel's origin, in units of
                    // PixelsPerUnit UI pixels, and the player's side of the card is -z.
                    var local = _panel.InverseTransformPoint(tip.Position);
                    var gap = -local.z * metres;
                    var point = new Vector2(
                        bounds.xMin + (local.x * PixelsPerUnit / PanelWidth + 0.5f) * bounds.width,
                        bounds.yMin + (1f - local.y * PixelsPerUnit / PanelHeight) * bounds.height);
                    var button = ButtonAt(point);
                    if (button != null && Mathf.Abs(gap) < HoverGap) hovered = button;

                    if (gap > ArmGap)
                    {
                        _armedTips[tip.Id] = true;
                    }
                    else if (gap <= PressGap && _armedTips.TryGetValue(tip.Id, out var armed) && armed)
                    {
                        _armedTips[tip.Id] = false;
                        if (button != null)
                        {
                            // Pressing may rebuild the card, and with it the button list: nothing more this frame.
                            Click(button);
                            return;
                        }
                    }
                }
            }

            _goneTips.Clear();
            foreach (var id in _armedTips.Keys)
            {
                var present = false;
                foreach (var tip in tips)
                    if (tip.Id == id)
                    {
                        present = true;
                        break;
                    }

                if (!present) _goneTips.Add(id);
            }

            foreach (var id in _goneTips) _armedTips.Remove(id);

            if (hovered == _hovered) return;
            if (_hovered != null) _hovered.style.scale = StyleKeyword.Null;
            _hovered = hovered;
            if (_hovered != null) _hovered.style.scale = new Scale(Vector3.one * HoverScale);
        }

        private Button ButtonAt(Vector2 point)
        {
            foreach (var (button, _) in _buttons)
            {
                if (button.panel == null || !button.enabledInHierarchy) continue;
                var rect = button.worldBound;
                if (point.x >= rect.xMin - ButtonSlop && point.x <= rect.xMax + ButtonSlop &&
                    point.y >= rect.yMin - ButtonSlop && point.y <= rect.yMax + ButtonSlop)
                    return button;
            }

            return null;
        }

        private void Click(Button button)
        {
            Action clicked = null;
            foreach (var (candidate, action) in _buttons)
                if (candidate == button)
                {
                    clicked = action;
                    break;
                }

            clicked?.Invoke();
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
        /// <param name="back">Back to the network. On the sign until the wrist menu arrives at XR8.</param>
        public void ShowLine(LineDefinition line, int cleared, int stations, int stars, Action back)
        {
            var card = Begin();
            // The way back sits at the top, as LINE MAP does in play: a ray reaching for the bottom of the card skims
            // low over the map and can catch a roundel first (the XR7 headset check).
            var top = Row(card);
            top.style.justifyContent = Justify.FlexEnd;
            top.Add(Button("‹  NETWORK", back, false));

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
        /// <param name="map">Back to the line map. On the sign until the wrist menu arrives at XR8.</param>
        public void ShowStation(LevelDefinition station, LineDefinition line, int stationIndex, Action map)
        {
            var card = Begin();
            var head = Row(card);
            head.style.alignItems = Align.Center;
            head.Add(Badge(line, 96f));
            Put(head, Text(line != null ? $"{line.Code}{stationIndex + 1:00}" : "", 56f, true, XRPalette.InkDim, 6f)).style.marginLeft = 24f;
            head.Add(Spacer());
            head.Add(Button("LINE MAP", map, false));

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
            _buttons.Clear();
            _hovered = null;
            _targets.Clear();
            _tier = -1;

            var root = _document.rootVisualElement;
            root.Clear();
            var card = new VisualElement { name = "card" };
            card.style.flexGrow = 1;
            card.style.backgroundColor = XRPalette.Paper;
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 10f;
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = XRPalette.Ink;
            Round(card, 36f);
            card.style.paddingTop = card.style.paddingBottom = 38f;
            card.style.paddingLeft = card.style.paddingRight = 48f;
            root.Add(card);
            return card;
        }

        private Label Text(string text, float size, bool heading, Color colour, float letterSpacing = 0f)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = colour;
            label.style.letterSpacing = letterSpacing;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0f;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            var font = _assets == null ? null : heading ? _assets.HeadingFont : _assets.BodyFont;
            if (font != null) label.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(font));
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        private Button Button(string text, Action clicked, bool primary)
        {
            var button = new Button(clicked) { text = text };
            button.style.fontSize = 50f;
            button.style.letterSpacing = 4f;
            button.style.color = primary ? XRPalette.Ink : XRPalette.Paper;
            button.style.backgroundColor = primary ? XRPalette.Warn : XRPalette.Ink;
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 0f;
            button.style.paddingTop = button.style.paddingBottom = 18f;
            button.style.paddingLeft = button.style.paddingRight = 34f;
            button.style.marginLeft = 20f;
            Round(button, 16f);
            if (_assets != null && _assets.HeadingFont != null)
                button.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(_assets.HeadingFont));
            _buttons.Add((button, clicked));
            return button;
        }

        /// <summary>The LED strip: amber type on the dark ground, the phone's departure-board look.</summary>
        private static VisualElement Led(VisualElement parent)
        {
            var strip = Row(parent);
            strip.style.alignItems = Align.Center;
            strip.style.backgroundColor = XRPalette.LedGround;
            strip.style.paddingTop = strip.style.paddingBottom = 16f;
            strip.style.paddingLeft = strip.style.paddingRight = 30f;
            Round(strip, 16f);
            return strip;
        }

        /// <summary>A line's roundel: its code on a disc of its colour.</summary>
        private VisualElement Badge(LineDefinition line, float size)
        {
            var colour = line != null ? line.Color : XRPalette.Warn;
            var badge = new VisualElement();
            Size(badge, size, size);
            Round(badge, size / 2f);
            badge.style.backgroundColor = colour;
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            var code = Text(line != null ? line.Code : "", size * 0.42f, true, XRPlatformMap.Contrast(colour), 2f);
            code.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.Add(code);
            return badge;
        }

        private VisualElement StarTally(int stars)
        {
            var tally = Row(null);
            tally.style.alignItems = Align.Center;
            tally.Add(new XRStarGlyph(58f, XRPalette.Led));
            Put(tally, Text(stars.ToString(), 64f, true, XRPalette.Led, 4f)).style.marginLeft = 14f;
            return tally;
        }

        /// <summary>Adds <paramref name="child"/> and hands it back, for styling it in the same breath.</summary>
        private static T Put<T>(VisualElement parent, T child) where T : VisualElement
        {
            parent.Add(child);
            return child;
        }

        /// <summary>Destroy in play mode, DestroyImmediate in edit mode, so this can be built from an editor probe.</summary>
        private static void Kill(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.pickingMode = PickingMode.Ignore;
            parent?.Add(row);
            return row;
        }

        private static VisualElement Spacer()
        {
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            spacer.pickingMode = PickingMode.Ignore;
            return spacer;
        }

        private static void Size(VisualElement element, float width, float height)
        {
            element.style.width = width;
            element.style.height = height;
            element.style.flexShrink = 0;
        }

        private static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius =
                element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
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
