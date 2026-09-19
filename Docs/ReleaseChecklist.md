# Tsugi — Release Checklist

v1.0 · 19 SEP 2026 · iOS first, Android second

Copy for every store field is in `Docs/StoreListing.md`. This file is the *order of operations* and the
decisions that are easy to get wrong once and then live with for years.

---

## 0 · The things that are one-way doors

Do these consciously, now. Every one of them is cheap today and expensive or impossible after launch.

| # | Door | Why it only swings one way |
|---|---|---|
| 1 | **Bundle ID `com.GorillaGonzalez.Tsugi`** | Cannot be changed after the first upload, ever. A new bundle ID is a new app with zero reviews. Check the capitalisation is what you want *before* you create the App Store Connect record — it is case-sensitive and permanent. |
| 2 | **The flat level order in `Network.asset`** | Already documented in CLAUDE.md: the flat index is the save-file identity, so lines may only be appended. After release, reordering silently rewrites every player's progress. |
| 3 | **Paid → freemium** | You mentioned possibly moving to free-with-a-daily-level plus a $2.99 unlock. Apple forbids taking content from people who already paid. Anyone who buys v1 at $0.99 must keep all 216 stations forever. **Done:** `save.json` is now version 3 and carries `purchasedFullVersion`. An *absent* flag reads as `true` permanently — a file without it was written by a build that predates it, and every such build was paid, so the migration is a fact rather than a guess. The freemium build's one job is to set it `false` explicitly for saves it creates itself; `SaveData.PurchasedFullVersion` says so in its remarks. |
| 4 | **Selling at $0.99 in v1** | Paid → free later is fine. Free → paid enrages your early adopters. You have chosen the safe direction. |
| 5 | **App name "Tsugi: Railway Puzzle"** | Changeable, but only with a new version submission, and it resets some of your search ranking. Worth being happy with now. |

---

## 1 · Screenshots

### What the stores actually demand

Apple now requires **only the largest size in each device family** and scales the rest down itself.
Because the Xcode export is Universal (`targetDevice: 2`), the iPad set is not optional.

| Slot | Exact pixels (portrait) | Required? | How we get it |
|---|---|---|---|
| iPhone 6.9" | **1320 × 2868** | Yes | **Your iPhone 16 Pro Max screenshots are natively exactly this.** Nothing to scale. |
| iPad 13" | **2064 × 2752** | Yes — the app runs on iPad | Unity Editor Game view (below), or the iPad simulator |
| Google Play phone | 1080 × 1920 min, 9:16 | 2 min, 4+ for recommendation eligibility | Downscale the 1320 × 2868 set |
| Google Play feature graphic | **1024 × 500**, no alpha | Yes, Play won't publish without it | Composed from the logo + a board render |
| App icon | 1024 × 1024 (Apple), 512 × 512 (Play) | Yes | Already in `Assets/02.Graphics/Ui/Icon/` |

Constraints that reject an upload: **no alpha channel**, PNG or JPEG, 1–10 per size on Apple, max 8
per device type on Play.

> **"Wrong dimensions" from App Store Connect does not necessarily mean the dimensions are wrong.**
> This cost a round trip: a set of 1320×2868 PNGs — a size Apple explicitly lists for the 6.9" slot —
> was refused for its dimensions. The files were **16-bit-per-channel and Display P3**, because an
> iPhone HEIC is 10-bit HDR and `sips -s format png` promotes it rather than flattening it. ASC wants
> 8-bit sRGB and reports anything else as a dimension fault. Convert with an explicit profile match
> and to a format that cannot carry 16 bits:
> `sips -s format jpeg -s formatOptions best --matchTo "/System/Library/ColorSync/Profiles/sRGB Profile.icc" in.HEIC --out out.jpg`
> Always convert **from the original**, never from an intermediate PNG, and check `bitsPerSample` and
> `profile` alongside the pixel size — `sips -g all` shows all of them.
>
> The same applies to Simulator's own ⌘S screenshots: they are 8-bit but carry an **alpha channel**,
> so they are rejected until converted. Tsugi hides the status bar (`UIStatusBarHidden: true`), so device captures
come out clean with no carrier/battery furniture to edit out — a small gift.

### Your shot list — capture these on the phone

Order matters more than people expect: the App Store search results page shows only the **first two
or three**, so the mechanic has to be legible there, before anyone taps through.

