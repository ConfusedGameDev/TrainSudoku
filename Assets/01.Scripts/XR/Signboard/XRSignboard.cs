using System;
using System.Collections.Generic;
using TrainSudoku.Core;
using TrainSudoku.Game;
using TrainSudoku.XR.Rules;
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
    /// Copy comes from the `XR` String Table (<see cref="XRText"/>); the words are the phone's where the phone has them.
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

        /// <summary>The arrival's stars pop one after another, as on the web edition, and the stamp comes down after the last.</summary>
        private const float StarInterval = 0.12f;
        private const float StarSeconds = 0.2f;
        private const float StarPop = 1.25f;
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
        private bool _stampHeard;

        /// <summary>The arrival's three stars while they pop, how many were earned and have been heard, and when the first starts.</summary>
        private readonly List<VisualElement> _stars = new List<VisualElement>();
        private int _starsEarned;
        private int _starsHeard;
        private float _starsAt;

        /// <summary>Pip on the view on show, and her speech bubble on the station view; null where there is none.</summary>
        private XRPip _pip;
        private Label _bubble;

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
            PopStars();
            PressStamp();
            if (_pip != null) _pip.Tick(Time.unscaledTime);
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
            Size(mark, 200f, 200f);
            Round(mark, 100f);
            mark.style.marginRight = 40f;
            if (_assets != null && _assets.Mark != null) mark.style.backgroundImage = _assets.Mark;
            row.Add(mark);

            var words = new VisualElement();
            row.Add(words);
            // Proper nouns of the masthead: never in a string table (the phone's rule, kept).
            words.Add(Text("TSUGI", 190f, true, XRPalette.Ink, 10f));
            words.Add(Text("NEXT STATION", 56f, true, XRPalette.InkDim, 12f));
            // Pip beckons the player on, as on the web edition's concourse.
            row.Add(Spacer());
            row.Add(Pip(290f, XRPipMood.Wave));

            var strip = Led(card);
            strip.Add(Text(XRText.Get(XRKeys.MastheadNetwork), 64f, true, XRPalette.Led, 6f));
            strip.Add(Spacer());
            strip.Add(StarTally(stars));
            Put(card, Text(XRText.Get(XRKeys.MastheadHint), 42f, false, XRPalette.InkDim)).style.marginTop = 18f;
        }

        /// <summary>One line's map (6.2): its code and name, and how far along it the player is, on the LED strip.</summary>
        public void ShowLine(LineDefinition line, int cleared, int stations, int stars)
        {
            var card = Begin();
            var row = Row(card);
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;
            row.Add(Badge(line, 170f));

            var words = new VisualElement();
            words.style.marginLeft = 36f;
            row.Add(words);
            // The longest line names run to thirteen letters: they shrink to leave Pip her corner.
            var name = line != null ? line.DisplayName.ToUpperInvariant() : "";
            words.Add(Text(name, 118f * Mathf.Min(1f, 10f / Mathf.Max(1, name.Length)), true, XRPalette.Ink, 4f));
            words.Add(Text(XRText.Get(XRKeys.LineMapTitle), 56f, true, XRPalette.InkDim, 10f));
            row.Add(Spacer());
            row.Add(Pip(250f, XRPipMood.Idle));

            var strip = Led(card);
            strip.Add(Text(XRText.Get(XRKeys.LineMapCleared, cleared, stations), 64f, true, XRPalette.Led, 6f));
            strip.Add(Spacer());
            strip.Add(StarTally(stars));

            Put(card, Text(XRText.Get(XRKeys.LineMapHint), 42f, false, XRPalette.InkDim)).style.marginTop = 18f;
        }

        /// <summary>Play (6.2): the station, the clock and the stars still there to beat.</summary>
        public void ShowStation(LevelDefinition station, LineDefinition line, int stationIndex)
        {
            var card = Begin();
            var head = StationHead(card, line, stationIndex);
            head.Add(Spacer());

            // The stars still there to beat, beside the station's code.
            var times = station != null ? station.StarTimes : null;
            if (times != null && times.Count >= 2 && times[0] > 0)
                for (var stars = 3; stars >= 2; stars--)
                {
                    var target = Row(head);
                    target.style.alignItems = Align.Center;
                    target.style.marginLeft = 44f;
                    for (var i = 0; i < stars; i++) target.Add(new XRStarGlyph(46f, XRPalette.Star));
                    Put(target, Text(ProgressTracker.FormatTime(times[3 - stars]), 56f, true, XRPalette.Ink, 2f)).style.marginLeft = 14f;
                    _targets.Add((stars, target));
                }

            var strip = Led(card);
            strip.style.marginTop = 20f;
            strip.Add(Text(station != null ? station.DisplayName.ToUpperInvariant() : "", 92f, true, XRPalette.Led, 4f));
            strip.Add(Spacer());
            _clock = Text("00:00", 150f, true, XRPalette.Led, 4f);
            strip.Add(_clock);

            // Pip watches the board from under the clock and says what she sees (SetPip).
            var corner = Row(card);
            corner.style.marginTop = StyleKeyword.Auto;
            corner.style.alignItems = Align.FlexEnd;
            corner.Add(Pip(190f, XRPipMood.Idle));
            _bubble = Bubble(_assets, "", 56f);
            _bubble.style.marginLeft = 14f;
            _bubble.style.marginBottom = 64f;
            _bubble.style.visibility = Visibility.Hidden;
            corner.Add(_bubble);
        }

        /// <summary>
        /// Pip's mood on the view on show and, on the station view, what she says; null or empty words put the bubble
        /// away. A view drawn afresh starts with a quiet Pip, so the shell says it again after every redraw.
        /// </summary>
        public void SetPip(XRPipMood mood, string words)
        {
            if (_pip != null) _pip.SetMood(mood);
            if (_bubble == null) return;
            var say = !string.IsNullOrEmpty(words);
            if (say) _bubble.text = words;
            _bubble.style.visibility = say ? Visibility.Visible : Visibility.Hidden;
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
            head.Add(Button(XRText.Get(XRKeys.PauseResume), resume, true));

            var strip = Led(card);
            strip.style.marginTop = 20f;
            strip.Add(Text(XRText.Get(XRKeys.PauseTitle), 120f, true, XRPalette.Led, 12f));
            strip.Add(Spacer());
            _clock = Text("00:00", 150f, true, XRPalette.Led, 4f);
            strip.Add(_clock);

            // Pip dozes through the pause, as on the web edition's signal stop.
            var corner = Row(card);
            corner.style.marginTop = StyleKeyword.Auto;
            corner.style.alignItems = Align.Center;
            corner.Add(Pip(170f, XRPipMood.Sleep));
            var hint = Put(corner, Text(XRText.Get(XRKeys.PauseHint), 42f, false, XRPalette.InkDim));
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.flexShrink = 1;
            hint.style.marginLeft = 28f;
        }

        /// <summary>
        /// One card of the tutorial's rules briefing (6.2, 7): shown once per session on the tutorial station with no rails
        /// laid, in place of the station view, which comes back after the last card.
        /// </summary>
        public void ShowBriefing(LineDefinition line, int page, int pages, string title, string body, string button, Action next)
        {
            var card = Begin();
            var head = Row(card);
            head.style.alignItems = Align.Center;
            head.Add(Badge(line, 96f));
            Put(head, Text(title, 88f, true, XRPalette.Ink, 6f)).style.marginLeft = 32f;
            head.Add(Spacer());
            head.Add(Text($"{page + 1} / {pages}", 52f, true, XRPalette.InkDim, 4f));

            var words = Put(card, Text(body, 50f, false, XRPalette.Ink));
            words.style.whiteSpace = WhiteSpace.Normal;
            words.style.marginTop = 24f;
            words.style.flexGrow = 1;
            words.style.unityTextAlign = TextAnchor.UpperLeft;

            var buttons = Row(card);
            buttons.style.alignItems = Align.FlexEnd;
            buttons.Add(Pip(140f, XRPipMood.Wave));
            buttons.Add(Spacer());
            buttons.Add(Button(button, next, true));
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
            words.Add(Text(XRText.Get(XRKeys.ArrivalTitle), 46f, true, XRPalette.InkDim, 10f));
            words.Add(Text(station != null ? station.DisplayName.ToUpperInvariant() : "", 90f, true, XRPalette.Ink, 3f));
            head.Add(Spacer());
            // Pip cheers the train in.
            head.Add(Pip(170f, XRPipMood.Cheer));

            var strip = Led(card);
            strip.style.marginTop = 16f;
            strip.Add(Text(XRText.Get(XRKeys.ArrivalThisRun, ProgressTracker.FormatTime(result.Time)), 60f, true, XRPalette.Led, 4f));
            strip.Add(Spacer());
            strip.Add(Text(XRText.Get(XRKeys.ArrivalBest, ProgressTracker.FormatTime(result.BestTime)), 60f, true, XRPalette.Led, 4f));

            // The stars pop in turn, then the verdict is stamped on beside them and the new-best flag.
            var verdict = Row(card);
            verdict.style.marginTop = 16f;
            verdict.style.alignItems = Align.Center;
            for (var i = 0; i < 3; i++)
            {
                var star = new XRStarGlyph(80f, i < result.Stars ? XRPalette.Star : XRPalette.ClosedLight);
                star.style.scale = new Scale(Vector3.zero);
                verdict.Add(star);
                _stars.Add(star);
            }

            _starsEarned = result.Stars;
            _starsHeard = 0;
            _starsAt = Time.unscaledTime + StampDelay;

            if (result.IsNewBest)
            {
                var best = Text(XRText.Get(XRKeys.ArrivalNewBest), 44f, true, XRPalette.Ink, 8f);
                best.style.backgroundColor = XRPalette.Warn;
                best.style.marginLeft = 28f;
                best.style.paddingLeft = best.style.paddingRight = 20f;
                Round(best, 24f);
                verdict.Add(best);
            }

            verdict.Add(Spacer());
            var (punctuality, ink) = result.Stars >= 3 ? (XRText.Get(XRKeys.ArrivalOnTime), XRPalette.Success)
                : result.Stars == 2 ? (XRText.Get(XRKeys.ArrivalSlightDelay), XRPalette.Warn) : (XRText.Get(XRKeys.ArrivalDelayed), XRPalette.Stop);
            verdict.Add(Stamp(punctuality, ink));

            var buttons = Row(card);
            buttons.style.marginTop = StyleKeyword.Auto;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.Add(Button(XRText.Get(XRKeys.ArrivalMap), map, false));
            buttons.Add(Button(XRText.Get(XRKeys.ArrivalRetry), retry, false));
            buttons.Add(Button(XRText.Get(lineComplete ? XRKeys.ArrivalToNetwork : XRKeys.ArrivalNext), next, true));
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
            Round(stamp, 18f);
            stamp.style.paddingTop = stamp.style.paddingBottom = 8f;
            stamp.style.paddingLeft = stamp.style.paddingRight = 34f;
            // Room for the tilted corners inside the card.
            stamp.style.marginRight = 24f;
            stamp.Add(Text(text, 72f, true, ink, 8f));
            stamp.style.opacity = 0f;
            _stamp = stamp;
            // After the last star, so the beat reads rating, then verdict.
            _stampAt = Time.unscaledTime + StampDelay + 3 * StarInterval + StarSeconds;
            _stampHeard = false;
            return stamp;
        }

        /// <summary>Down fast from <see cref="StampFrom"/> past rest to <see cref="StampHit"/>, out to rest, then the rattle.</summary>
        private void PressStamp()
        {
            if (_stamp == null) return;
            var elapsed = Time.unscaledTime - _stampAt;
            if (elapsed < 0f) return;

            var press = Mathf.Clamp01(elapsed / StampPressSeconds);
            if (press >= StampImpact && !_stampHeard)
            {
                // The thump on impact, from the sign.
                _stampHeard = true;
                XRCuePlayer.Play(XRCue.Stamp, _panel != null ? _panel.position : transform.position);
            }

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

        /// <summary>Each star in from nothing, over-size at the middle of its pop; an earned one is heard as it lands.</summary>
        private void PopStars()
        {
            if (_stars.Count == 0) return;
            var elapsed = Time.unscaledTime - _starsAt;
            var done = true;
            for (var i = 0; i < _stars.Count; i++)
            {
                var pop = Mathf.Clamp01((elapsed - i * StarInterval) / StarSeconds);
                done &= pop >= 1f;
                var scale = pop <= 0f ? 0f : pop * (1f + (StarPop - 1f) * Mathf.Sin(pop * Mathf.PI));
                _stars[i].style.scale = new Scale(new Vector3(scale, scale, 1f));
                if (pop > 0f && i < _starsEarned && i >= _starsHeard)
                {
                    _starsHeard = i + 1;
                    XRCuePlayer.Play(XRCue.Star, _panel != null ? _panel.position : transform.position);
                }
            }

            // At rest: nothing more to do until the next arrival.
            if (done) _stars.Clear();
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
            _stars.Clear();
            _pip = null;
            _bubble = null;
            _touch.Clear();
            _targets.Clear();
            _tier = -1;

            var root = _document.rootVisualElement;
            root.Clear();
            return Card(root, CardBorder, CardRadius, 30f, 44f, CardChunk);
        }

        /// <summary>Pip on this view, kept so <see cref="LateUpdate"/> can move her.</summary>
        private XRPip Pip(float size, XRPipMood mood) => _pip = new XRPip(_assets, size, mood);

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
