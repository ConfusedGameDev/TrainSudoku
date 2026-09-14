using System;
using System.Collections.Generic;
using TrainSudoku.Game;
using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>How one station stands with the player, for its roundel on the line map.</summary>
    public readonly struct StationMark
    {
        /// <summary>Reachable: its roundel can be chosen.</summary>
        public bool Open { get; }

        /// <summary>Left unfinished: choosing it continues that attempt.</summary>
        public bool InProgress { get; }

        /// <summary>The best rating earned on it, 0 for never finished.</summary>
        public int Stars { get; }

        /// <summary>The first station on the line not yet finished: the one the map points the player at.</summary>
        public bool Next { get; }

        public StationMark(bool open, bool inProgress, int stars, bool next)
        {
            Open = open;
            InProgress = inProgress;
            Stars = stars;
            Next = next;
        }
    }

    /// <summary>
    /// The maps printed on the platform (XR-PRD 6.2) while no board is on it: the network, lines as coloured track with
    /// a raised roundel at each line's end, and one line's station map, a raised roundel at every station with its name
    /// printed beside it. Both lie on a card of fixed footprint, about the size of an 8x8 board (5.4), whose near edge
    /// sits on the board root's origin like the board's.
    /// </summary>
    /// <remarks>
    /// Everything is built fresh on each <see cref="ShowNetwork"/> or <see cref="ShowLine"/>: a few dozen objects, and
    /// the progress they show changes between visits. Positions come from the lines' own map nodes, fitted by
    /// <see cref="MapFit"/>, so a line authored with the phone's Line Map Editor lands here unchanged.
    /// </remarks>
    public sealed class XRPlatformMap : MonoBehaviour
    {
        /// <summary>The card, in cells: 9.6 is 58 cm at the 6 cm cell.</summary>
        public const float CardSize = 9.6f;

        /// <summary>How far the card's near edge sits from the root's origin, so its concrete base lines up with the board's edge.</summary>
        public const float CardInset = 0.2f;

        private const float BaseHeight = XRBoardDisplay.TileHeight;
        private const float BaseMargin = 0.15f;
        private const float PaperThickness = 0.01f;

        /// <summary>
        /// Each printed layer floats this much above the last (0.3 mm), and the first this much above the paper, so no two
        /// surfaces fight for the same depth: a stroke printed flush with the paper's top showed only as broken dashes.
        /// </summary>
        private const float LayerStep = 0.005f;

        // The network.
        private const float NetworkMargin = 0.7f;

        /// <summary>The most cells a map unit may take: the phone's 150-unit node pitch at most three cells apart.</summary>
        private const float MaxScale = 0.02f;

        /// <summary>
        /// The network's stroke, in map units scaled by the fit, as on the phone: a map measurement, so the lines stay
        /// in proportion from the opening two lines to the whole network...
        /// </summary>
        private const float DesignStroke = 44f;

        /// <summary>...but never thinner than 6 mm, or the whole network's lines vanish.</summary>
        private const float MinStroke = 0.1f;

        /// <summary>Roundels are at least 3 cm across (6.2).</summary>
        private const float RoundelSize = 0.55f;

        // The line map.
        private const float LineMarginX = 2.6f;
        private const float LineMarginZ = 0.9f;
        private const float LineStroke = 0.2f;
        private const float HaloShare = 0.78f;
        private const float CodeHeight = 0.13f;
        private const float NameHeight = 0.19f;
        private const float LineSpacing = 1.25f;
        private const float LabelGap = 0.12f;
        private const float ExtrasHeight = 0.28f;
        private const float StarRadius = 0.1f;
        private const float StarGap = 0.04f;
        private const float ContinueHeight = 0.12f;

        private static float PrintY => BaseHeight + PaperThickness + LayerStep;
        private static float CentreZ => CardInset + CardSize / 2f;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Transform _print;

        /// <summary>An open line's roundel was chosen on the network.</summary>
        public event Action<int> LineChosen;

        /// <summary>An open station's roundel was chosen on the line map, by its position along the line.</summary>
        public event Action<int> StationChosen;

        /// <summary>The card's far edge, in cells from the root's origin: where the signboard stands behind it.</summary>
        public static float FarEdge => CardInset + CardSize + BaseMargin;

        /// <summary>How far the card's base reaches either side of the middle of its near edge, in cells: where the board handle wraps its corner.</summary>
        public static float HalfWidth => CardSize / 2f + BaseMargin;

        public static XRPlatformMap Create(Transform parent)
        {
            var go = new GameObject("Platform Map");
            go.transform.SetParent(parent, false);
            var map = go.AddComponent<XRPlatformMap>();
            map.BuildCard();
            map._print = new GameObject("Print").transform;
            map._print.SetParent(go.transform, false);
            go.SetActive(false);
            return map;
        }

        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ the network

        /// <summary>The network map: every line the player has earned and the one they are working towards (6.2).</summary>
        public void ShowNetwork(NetworkDefinition network, Func<int, bool> unlocked)
        {
            Clear();
            gameObject.SetActive(true);
            if (network == null) return;

            var revealed = MapReveal.Revealed(network.LineCount, index => Drawable(network.Line(index)), unlocked);
            var nodes = new List<(double X, double Y)>();
            foreach (var index in revealed)
                foreach (var node in network.Line(index).MapNodes)
                    nodes.Add((node.x, node.y));
            if (!MapFit.TryFit(nodes, CardSize, CardSize, NetworkMargin, NetworkMargin, 0, CentreZ, MaxScale, out var frame)) return;

            var stroke = Mathf.Max(MinStroke, (float)(DesignStroke * frame.Scale));
            var layer = 0;
            foreach (var index in revealed)
            {
                var line = network.Line(index);
                var open = unlocked == null || unlocked(index);

                // A closed line is tinted towards the paper rather than greyed flat, so the player can still tell which
                // line they are working towards.
                var colour = open ? line.Color : Color.Lerp(XRPalette.Paper, line.Color, 0.25f);
                Print($"Line {line.Code}", new XRMapMesh(PrintY + LayerStep * layer++).Stroke(StrokeOf(line, frame), open ? stroke : stroke * 0.79f),
                    XRBoardMaterials.Solid(colour));

                var dots = new XRMapMesh(PrintY + LayerStep * layer++);
                foreach (var node in line.StationNodeIndices)
                    if (node >= 0 && node < line.MapNodes.Count) dots.Disc(At(frame, line.MapNodes[node]), stroke * 0.28f);
                Print($"Stations {line.Code}", dots,
                    XRBoardMaterials.Solid(open ? XRPalette.Paper : Color.Lerp(XRPalette.Paper, XRPalette.ClosedLight, 0.4f)));
            }

            // A ring at every node two revealed lines share, above both: a ring hanging off a line not yet on the map
            // would promise a line the player cannot see.
            var fills = new XRMapMesh(PrintY + LayerStep * layer++);
            var rings = new XRMapMesh(PrintY + LayerStep * layer++);
            foreach (var interchange in network.Interchanges)
            {
                if (!revealed.Contains(interchange.lineA) || !revealed.Contains(interchange.lineB)) continue;
                var line = network.Line(interchange.lineA);
                if (line == null || interchange.nodeA < 0 || interchange.nodeA >= line.MapNodes.Count) continue;
                var centre = At(frame, line.MapNodes[interchange.nodeA]);
                fills.Disc(centre, stroke * 0.95f);
                rings.Ring(centre, stroke * 0.75f, stroke * 1.15f);
            }

            Print("Interchanges", fills, XRBoardMaterials.Solid(XRPalette.Paper));
            Print("Interchange Rings", rings, XRBoardMaterials.Solid(XRPalette.Ink));

            // The roundel sits at the line's far end, where the phone hangs a closed line's padlock.
            foreach (var index in revealed)
            {
                var line = network.Line(index);
                var open = unlocked == null || unlocked(index);
                var look = open
                    ? new RoundelLook(XRBoardMaterials.Solid(line.Color), XRBoardMaterials.Solid(XRPalette.Ink), line.Code, Contrast(line.Color))
                    : new RoundelLook(XRBoardMaterials.Solid(XRPalette.ClosedLight), XRBoardMaterials.Solid(XRPalette.Closed), null, XRPalette.Closed,
                        XRBoardMaterials.Solid(XRPalette.Closed));
                var lineIndex = index;
                XRMapRoundel.Create(_print, $"Roundel {line.Code}", At(frame, line.MapNodes[line.MapNodes.Count - 1]),
                    PrintY + LayerStep * layer, RoundelSize, look, open, () => LineChosen?.Invoke(lineIndex));
            }
        }

        // ------------------------------------------------------------------ one line

        /// <summary>One line's station map: its route, a roundel at every station, and each station's name beside it (6.2).</summary>
        public void ShowLine(LineDefinition line, Func<int, StationMark> markOf)
        {
            Clear();
            gameObject.SetActive(true);
            if (!Drawable(line)) return;

            var nodes = new List<(double X, double Y)>();
            foreach (var node in line.MapNodes) nodes.Add((node.x, node.y));
            if (!MapFit.TryFit(nodes, CardSize, CardSize, LineMarginX, LineMarginZ, 0, CentreZ, MaxScale, out var frame)) return;

            Print($"Line {line.Code}", new XRMapMesh(PrintY).Stroke(StrokeOf(line, frame), LineStroke), XRBoardMaterials.Solid(line.Color));

            var centre = new Vector2(0f, CentreZ);
            var indices = line.StationNodeIndices;
            for (var station = 0; station < indices.Count; station++)
            {
                var node = indices[station];
                if (node < 0 || node >= line.MapNodes.Count) continue;
                var at = At(frame, line.MapNodes[node]);
                var mark = markOf != null ? markOf(station) : new StationMark(true, false, 0, false);

                if (mark.Next && mark.Open)
                    Print($"Halo {station + 1}", new XRMapMesh(PrintY + LayerStep).Disc(at, RoundelSize * HaloShare),
                        XRBoardMaterials.Solid(Color.Lerp(XRPalette.Paper, line.Color, 0.45f)));

                var number = (station + 1).ToString("00");
                RoundelLook look;
                if (!mark.Open)
                    look = new RoundelLook(XRBoardMaterials.Solid(Color.Lerp(XRPalette.Paper, XRPalette.ClosedLight, 0.5f)),
                        XRBoardMaterials.Solid(XRPalette.ClosedLight), null, XRPalette.Closed, XRBoardMaterials.Solid(XRPalette.Closed));
                else if (mark.Stars > 0)
                    look = new RoundelLook(XRBoardMaterials.Solid(line.Color), XRBoardMaterials.Solid(XRPalette.Ink), number, Contrast(line.Color));
                else
                    look = new RoundelLook(XRBoardMaterials.Solid(XRPalette.Paper), XRBoardMaterials.Solid(line.Color), number, XRPalette.Ink);

                var stationIndex = station;
                XRMapRoundel.Create(_print, $"Station {line.Code}{number}", at, PrintY + LayerStep * 2, RoundelSize, look, mark.Open,
                    () => StationChosen?.Invoke(stationIndex));

                var level = line.Station(station);
                PlaceLabel(at, Side(at, centre), $"{line.Code}{number}", level != null ? level.DisplayName.ToUpperInvariant() : "", mark);
            }
        }

        /// <summary>Which way a station's name hangs off its roundel: the phone's rule, out to the side it sits on, or above and below at the route's two ends.</summary>
        private static MarkerSide Side(Vector2 at, Vector2 centre)
        {
            if (Mathf.Abs(at.x - centre.x) < RoundelSize * 0.5f) return at.y > centre.y ? MarkerSide.Above : MarkerSide.Below;
            return at.x > centre.x ? MarkerSide.Right : MarkerSide.Left;
        }

        /// <summary>
        /// The code, the name and a line of extras (stars earned, "CONTINUE") stacked beside the roundel. Printed flat to
        /// read from the near edge, where the tray stands.
        /// </summary>
        private void PlaceLabel(Vector2 at, MarkerSide side, string code, string name, StationMark mark)
        {
            var codeLine = CodeHeight * LineSpacing;
            var nameLine = NameHeight * LineSpacing;
            var hasExtras = mark.Open && (mark.Stars > 0 || mark.InProgress);
            var block = codeLine + nameLine + (hasExtras ? ExtrasHeight : 0f);
            var out_ = RoundelSize / 2f + LabelGap;

            float x, top;
            TextAnchor codeAnchor, nameAnchor;
            switch (side)
            {
                case MarkerSide.Right:
                    x = at.x + out_;
                    top = at.y + block / 2f;
                    codeAnchor = TextAnchor.MiddleLeft;
                    break;
                case MarkerSide.Left:
                    x = at.x - out_;
                    top = at.y + block / 2f;
                    codeAnchor = TextAnchor.MiddleRight;
                    break;
                case MarkerSide.Above:
                    x = at.x;
                    top = at.y + out_ + block;
                    codeAnchor = TextAnchor.MiddleCenter;
                    break;
                default:
                    x = at.x;
                    top = at.y - out_;
                    codeAnchor = TextAnchor.MiddleCenter;
                    break;
            }

            nameAnchor = codeAnchor;
            var dim = !mark.Open;
            Put(FlatText(_print, code, CodeHeight, dim ? XRPalette.Closed : XRPalette.InkDim, codeAnchor), x, top - codeLine / 2f);
            Put(FlatText(_print, name, NameHeight, dim ? XRPalette.Closed : XRPalette.Ink, nameAnchor), x, top - codeLine - nameLine / 2f);
            if (!hasExtras) return;

            // The extras line: three stars when the station has been finished, then a CONTINUE tag when an attempt waits.
            var starsWidth = mark.Stars > 0 ? 3 * StarRadius * 2f + 2 * StarGap : 0f;
            var tagWidth = mark.InProgress ? ContinueHeight * 0.66f * 8f + ContinueHeight : 0f;
            var gap = starsWidth > 0f && tagWidth > 0f ? StarGap * 3f : 0f;
            var width = starsWidth + gap + tagWidth;
            var left = codeAnchor == TextAnchor.MiddleLeft ? x : codeAnchor == TextAnchor.MiddleRight ? x - width : x - width / 2f;
            var z = top - codeLine - nameLine - ExtrasHeight / 2f;

            if (starsWidth > 0f)
            {
                var earned = new XRMapMesh(PrintY + LayerStep);
                var unearned = new XRMapMesh(PrintY + LayerStep);
                for (var i = 0; i < 3; i++)
                    (i < mark.Stars ? earned : unearned).Star(new Vector2(left + StarRadius + i * (StarRadius * 2f + StarGap), z), StarRadius);
                Print("Stars", earned, XRBoardMaterials.Solid(XRPalette.Warn));
                Print("Stars Unearned", unearned, XRBoardMaterials.Solid(XRPalette.ClosedLight));
            }

            if (tagWidth > 0f)
            {
                var tagCentre = new Vector2(left + starsWidth + gap + tagWidth / 2f, z);
                Print("Continue Tag", new XRMapMesh(PrintY + LayerStep).Rect(tagCentre, new Vector2(tagWidth, ContinueHeight * 1.7f)),
                    XRBoardMaterials.Solid(XRPalette.Warn));
                var text = FlatText(_print, "CONTINUE", ContinueHeight, XRPalette.Ink, TextAnchor.MiddleCenter);
                text.transform.localPosition = new Vector3(tagCentre.x, PrintY + LayerStep * 2, tagCentre.y);
            }
        }

        private static void Put(TextMesh text, float x, float z) => text.transform.localPosition = new Vector3(x, PrintY + LayerStep, z);

        // ------------------------------------------------------------------ building

        /// <summary>The card: a concrete base the height of the board's slabs, with a sheet of paper on it for the map.</summary>
        private void BuildCard()
        {
            var size = CardSize + BaseMargin * 2f;
            var slab = Primitive(PrimitiveType.Cube, transform, XRBoardMaterials.Tile, "Card Base");
            slab.transform.localPosition = new Vector3(0f, BaseHeight / 2f, CentreZ);
            slab.transform.localScale = new Vector3(size, BaseHeight, size);

            var paper = Primitive(PrimitiveType.Cube, transform, XRBoardMaterials.Solid(XRPalette.Paper), "Card Paper");
            paper.transform.localPosition = new Vector3(0f, BaseHeight + PaperThickness / 2f, CentreZ);
            paper.transform.localScale = new Vector3(CardSize, PaperThickness, CardSize);
        }

        private void Print(string name, XRMapMesh shape, Material material)
        {
            var mesh = shape.ToMesh(name);
            _meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(_print, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Text lying on the card, its top towards the far side so it reads from the near edge.</summary>
        internal static TextMesh FlatText(Transform parent, string text, float height, Color colour, TextAnchor anchor)
        {
            var go = new GameObject($"Text {text}");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = XRPalette.Font;
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = height / 6.4f;
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : anchor == TextAnchor.MiddleRight ? TextAlignment.Right : TextAlignment.Center;
            mesh.color = colour;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = XROcclusionMaterials.Text(XRPalette.Font);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mesh;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, Material material, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Kill(go.GetComponent<Collider>());
            return go;
        }

        private void Clear()
        {
            if (_print != null)
                for (var i = _print.childCount - 1; i >= 0; i--)
                {
                    // Out of the hierarchy at once, so a rebuild in the same frame cannot find the old roundels. First let
                    // go of whatever is hovering or holding them, while their colliders still exist.
                    var child = _print.GetChild(i).gameObject;
                    foreach (var roundel in child.GetComponentsInChildren<XRMapRoundel>(true)) roundel.Release();
                    child.SetActive(false);
                    Kill(child);
                }

            foreach (var mesh in _meshes)
                Kill(mesh);
            _meshes.Clear();
        }

        private void OnDestroy() => Clear();

        /// <summary>Destroy in play mode, DestroyImmediate in edit mode, so this can be built from an editor probe.</summary>
        private static void Kill(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        // ------------------------------------------------------------------ map space

        private static bool Drawable(LineDefinition line) => line != null && line.HasContent && line.MapNodes.Count > 1;

        private static List<Vector2> StrokeOf(LineDefinition line, MapFrame frame)
        {
            var nodes = new List<(double X, double Y)>();
            foreach (var node in line.MapNodes) nodes.Add((node.x, node.y));
            var shape = line.MapShape == MapShape.Stadium ? StrokeShape.Stadium : line.MapShape == MapShape.Loop ? StrokeShape.Loop : StrokeShape.Route;
            var points = new List<Vector2>();
            foreach (var (x, y) in MapStroke.Points(shape, nodes))
            {
                var (px, pz) = frame.ToPlatform(x, y);
                points.Add(new Vector2((float)px, (float)pz));
            }

            return points;
        }

        private static Vector2 At(MapFrame frame, Vector2 node)
        {
            var (x, z) = frame.ToPlatform(node.x, node.y);
            return new Vector2((float)x, (float)z);
        }

        /// <summary>Ink on a light line colour, paper on a dark one.</summary>
        internal static Color Contrast(Color colour) =>
            0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b > 0.55f ? XRPalette.Ink : XRPalette.Paper;

        private enum MarkerSide
        {
            Right,
            Left,
            Above,
            Below,
        }
    }
}