| # | Screen | State to capture it in | What it has to prove |
|---|---|---|---|
| 1 | **Play, mid-solve** | A board roughly two-thirds laid, on a good-looking line, with at least one clue already green | The whole game in one frame: grid, numbers, curved track |
| 2 | **Play, cell selected** | Tap an empty cell so the green / red / line-colour neighbour markers are all visible at once | *How you play* — this is the one that answers "what do I actually do" |
| 3 | **Arrival card, 3 stars** | Finish a station fast enough for three | The reward, and the clock |
| 4 | **Network map** | Tick the Game object's **Debug: line unlocks** boxes **in order** so a good spread of lines is drawn | Scale — "216 stations" as a picture rather than a claim |
| 5 | **Train running** | Mid-run, train part way along the finished route, ideally entering a tunnel | Motion and payoff |
| 6 | **Line map** | Any line with a few stations starred | Progression |

Optional 7th: the tutorial callout, if you want to signal "this teaches you".

**How to capture:** press Volume Up + Side button on the phone, then AirDrop the PNGs to the Mac, or
put them anywhere and tell me the folder. Do **not** let them go through anything that re-encodes —
no Photos "optimised" export, no WhatsApp, no email compression. AirDrop preserves the file exactly.
Then I crop/pad nothing (they're already correct), order them, and build the captioned versions.

**Captions:** plain gameplay screenshots are allowed and some premium puzzle games ship exactly that.
But a short caption bar above each frame measurably lifts conversion, and costs us nothing since I
compose them anyway. Proposed captions, one per shot: *"Every row and column tells you how many."* ·
*"Tap a cell. It shows you where you may connect."* · *"Beat the clock, or ignore it."* ·
*"24 lines. 216 stations."* · *"Then watch it run."* · *"Star every station to open the next line."*

### The iPad set, which you can't shoot

Two routes, and they differ in fidelity:

- **Unity Editor Game view** (fast, good enough): run `Window > TrainSudoku > Screenshots > Add Store
  Game View Sizes`, pick `Store iPad 13` in the Game view dropdown, enter play mode, and
  press **F12** at each screen. Output lands in `StoreScreenshots/` at the project root. Caveat: the
  Editor render is not bit-identical to device — safe-area insets and the mobile URP asset can differ.
- **iPad simulator** (slow, exact — and the route being taken): `BuildScript` now takes `-simulator`,
  which flips the SDK, exports to `Builds-Simulator/`, and flips the SDK back on the next device
  build. The **iPad Pro 13-inch (M5)** simulator on the iOS 26.5 runtime captures natively at
  **2064×2752**, verified — bit-for-bit the store's 13-inch slot. No `sudo` is needed: setting
  `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer` per command reaches the full toolchain
  without changing system state. This is the real renderer at the real resolution, and it also
  **tells you whether the iPad layout is acceptable at all** — which matters, because Apple's
  reviewer opens it on an iPad and "phone UI stretched to 13 inches" is a real rejection reason.

  **Every simulator capture must have its alpha stripped before upload.** `simctl io screenshot`
  writes PNG *with* an alpha channel and App Store Connect rejects those, naming the file rather than
  the cause. Convert with `sips -s format jpeg -s formatOptions best in.png --out out.jpg`; Apple
  accepts .jpg. Do **not** reach for `sips -s format png --setProperty hasAlpha false` — it is
  accepted silently and writes nothing at all.

  **Build it with `-scheme Unity-iPhone -destination "platform=iOS Simulator,id=<UDID>"`.** Both
  `-derivedDataPath` and `-destination` require a scheme — xcodebuild refuses outright with *"The flag
  -scheme, -testProductsPath, or -xctestrun is required when specifying -derivedDataPath"* — so the
  `-target` form cannot be combined with either.

  **A simulator build must be arm64, and Unity defaults it to x86_64.** `iOSSimulatorArchitecture` in
  Player Settings is `0` (x86_64); on an Apple Silicon Mac every simulator is arm64, so the export
  builds, links and then fails at *install* with a misleading message — *"Tsugi Needs to Be Updated /
  This app needs to be updated by the developer to work on this version of iPadOS"*, whose actual
  cause is the line under it: `Failed to find matching arch`. Forcing `ARCHS=arm64` on xcodebuild
  does **not** fix it, because Unity ships its own prebuilt simulator libraries (`baselib.a`,
  `lib_burst_generated.a`) with only the slice that setting asked for — so the link would fail
  instead. The setting has to change in Unity and the export be redone. Values: `0` x86_64, `1`
  arm64, `2` universal.

  **Set it with `PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64`
  — not with `SetPropertyInt`.** `PlayerSettings.SetPropertyInt("iOSSimulatorArchitecture", 1, …)`
  compiles, runs, throws nothing and changes nothing: it silently no-ops on a property name it does
  not recognise, and the serialized YAML key is not the name it wants. A build driven through it came
  out x86_64 with the setting still at `0`, while the build log cheerfully reported arm64 — because
  the log echoed the intention instead of reading the value back. **Log settings by reading them, not
  by restating them**; that one habit is the difference between catching this in seconds and shipping
  an uninstallable build. The typed property is public, static and settable, and the enum is public
  (`X86_64 = 0`, `ARM64 = 1`, `Universal = 2`), both confirmed by reflecting over `UnityEditor.dll`.

  The wrinkle to know about: **schemes are generated, not authored, so they are not guaranteed to be
  there.** A fresh export lists `GameAssembly`, `Unity-iPhone` and `UnityFramework`, but the older
  device export in `Builds/` lists only `GameAssembly` — the IL2CPP static library, not the app. The
  *targets* are identical in both. So if the scheme is missing, `xcodebuild -list` will say so, and
  the fallback is `-target Unity-iPhone` with an explicit `CONFIGURATION_BUILD_DIR` instead of
  `-derivedDataPath`. Signing is turned off with `CODE_SIGNING_ALLOWED=NO`: correct rather than a
  workaround, since a simulator build needs no signature and `appleDeveloperTeamID` is empty.

**Do the simulator pass at least once even if you ship the Editor captures.** Keeping Universal means
committing to an iPad build being good, and nobody has looked at one yet.

---

## 2 · Before the first upload

- [ ] **Pick the App Store Connect app record's bundle ID carefully** (see one-way door #1).
- [x] **Export compliance.** Tsugi uses no encryption beyond HTTPS-that-doesn't-exist — it has no
      networking at all. Without a declaration, App Store Connect asks you the encryption question on
      *every single upload*, and holds the build out of TestFlight until a human answers.
      **Done:** `Assets/01.Scripts/Editor/IosPostProcess.cs` writes
      `ITSAppUsesNonExemptEncryption = false` into the generated Info.plist on every iOS export. It
      has to live there rather than in Xcode, because `BuildScript` is deliberately a Replace and
      drops by-hand project edits on the next export.
- [ ] **Version and build number.** `bundleVersion` is `1.0.0`, `buildNumber` is `9`. Build number
      must strictly increase per upload; 1.0.0 is right for the first release. The build script takes
      `-buildNumber` and `-bundleVersion`, so bump via the command line, not by hand.
- [ ] **Orientation** is already Portrait (`defaultScreenOrientation: 0`) and `BuildScript` re-asserts
      it on every build. Good.
- [ ] **iOS deployment target** is 15.0, which covers iPhone 6s and later — sensible, no action.
- [ ] **Signing:** `appleEnableAutomaticSigning: 0` and `appleDeveloperTeamID` is **empty**. You sign
      by hand in Xcode, which is fine, but the empty team ID means the generated project won't
      auto-select a team. Fill it in Player Settings to save a click every build.
- [ ] **A real support email** you will actually read. Apple shows it to users and Google requires it
      publicly on the listing. Suggest a dedicated alias, not your personal inbox — it goes on a public
      web page and will be scraped.

---

## 3 · The privacy answers

You can answer these honestly and they're all the easy answer. Verified in this repo:
`UnityAnalyticsSettings`, `UnityAdsSettings`, `UnityPurchasingSettings` and `CrashReportingSettings`
are all `m_Enabled: 0`, and **no script in `Assets/01.Scripts/` references `UnityWebRequest`, any HTTP
client, sockets, Firebase or any ad SDK.** The app genuinely has no way to send anything anywhere.

**App Store — App Privacy section:** select **"Data Not Collected"**. That is the entire questionnaire;
answering it correctly earns the "Data Not Collected" badge on your listing, which is worth having.

**Google Play — Data safety form:**

| Question | Answer |
|---|---|
| Does your app collect or share any required user data? | **No** |
| Is all of the user data collected by your app encrypted in transit? | N/A (nothing is collected) |
| Do you provide a way for users to request that their data is deleted? | N/A |

**One thing to double-check rather than assume:** `UnityConnectSettings` has
`m_EngineDiagnosticsEnabled: 1`. That is Unity's *editor/engine* diagnostic channel, not runtime user
data collection, and it does not require disclosure — but if you want zero ambiguity, turn it off in
Project Settings → Services before the release build and the question disappears entirely.

**Age rating:** 4+ on Apple, Everyone on Play. On Play, set **Target audience to 13+ rather than
"all ages"** even though the content is 4+ — declaring an under-13 audience pulls you into Families
policy, Designed for Families review and COPPA obligations for no benefit, since your actual audience
is adults who like sudoku.

---

## 4 · The support site

Apple requires a **Support URL** and a **Privacy Policy URL**; Google requires the privacy policy and a
website. Being deployed to `tsugi-flax.vercel.app`:

- `/` — landing page: mark, tagline, what it is, screenshots, store badge
- `/support` — how to get help, the support email, a short FAQ (where's my progress saved, how do I
  reset it, why is there no hint button)
- `/privacy` — the policy. Short and true: collects nothing, stores one local `save.json`, no third
  parties, no analytics, contact address, effective date.

A privacy policy that says "we collect nothing" is the easiest one to write and the only one you can
actually keep. Don't paste a generic template that claims you use cookies and analytics — Apple has
rejected apps for a policy that contradicts the app's declared behaviour.

---

## 5 · Outstanding work

- [x] ~~`ITSAppUsesNonExemptEncryption` post-process build step~~ — done, see §2. The Editor assembly
      has `overrideReferences: false`, so `UnityEditor.iOS.Xcode` was already available to it.
- [x] ~~Screenshot capture tool~~ — `Assets/01.Scripts/Editor/StoreScreenshots.cs`, under
      `Window > TrainSudoku > Screenshots`. Compiles clean; the capture itself is unexercised because
      it needs Play mode, so the first F12 is the real test.
- [x] ~~Support site~~ — **live at `https://tsugi-flax.vercel.app`**, verified returning 200 with real
      content on `/`, `/support` and `/privacy` from an anonymous request. Two things to know: the
      short name `tsugi.vercel.app` was already taken globally, so every store field uses
      `tsugi-flax.vercel.app`; and Vercel's SSO protection **is** enabled but applies only to the raw
      deployment URL, not the production alias — the deployment URL 302s to a login page while the
      alias is public, which is what the stores need. Don't paste a deployment URL into App Store
      Connect: Apple's reviewer is anonymous and would hit the login wall.
- [ ] **Confirm the support alias exists.** The pages publish `confusedgamedevsupport@gmail.com`;
      that mailbox has to actually be created, or the store's required contact address bounces.
- [x] ~~`save.json` `purchasedFullVersion` flag~~ — done, save format v3. Entitlement **fails open**: an
      absent or malformed flag resolves to owned, because wrongly stripping a paying player of 216
      stations is unrecoverable where wrongly granting them is merely generous.
- [x] ~~Get the game running on an iPad~~ — **done, and it needs no `sudo`.** `xcode-select` points at
      the Command Line Tools, but that governs only the default toolchain: setting `DEVELOPER_DIR` to
      `/Applications/Xcode.app/Contents/Developer` per-command reaches the full Xcode 26.6 toolchain
      and `simctl` without touching system state or asking for a password. Tsugi installs and launches
      on the **iPad Pro 13-inch (M5)** simulator (iOS 26.5), which captures natively at 2064×2752 —
      the store's 13-inch slot exactly. `BuildScript -simulator` (or **Window > TrainSudoku > Build
      iOS (Simulator)**) exports to `Builds-Simulator/`, kept apart from `Builds/` because the export
      is a Replace and would otherwise destroy the real device project.
