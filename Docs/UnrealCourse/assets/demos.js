/* Live demos: small JavaScript ports of TrainSudoku.Core, so a rule can be played with before it is ported.
   Each demo mounts into an element by id and does nothing when that element is absent. */
(function () {
  "use strict";

  // ---------------------------------------------------------------- Core, as in Assets/01.Scripts/Core
  var N = 0, E = 1, S = 2, W = 3;
  var DIRS = [N, E, S, W];
  var DIR_NAME = ["North", "East", "South", "West"];
  var DX = [0, 1, 0, -1], DY = [-1, 0, 1, 0];           // North is y-1: row 0 is the far row
  var KEYS = ["NS", "EW", "NW", "NE", "SW", "SE"];
  var CONN = { NS: [N, S], EW: [E, W], NW: [N, W], NE: [N, E], SW: [S, W], SE: [S, E] };
  function opposite(d) { return (d + 2) % 4; }
  function has(key, d) { return CONN[key][0] === d || CONN[key][1] === d; }
  function other(key, d) { return CONN[key][0] === d ? CONN[key][1] : CONN[key][0]; }

  function tunnelCell(t, w, h) {
    return { x: t.side === E ? w - 1 : t.side === W ? 0 : t.index, y: t.side === S ? h - 1 : t.side === N ? 0 : t.index };
  }
  function makeBoard(level) {
    var cells = [];
    for (var i = 0; i < level.w * level.h; i++) cells.push(null);
    level.fixed.forEach(function (f) { cells[f.y * level.w + f.x] = { key: f.key, fixed: true }; });
    return { level: level, cells: cells };
  }
  function inBounds(b, x, y) { return x >= 0 && y >= 0 && x < b.level.w && y < b.level.h; }
  function at(b, x, y) { return inBounds(b, x, y) ? b.cells[y * b.level.w + x] : null; }
  function isTunnel(b, t, x, y, side) { var c = tunnelCell(t, b.level.w, b.level.h); return t.side === side && c.x === x && c.y === y; }
  function hasTunnel(b, x, y, side) { return isTunnel(b, b.level.entrance, x, y, side) || isTunnel(b, b.level.exit, x, y, side); }

  // Legality.Classify
  function classify(b, x, y) {
    return DIRS.map(function (d) {
      var nx = x + DX[d], ny = y + DY[d];
      if (!inBounds(b, nx, ny)) return hasTunnel(b, x, y, d) ? "Forced" : "Forbidden";
      var n = at(b, nx, ny);
      if (n) return has(n.key, opposite(d)) ? "Forced" : "Forbidden";
      return "Open";
    });
  }
  function isLegal(classes, key) {
    var a = CONN[key][0], c = CONN[key][1];
    if (classes[a] === "Forbidden" || classes[c] === "Forbidden") return false;
    return DIRS.every(function (d) { return classes[d] !== "Forced" || d === a || d === c; });
  }
  function legalKeys(b, x, y) {
    if (!inBounds(b, x, y) || at(b, x, y)) return [];
    var classes = classify(b, x, y);
    if (classes.filter(function (c) { return c === "Forced"; }).length > 2) return [];
    return KEYS.filter(function (k) { return isLegal(classes, k); });
  }
  // PathFinder.TryFindPath
  function findPath(b) {
    var path = [], seen = {};
    var c = tunnelCell(b.level.entrance, b.level.w, b.level.h), x = c.x, y = c.y, from = b.level.entrance.side;
    for (;;) {
      var p = at(b, x, y);
      if (!p || !has(p.key, from) || seen[x + "," + y]) return { ok: false, path: path };
      seen[x + "," + y] = true; path.push([x, y]);
      var next = other(p.key, from);
      if (isTunnel(b, b.level.exit, x, y, next)) return { ok: true, path: path };
      x += DX[next]; y += DY[next]; from = opposite(next);
      if (!inBounds(b, x, y)) return { ok: false, path: path };
    }
  }
  // WinChecker.Evaluate
  function evaluate(b) {
    var w = b.level.w, h = b.level.h, rows = [], cols = [], x, y, clues = true, noOpen = true;
    for (y = 0; y < h; y++) { var r = 0; for (x = 0; x < w; x++) if (at(b, x, y)) r++; rows.push(r); if (r !== b.level.rows[y]) clues = false; }
    for (x = 0; x < w; x++) { var c = 0; for (y = 0; y < h; y++) if (at(b, x, y)) c++; cols.push(c); if (c !== b.level.cols[x]) clues = false; }
    for (y = 0; y < h; y++) for (x = 0; x < w; x++) {
      var p = at(b, x, y); if (!p) continue;
      CONN[p.key].forEach(function (d) {
        var nx = x + DX[d], ny = y + DY[d];
        if (!inBounds(b, nx, ny)) { if (!hasTunnel(b, x, y, d)) noOpen = false; return; }
        var n = at(b, nx, ny);
        if (!n || !has(n.key, opposite(d))) noOpen = false;
      });
    }
    var path = findPath(b);
    return { pathConnected: path.ok, cluesSatisfied: clues, noOpenEnds: noOpen, isWin: path.ok && clues && noOpen, rows: rows, cols: cols };
  }

  // The Plan Example from Assets/99.Test/EditMode/TestLevels.cs, with its hand-verified unique solution.
  var PLAN = {
    w: 6, h: 6, cols: [2, 3, 1, 1, 3, 1], rows: [0, 0, 4, 3, 4, 0],
    fixed: [{ x: 1, y: 2, key: "SW" }, { x: 4, y: 4, key: "NW" }],
    entrance: { side: W, index: 3 }, exit: { side: E, index: 2 }
  };
  var PLAN_SOLUTION = [[0, 3, "NW"], [0, 2, "SE"], [1, 2, "SW"], [1, 3, "NS"], [1, 4, "NE"], [2, 4, "EW"], [3, 4, "EW"], [4, 4, "NW"], [4, 3, "NS"], [4, 2, "SE"], [5, 2, "EW"]];

  function el(tag, cls, text) { var n = document.createElement(tag); if (cls) n.className = cls; if (text != null) n.textContent = text; return n; }
  function railSvg(key) {
    // 34-unit cell; side midpoints; curves are quarter circles about the shared corner, as TrackCurve does it.
    var mid = { 0: [17, 0], 1: [34, 17], 2: [17, 34], 3: [0, 17] };
    var a = CONN[key][0], c = CONN[key][1], d;
    if (a === opposite(c)) d = "M" + mid[a] + " L" + mid[c];
    else {
      var corner = [mid[a][0] + mid[c][0] - 17, mid[a][1] + mid[c][1] - 17];
      var cross = (mid[a][0] - corner[0]) * (mid[c][1] - corner[1]) - (mid[a][1] - corner[1]) * (mid[c][0] - corner[0]);
      d = "M" + mid[a] + " A17 17 0 0 " + (cross > 0 ? 1 : 0) + " " + mid[c];
    }
    return '<svg viewBox="0 0 34 34" aria-hidden="true"><path class="rail" d="' + d + '"/></svg>';
  }

  // ---------------------------------------------------------------- demo 1: legality and win check
  function legalityDemo(host) {
    var board = makeBoard(PLAN), sel = null;
    var grid = el("div", "board"), side = el("div"), banner = el("p", "banner");
    grid.style.gridTemplateColumns = "repeat(" + (PLAN.w + 3) + ", 34px)";
    var row = el("div", "demo-row");
    var left = el("div"); left.appendChild(grid);
    var buttons = el("div", "keys");
    var solve = el("button", "act ghost", "Lay the solution"); solve.type = "button";
    var reset = el("button", "act ghost", "Reset"); reset.type = "button";
    buttons.appendChild(solve); buttons.appendChild(reset);
    buttons.style.marginTop = "12px";
    left.appendChild(buttons);
    row.appendChild(left); row.appendChild(side);
    host.appendChild(row); host.appendChild(banner);

    solve.addEventListener("click", function () {
      board = makeBoard(PLAN);
      PLAN_SOLUTION.forEach(function (s) { if (!at(board, s[0], s[1])) board.cells[s[1] * PLAN.w + s[0]] = { key: s[2], fixed: false }; });
      sel = null; draw();
    });
    reset.addEventListener("click", function () { board = makeBoard(PLAN); sel = null; draw(); });

    function draw() {
      var result = evaluate(board);
      grid.innerHTML = "";
      // top row: column clues
      grid.appendChild(el("span")); // west tunnel column
      for (var x = 0; x < PLAN.w; x++) {
        var c = el("span", "clue" + (result.cols[x] === PLAN.cols[x] ? " met" : result.cols[x] > PLAN.cols[x] ? " over" : ""), String(PLAN.cols[x]));
        grid.appendChild(c);
      }
      grid.appendChild(el("span")); grid.appendChild(el("span"));
      for (var y = 0; y < PLAN.h; y++) {
        grid.appendChild(PLAN.entrance.side === W && PLAN.entrance.index === y ? el("span", "tun", "S") : el("span"));
        for (x = 0; x < PLAN.w; x++) (function (x, y) {
          var p = at(board, x, y);
          var b = el("button", "cell" + (p ? (p.fixed ? " fixed" : " player") : "") + (sel && sel[0] === x && sel[1] === y ? " sel" : ""));
          b.type = "button";
          b.setAttribute("aria-label", "Cell " + x + "," + y + (p ? " " + p.key + (p.fixed ? " fixed" : "") : " empty"));
          if (p) b.innerHTML = railSvg(p.key);
          b.addEventListener("click", function () {
            if (p && !p.fixed) { board.cells[y * PLAN.w + x] = null; sel = [x, y]; }   // Board.TryErase
            else if (!p) sel = [x, y];
            draw();
          });
          grid.appendChild(b);
        })(x, y);
        grid.appendChild(el("span", "clue" + (result.rows[y] === PLAN.rows[y] ? " met" : result.rows[y] > PLAN.rows[y] ? " over" : ""), String(PLAN.rows[y])));
        grid.appendChild(PLAN.exit.side === E && PLAN.exit.index === y ? el("span", "tun", "E") : el("span"));
      }

      side.innerHTML = "";
      if (sel && !at(board, sel[0], sel[1])) {
        var classes = classify(board, sel[0], sel[1]);
        side.appendChild(el("p", null, "Legality.Classify(board, " + sel[0] + ", " + sel[1] + ")"));
        var list = el("ul", "sides");
        DIRS.forEach(function (d) { var li = el("li", classes[d], DIR_NAME[d] + ": " + classes[d]); list.appendChild(li); });
        side.appendChild(list);
        var keys = legalKeys(board, sel[0], sel[1]);
        side.appendChild(el("p", null, "LegalKeys: " + (keys.length ? keys.length + (keys.length === 1 ? " key" : " keys") : "none")));
        var box = el("div", "keys");
        keys.forEach(function (k) {
          var b = el("button", "act", k); b.type = "button";
          b.addEventListener("click", function () { board.cells[sel[1] * PLAN.w + sel[0]] = { key: k, fixed: false }; draw(); });
          box.appendChild(b);
        });
        side.appendChild(box);
      } else {
        side.appendChild(el("p", null, "Pick an empty cell to classify its four sides. Click a green rail to lift it; grey rails are fixed."));
      }
      var pre = el("p", "readout", "PathConnected: " + result.pathConnected + "\nCluesSatisfied: " + result.cluesSatisfied + "\nNoOpenEnds: " + result.noOpenEnds + "\nIsWin: " + result.isWin);
      side.appendChild(pre);
      banner.hidden = !result.isWin;
      banner.className = "banner win";
      banner.textContent = "WinChecker.Evaluate says IsWin. In the game this is where GameFlow.CompleteLevel starts the train.";
    }
    draw();
  }

  // ---------------------------------------------------------------- demo 2: track bend
  function bendDemo(host) {
    var canvas = el("canvas"); canvas.width = 440; canvas.height = 440; canvas.style.maxWidth = "100%"; canvas.style.height = "auto";
    canvas.setAttribute("role", "img"); canvas.setAttribute("aria-label", "A strip of track bent round a quarter circle");
    var controls = el("div"); controls.style.flex = "1 1 300px"; controls.style.display = "grid"; controls.style.gap = "12px";
    function slider(label, min, max, step, value, id) {
      var wrap = el("label"); var text = el("span", null, label);
      var input = el("input"); input.type = "range"; input.min = min; input.max = max; input.step = step; input.value = value; input.id = id;
      wrap.appendChild(text); wrap.appendChild(input); controls.appendChild(wrap);
      return { input: input, text: text, label: label };
    }
    var slices = slider("Curve slices", 1, 16, 1, 3, "bend-slices");
    var half = slider("Half width of the art (cells)", 0.1, 0.6, 0.01, 0.35, "bend-half");
    var out = el("p", "readout"); controls.appendChild(out);
    var row = el("div", "demo-row"); row.appendChild(canvas); row.appendChild(controls); host.appendChild(row);

    var R = 0.5;
    function css(name) { return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || "#888"; }
    function draw() {
      var n = parseInt(slices.input.value, 10), hw = parseFloat(half.input.value);
      slices.text.textContent = slices.label + ": " + n;
      half.text.textContent = half.label + ": " + hw.toFixed(2);
      var g = canvas.getContext("2d"), size = canvas.width, pad = 40, scale = size - pad * 2;
      g.clearRect(0, 0, size, size);
      // The cell: x east, z north, origin at the centre. Curve NE: from North side midpoint to East side midpoint,
      // a quarter circle about the shared corner (0.5, 0.5).
      function px(x, z) { return [pad + (x + 0.5) * scale, pad + (0.5 - z) * scale]; }
      g.strokeStyle = css("--closed"); g.lineWidth = 1; g.strokeRect(pad, pad, scale, scale);
      var cx = 0.5, cz = 0.5, phi0 = Math.PI, delta = Math.PI / 2;     // from (0, 0.5) round to (0.5, 0)
      function bend(t, x) {
        var phi = phi0 + delta * t;
        var p = [cx + R * Math.cos(phi), cz + R * Math.sin(phi)];
        var f = [-Math.sin(phi), Math.cos(phi)];
        var right = [f[1], -f[0]];
        return [p[0] + right[0] * x, p[1] + right[1] * x];
      }
      // the bent strip, sliced into n bands along its length: each band stays a straight-sided quad
      for (var i = 0; i < n; i++) {
        var t0 = i / n, t1 = (i + 1) / n;
        var q = [bend(t0, -hw), bend(t1, -hw), bend(t1, hw), bend(t0, hw)].map(function (p) { return px(p[0], p[1]); });
        g.beginPath(); g.moveTo(q[0][0], q[0][1]); for (var k = 1; k < 4; k++) g.lineTo(q[k][0], q[k][1]); g.closePath();
        g.fillStyle = css("--line-soft"); g.fill(); g.strokeStyle = css("--line"); g.lineWidth = 2; g.stroke();
      }
      // the true centre line
      g.beginPath();
      for (var s = 0; s <= 64; s++) { var c = px.apply(null, bend(s / 64, 0)); if (s) g.lineTo(c[0], c[1]); else g.moveTo(c[0], c[1]); }
      g.strokeStyle = css("--ink"); g.lineWidth = 2; g.setLineDash([6, 5]); g.stroke(); g.setLineDash([]);
      // arc centre
      var corner = px(cx, cz); g.fillStyle = css("--stop"); g.beginPath(); g.arc(corner[0], corner[1], 5, 0, Math.PI * 2); g.fill();

      // TrackMeshBender's Jacobian, k = 1 + curvature * x. This NE curve turns left, so curvature is +1/R and the
      // arc centre is to the LEFT of travel: the inner edge is x = -hw.
      var curvature = 1 / R;
      var inner = 1 + curvature * (-hw);
      var outer = 1 + curvature * (hw);
      var length = Math.PI * R / 2;
      out.textContent =
        "curve length      " + length.toFixed(4) + "  (pi/4 of a cell)\n" +
        "k at inner edge   " + Math.max(0, inner).toFixed(2) + (inner <= 0.02 ? "   <- folded onto the arc centre" : "") + "\n" +
        "k at outer edge   " + outer.toFixed(2) + "\n" +
        "facets on the arc " + n + (n < 6 ? "   <- reads as a polyline" : "");
    }
    slices.input.addEventListener("input", draw);
    half.input.addEventListener("input", draw);
    draw();
  }

  // ---------------------------------------------------------------- demo 3: Unity cell -> Unreal centimetres
  function unitsDemo(host) {
    var fields = {};
    var row = el("div", "demo-row");
    [["x", "Column x", 1], ["y", "Row y", 2], ["w", "Board width", 6], ["h", "Board height", 6], ["cm", "Cell size, cm", 6]].forEach(function (f) {
      var wrap = el("label"); wrap.appendChild(el("span", null, f[1]));
      var input = el("input"); input.type = "number"; input.value = f[2]; input.id = "units-" + f[0]; input.step = f[0] === "cm" ? "0.5" : "1";
      wrap.appendChild(input); row.appendChild(wrap); fields[f[0]] = input;
      input.addEventListener("input", draw);
    });
    var out = el("p", "readout"); out.style.marginTop = "14px";
    host.appendChild(row); host.appendChild(out);
    function draw() {
      var x = +fields.x.value, y = +fields.y.value, w = +fields.w.value, h = +fields.h.value, cm = +fields.cm.value;
      var ux = x - (w - 1) / 2, uz = (h - 1) / 2 - y;                 // BoardLayout.CellCenter
      out.textContent =
        "BoardLayout.CellCenter(" + x + ", " + y + ", " + w + ", " + h + ")\n" +
        "  Unity, board space        (x " + ux.toFixed(2) + ", y 0, z " + uz.toFixed(2) + ")   one unit per cell\n" +
        "  Unreal, board local       (X " + (uz * 100).toFixed(0) + ", Y " + (ux * 100).toFixed(0) + ", Z 0)   100 units per cell\n" +
        "  Board root scale          " + (cm / 100).toFixed(3) + "\n" +
        "  Unreal, world from root   (X " + (uz * cm).toFixed(1) + " cm, Y " + (ux * cm).toFixed(1) + " cm)\n" +
        "\nUnity z (north) becomes Unreal X (forward). Unity x (east) becomes Unreal Y (right). Unity y (up) becomes Unreal Z.";
    }
    draw();
  }

  function mount(id, fn) { var host = document.getElementById(id); if (host) fn(host); }
  mount("demo-legality", legalityDemo);
  mount("demo-bend", bendDemo);
  mount("demo-units", unitsDemo);

  // exposed for the one check that runs outside a browser
  if (typeof module !== "undefined") module.exports = { makeBoard: makeBoard, evaluate: evaluate, legalKeys: legalKeys, PLAN: PLAN, PLAN_SOLUTION: PLAN_SOLUTION, classify: classify };
})();
