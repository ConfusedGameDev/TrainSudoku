/* Tsugi → Unreal course: page chrome, progress, code blocks, quizzes. No build step; every page loads this file. */
(function () {
  "use strict";

  var LESSONS = [
    { slug: "00-orientation", title: "Orientation", sub: "What you are porting, and how Unreal thinks" },
    { slug: "01-toolchain", title: "Toolchain and first build", sub: "Engine, Android, Meta plugin, an empty app on the headset" },
    { slug: "02-cpp-types", title: "Unreal C++ through the basic types", sub: "Direction, PieceKey, Piece, Tunnel, LevelData" },
    { slug: "03-rules-tests", title: "Rules and automated tests", sub: "Board, Legality, WinChecker, PathFinder, Solver" },
    { slug: "04-data-assets", title: "Data assets and the level pipeline", sub: "216 levels from Unity into Unreal" },
    { slug: "05-game-flow", title: "Game framework and flow", sub: "GameFlow, timer, progress, subsystems, delegates" },
    { slug: "06-saving", title: "Saving", sub: "save.json, atomic writes, preferences" },
    { slug: "07-board-world", title: "The board in the world", sub: "Actors, components, units, materials" },
    { slug: "08-procedural-geometry", title: "Procedural geometry", sub: "Bending track along a curve" },
    { slug: "09-xr-foundation", title: "XR foundation", sub: "Pawn, hands, controllers, passthrough" },
    { slug: "10-board-in-room", title: "The board in the room", sub: "Planes, anchors, occlusion, the handle" },
    { slug: "11-tray-grab", title: "Tray and grab", sub: "PieceDrop, ghost, physics, steam" },
    { slug: "12-signage-maps", title: "Signage and maps", sub: "World-space UMG pressed with a fingertip" },
    { slug: "13-flow-train", title: "Flow on the platform and the train run", sub: "Wiring it together; TrackPath" },
    { slug: "14-wrist-pause", title: "Wrist menu and pause", sub: "Hand joints, focus loss, settings" },
    { slug: "15-tutorial", title: "Tutorial", sub: "The coach, the gate, the callout" },
    { slug: "16-sound-haptics-language", title: "Sound, haptics, language", sub: "Cues in the room, four locales" },
    { slug: "17-performance", title: "Performance on Quest", sub: "90 Hz on Quest 3, never under 72 on 3S" },
    { slug: "18-shipping", title: "Shipping to Meta Standalone", sub: "Signing, manifest, store checks" }
  ];

  var KEY = "tsugi-ue-course:v1";
  function load() {
    try { return JSON.parse(localStorage.getItem(KEY) || "{}") || {}; } catch (e) { return {}; }
  }
  function save(state) {
    try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) { /* progress is a convenience */ }
  }
  var state = load();
  state.checks = state.checks || {};
  state.totals = state.totals || {};
  state.quiz = state.quiz || {};

  function el(tag, attrs, children) {
    var node = document.createElement(tag);
    if (attrs) Object.keys(attrs).forEach(function (k) {
      if (k === "class") node.className = attrs[k];
      else if (k === "text") node.textContent = attrs[k];
      else if (k === "html") node.innerHTML = attrs[k];
      else node.setAttribute(k, attrs[k]);
    });
    (children || []).forEach(function (c) { if (c) node.appendChild(typeof c === "string" ? document.createTextNode(c) : c); });
    return node;
  }
  function pad(n) { return n < 10 ? "0" + n : "" + n; }
  function done(n) {
    var total = state.totals[n] || 0;
    if (!total) return 0;
    var count = 0;
    for (var i = 0; i < total; i++) if (state.checks[n + ":" + i]) count++;
    return count;
  }

  var host = document.getElementById("course");
  if (!host) return;
  var root = host.getAttribute("data-root") || "";
  var current = host.hasAttribute("data-lesson") ? parseInt(host.getAttribute("data-lesson"), 10) : -1;
  var home = root + "index.html";
  function href(n) { return root + "lessons/" + LESSONS[n].slug + ".html"; }

  // ---- LED strip
  var led = el("header", { class: "led" }, [
    el("a", { href: home, text: "TSUGI · Unity to Unreal 5.8" }),
    el("span", { class: "dim", text: current >= 0 ? "Station UE " + pad(current) + " of " + pad(LESSONS.length - 1) : "Meta Quest standalone" })
  ]);

  // ---- line map
  function lineMap() {
    var list = el("ol");
    LESSONS.forEach(function (lesson, n) {
      var total = state.totals[n] || 0, got = done(n);
      var li = el("li", { class: (n === current ? "here " : "") + (total && got === total ? "done" : "") }, [
        el("a", { href: href(n) }, [
          el("span", { class: "dot", text: pad(n) }),
          el("span", null, [lesson.title, el("span", { class: "meta", text: total ? got + " / " + total + " checks" : lesson.sub })])
        ])
      ]);
      list.appendChild(li);
    });
    var nav = el("details", { class: "linemap" }, [el("summary", { text: "The line · " + LESSONS.length + " stations" }), list]);
    if (window.matchMedia && window.matchMedia("(min-width: 901px)").matches) nav.setAttribute("open", "");
    return nav;
  }

  var article = document.querySelector("article.lesson");
  var frame = el("div", { class: "frame" });
  host.appendChild(led);
  host.appendChild(frame);
  var nav = lineMap();
  frame.appendChild(nav);
  if (article) frame.appendChild(article);

  // ---- code blocks: <script type="text/plain" data-lang="cpp" data-title="…"> so C++ needs no HTML escaping
  function copyButton(getText) {
    var button = el("button", { class: "copy", type: "button", text: "Copy" });
    button.addEventListener("click", function () {
      var text = getText();
      var ok = function () { button.textContent = "Copied"; setTimeout(function () { button.textContent = "Copy"; }, 1400); };
      if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).then(ok, function () { button.textContent = "Select and copy"; });
      else button.textContent = "Select and copy";
    });
    return button;
  }
  function pre(source) {
    var text = source.textContent.replace(/^\s*\n/, "").replace(/\s+$/, "");
    var code = el("code", { class: "language-" + (source.getAttribute("data-lang") || "plaintext") });
    code.textContent = text;
    if (window.hljs) { try { window.hljs.highlightElement(code); } catch (e) { /* plain text is fine */ } }
    return el("pre", null, [code]);
  }
  function buildCode(sources, into) {
    var box = el("div", { class: "code" });
    var panes = sources.map(pre);
    var shown = 0;
    var head = el("div", { class: "code-head" });
    if (sources.length > 1) {
      var tabs = el("div", { class: "code-tabs", role: "tablist" });
      var buttons = sources.map(function (s, i) {
        var b = el("button", { type: "button", role: "tab", text: s.getAttribute("data-title") || s.getAttribute("data-lang") });
        b.addEventListener("click", function () { show(i); });
        tabs.appendChild(b);
        return b;
      });
      var show = function (i) {
        shown = i;
        panes.forEach(function (p, j) { p.hidden = j !== i; });
        buttons.forEach(function (b, j) { b.setAttribute("aria-selected", j === i ? "true" : "false"); });
      };
      head.appendChild(tabs);
      // Unreal is what the reader is writing, so it opens first when a pair is given.
      var start = 0;
      sources.forEach(function (s, i) { if (/unreal/i.test(s.getAttribute("data-title") || "")) start = i; });
      show(start);
    } else {
      head.appendChild(el("span", { class: "code-label", text: sources[0].getAttribute("data-title") || sources[0].getAttribute("data-lang") || "" }));
    }
    head.appendChild(copyButton(function () { return panes[shown].textContent; }));
    box.appendChild(head);
    panes.forEach(function (p) { box.appendChild(p); });
    into.parentNode.replaceChild(box, into);
  }
  Array.prototype.slice.call(document.querySelectorAll(".tabs")).forEach(function (group) {
    var sources = Array.prototype.slice.call(group.querySelectorAll('script[type="text/plain"]'));
    if (sources.length) buildCode(sources, group);
  });
  Array.prototype.slice.call(document.querySelectorAll('script[type="text/plain"]')).forEach(function (s) { buildCode([s], s); });

  // ---- acceptance checks
  if (current >= 0) {
    var items = Array.prototype.slice.call(document.querySelectorAll("ul.checks > li"));
    state.totals[current] = items.length;
    var status = el("p", { class: "verified" });
    var refresh = function () {
      var got = done(current);
      status.textContent = got === items.length && items.length ? "Verified by human: all " + got + " checks ticked. This station is done." : "Verified by human: " + got + " of " + items.length + " ticked.";
      status.className = "verified" + (got === items.length && items.length ? " all" : "");
    };
    items.forEach(function (li, i) {
      var id = "check-" + current + "-" + i;
      var box = el("input", { type: "checkbox", id: id });
      box.checked = !!state.checks[current + ":" + i];
      var label = el("label", { for: id });
      var body = el("span");
      while (li.firstChild) body.appendChild(li.firstChild);
      label.appendChild(box);
      label.appendChild(body);
      li.appendChild(label);
      if (box.checked) li.classList.add("ticked");
      box.addEventListener("change", function () {
        state.checks[current + ":" + i] = box.checked;
        li.classList.toggle("ticked", box.checked);
        save(state);
        refresh();
      });
    });
    var lists = document.querySelectorAll("ul.checks");
    if (lists.length) lists[lists.length - 1].parentNode.insertBefore(status, lists[lists.length - 1].nextSibling);
    refresh();
    save(state);
  }

  // ---- quizzes: <div class="quiz"><p class="q">…</p><ul><li data-correct>…</li><li>…</li></ul><p class="why">…</p></div>
  Array.prototype.slice.call(document.querySelectorAll(".quiz")).forEach(function (quiz, q) {
    var id = current + ":" + q;
    var why = quiz.querySelector(".why");
    if (why) why.hidden = true;
    var buttons = [];
    var answer = function (picked, remember) {
      buttons.forEach(function (b) {
        b.button.disabled = true;
        if (b.correct) b.button.classList.add("right");
        else if (b.index === picked) b.button.classList.add("wrong");
      });
      if (why) why.hidden = false;
      if (remember) { state.quiz[id] = picked; save(state); }
    };
    Array.prototype.slice.call(quiz.querySelectorAll("li")).forEach(function (li, i) {
      var button = el("button", { type: "button" });
      while (li.firstChild) button.appendChild(li.firstChild);
      li.appendChild(button);
      buttons.push({ button: button, correct: li.hasAttribute("data-correct"), index: i });
      button.addEventListener("click", function () { answer(i, true); });
    });
    if (typeof state.quiz[id] === "number") answer(state.quiz[id], false);
  });

  // ---- prev / next
  if (current >= 0 && article) {
    var pager = el("nav", { class: "pager", "aria-label": "Lessons" });
    if (current > 0) pager.appendChild(el("a", { href: href(current - 1) }, [el("small", { text: "Previous station" }), LESSONS[current - 1].title]));
    else pager.appendChild(el("a", { href: home }, [el("small", { text: "Back to" }), "Course home"]));
    if (current < LESSONS.length - 1) pager.appendChild(el("a", { class: "next", href: href(current + 1) }, [el("small", { text: "Next station" }), LESSONS[current + 1].title]));
    else pager.appendChild(el("a", { class: "next", href: home }, [el("small", { text: "Terminus" }), "Back to course home"]));
    article.appendChild(pager);
  }

  // ---- home: station list and overall meter
  var stations = document.getElementById("stations");
  if (stations) {
    var totalChecks = 0, totalDone = 0, started = 0;
    LESSONS.forEach(function (lesson, n) {
      var total = state.totals[n] || 0, got = done(n);
      totalChecks += total; totalDone += got; if (got) started++;
      stations.appendChild(el("li", { class: total && got === total ? "done" : "" }, [
        el("a", { href: href(n) }, [
          el("span", { class: "code-badge", text: "UE " + pad(n) }),
          el("span", null, [el("b", { text: lesson.title }), el("span", { class: "sub", text: lesson.sub })]),
          el("span", { class: "prog", text: total ? (got === total ? "Verified" : got + " / " + total) : "Not opened" })
        ])
      ]));
    });
    var meter = document.getElementById("meter");
    var label = document.getElementById("meter-label");
    if (meter) meter.style.width = (totalChecks ? Math.round(100 * totalDone / totalChecks) : 0) + "%";
    if (label) label.textContent = totalChecks
      ? totalDone + " of " + totalChecks + " checks ticked across the lessons you have opened. Progress is kept in this browser only."
      : "Nothing ticked yet. Progress is kept in this browser only.";
  }

  window.TsugiCourse = { lessons: LESSONS };
})();