- [x] ~~iPad layout pass~~ — **confirmed good by eye on the iPad Pro 13-inch simulator.** This was the
      real rejection risk, since Apple's reviewer opens a Universal app on an iPad and "phone UI
      stretched to 13 inches" is a documented rejection reason. The concourse was verified in detail:
      nothing clipped, nothing mis-stretched, and the LED ticker shows the full
      `NEXT STATION: ASHGATE` where the phone clipped it to `NEXT STATION: WRE` — which suggests the
      phone-side label clipping is driven by width, and does not reproduce at 4:3.
- [x] ~~Capture the iPad 13-inch screenshot set~~ — **five frames, all 2064×2752, 8-bit, sRGB, no
      alpha**, in `Screenshots/store-ipad-13/`: mid-solve board, arrival card, line map, concourse,
      and a **network map** that the iPhone set does not have. Mandatory while the export stays
      Universal. `simctl` grabs a frame but cannot tap, so a human drives the simulator; Simulator's
      own ⌘S works too, but its PNGs carry an alpha channel and must be converted before upload.
- [ ] **Re-shoot the iPad arrival card.** `03-arrival-three-stars-on-time.jpg` predates the hint fix
      and still shows the overlap. Everything else in the set is final.
- [x] ~~Does `BoardCamera` frame the board correctly at 4:3?~~ — **yes, confirmed on the iPad Pro
      13-inch.** This was the real unknown behind keeping the app Universal: the fit is solved for an
      aspect nothing had ever checked. The board comes out centred with both clue axes legible, both
      tunnel mouths in frame and the platform decals intact. No change needed.
- [x] ~~iPhone screenshots at full size~~ — 8 captures received in `Screenshots/`, all **1320×2868
      with no alpha**, which is the 6.9" slot exactly. They arrived as **HEIC**, which App Store
      Connect does not accept. The ordered five-shot set in `Screenshots/store-iphone-6.9/ordered/`
      is **JPEG, 8-bit, sRGB** — converted straight from the HEIC originals. An earlier PNG version
      was refused by App Store Connect for its "dimensions" when the real fault was 16-bit Display
      P3; see the note in §1. Those PNGs have been deleted so the wrong set cannot be uploaded again. (The very first set, sent through the chat, came
      through at 1186×2576 — downscaled in transit. Always copy the files, never send them through
      something that re-encodes.)
- [ ] **One shot still missing, and it is the important one: a cell selected.** None of the eight show
      the green / red / line-colour neighbour markers. That is the screenshot that answers "what do I
      actually do", and it belongs at position 2, where the App Store search results still show it.
      Tap an empty cell so all three marker colours are visible at once, and capture that.
- [ ] **Label overflow on the line map — phone only, now confirmed.** `TS03 MERROW CROS` is clipped
      at the right edge on the phone and `TS06 SABLE JUNCTION` touches it. The same screen on the
      iPad renders **`TS03 MERROW CROSS` in full**, and the LED ticker that clipped to
      `NEXT STATION: WRE` there reads `NEXT STATION: ASHGATE` complete. So this is width-driven and
      the fix belongs in the phone layout, not the shared one — worth knowing before anyone goes
      hunting in `LineMapElement`'s general label placement.
- [x] ~~On iPad, the hint button collides with the arrival card~~ — **fixed in `PlayScreen.Refresh`.**
      The green ▶ is `_hint`, the disabled placeholder for a hint system that does not exist yet; it
      hangs off the camera strip's bottom right, and `PlayScreen` stays visible through `Win` on
      purpose so the board keeps its frame. At 4:3 the arrival card is proportionally taller than on
      a phone, so its corner reached that spot. It is now hidden outside `GameState.Play`, matching
      what the pause button and the tutorial coach either side of it already do — and unlike resizing
      the card, that costs no height, which the card has none to spare. It also hides under the pause
      menu and during the train run, both of which are right. `GameManager.ApplyState` refreshes every
      visible screen on each transition, which is what makes the gate fire.
- [ ] **Re-capture the iPad arrival frame after the fix.** The stored
      `Screenshots/store-ipad-13/03-arrival-three-stars-on-time.jpg` still shows the overlap, because
      it predates the fix. What is actually proven so far: the code path fires
      (`GameManager.ApplyState` refreshes every visible screen), the Game assembly compiles, and the
      full EditMode suite is **419/419 with zero compile errors**. What is *not* proven is how it
      looks — that needs a simulator re-export and a play-through, since the arrival card only
      appears after finishing a station.
- [ ] **Time the LED ticker when capturing.** It scrolls, so four of the eight caught it mid-word
      (`COLUMN 3 C`, `AR`, `NEXT STATION: WRE`). Not a bug, but it reads as one in a store frame.
- [ ] Android: `AndroidBundleVersionCode: 1`, bundle ID was still the URP template default until the
      rename; confirm it is `com.GorillaGonzalez.Tsugi` before the first Play upload, since it is
      equally permanent there.
