# Tsugi — Store Listing Copy

v1.0 · drafted 19 SEP 2026 · for App Store (iOS, first) then Google Play (Android)

Everything here is ready to paste. Field names match App Store Connect and Play Console exactly.
Character limits are stated per field and every string below is **already inside its limit** — recount
if you edit one.

> **One standing caution.** The copy claims "pay once, play all 216" because that is true of v1 at
> $0.99. If you later move to free-with-a-daily-level plus a $2.99 unlock, this listing gets rewritten
> and — more importantly — **Apple does not let you take content away from people who already paid**.
> Anyone who buys v1 must keep all 216 stations forever. That is a one-way door, not a blocker: it
> just means the freemium build needs an "owned everything before the change" flag in `save.json`
> written *now*, while it costs nothing, rather than reconstructed later from receipts. See
> `Docs/ReleaseChecklist.md`.

---

## 1 · App Store Connect — English (primary, en-GB/en-US)

### App Name — max 30 characters

```
Tsugi: Railway Puzzle
```

*(21 chars. "Tsugi" alone is unsearchable; the two extra words are the only keywords Apple weights
most heavily. Both are indexed automatically, so neither appears in the keyword field below.)*

### Subtitle — max 30 characters

```
A sudoku of railway lines
```

*(25 chars. "sudoku" is indexed from here, so it is also kept out of the keyword field.)*

### Promotional Text — max 170 characters (editable any time, no review)

```
216 stations. 24 lines. Every board has exactly one solution, proven by solver — so there is never a guess, only a deduction you haven't found yet.
```

*(147 chars. This is the one field you can change without submitting a build — use it for launch
notes, sales, or a new line later.)*

### Description — max 4000 characters

```
Lay the rails. Satisfy every number. Let the train run.

Tsugi is a railway puzzle in the spirit of sudoku. Every board is a small grid with a number beside each row and column, and that number is exactly how many pieces of track the line must hold. Your job is to lay one continuous route from the entrance tunnel to the exit — and to make every number come out right.

No timer pushing you. No lives. No hints to buy. Just a clean deduction, and a train that runs the line you built.


216 STATIONS ACROSS 24 LINES

Ashenvale, Briarwharf, Calderwyke, Emberfell, Ironmere, Ravenscar, Saltmarch — twenty-four lines to work your way along. Every single board has been verified by solver to have exactly one solution, so you are never guessing and never stuck on a coin flip. If you cannot see the next piece yet, it is there.

Lines open as you go: earn a star at every station on a line and the next one joins the network map.


THREE STARS, IF YOU WANT THEM

Finish a station and you are rated against times set by real play, not by a formula. The clock sits quietly in the corner — chase it or ignore it entirely, the puzzle does not change.


BUILT TO BE PUT DOWN

Every board saves itself as you lay it, mid-puzzle and mid-thought. Close it on the platform, open it again at home, and the track is exactly where you left it — clock included.


SHARE THE LINE

Every finished station makes a small text card you can paste into a chat. It shows the puzzle, never your solution, so passing it to a friend is an invitation rather than a spoiler.


PLAYS IN FOUR LANGUAGES

English, 日本語, Español, Français — the whole game, not just the menus.


NO ADS. NO TRACKING. NO ACCOUNT.

Tsugi collects nothing, sends nothing, and asks for nothing. There is no sign-in, no email box, no analytics and no advertising SDK — the app has no networking code in it at all. It works with aeroplane mode on and always will. Pay once, play all 216 stations.
```

*(≈1,760 chars of 4,000. Deliberately short — App Store descriptions are read in the first three
lines or not at all, and the headed blocks below the fold are there for the people who scroll.)*

### Keywords — max 100 characters, comma separated, no spaces

```
logic,train,brain,track,rail,metro,subway,zen,offline,tracks,teaser,japan,relax,solitaire,tile
```

*(94 chars. Rules applied: never repeat a word already in the App Name or Subtitle — Apple indexes
those separately and a repeat wastes the budget — never use plurals of a word already present, and
never use a competitor's name. "brain" + "teaser" are separate entries because Apple recombines
keywords into phrases.)*

### What's New in This Version — max 4000 characters

```
The first departure.

216 stations across 24 lines, four languages, and a train that runs whatever route you build.

Thank you for riding.
```

### Support URL — required

```
https://tsugi-flax.vercel.app/support
```

### Marketing URL — optional

```
https://tsugi-flax.vercel.app
```

### Privacy Policy URL — required

```
https://tsugi-flax.vercel.app/privacy
```

### Copyright

```
2026 GorillaGonzalez
```

*(App Store format: year then holder, no © symbol — Apple adds it.)*

### Category

| Field | Value |
|---|---|
| Primary | Games → Puzzle |
| Secondary | Games → Board |

*Puzzle is the right primary: it is the most-browsed games subcategory and the honest one. Board is a
better secondary than Strategy or Family — the board-game audience overlaps heavily with sudoku.*

### Age Rating

**4+.** No violence, no gambling, no user-generated content, no web access, no contests.
The share card is a plain-text copy to the clipboard, which is not "unrestricted web access" and not
UGC — it never shows another player's content.

### Price

**$0.99** (Tier 1), all territories.

---

## 2 · App Store Connect — 日本語 (ja)

### App Name

```
Tsugi：線路をつなぐパズル
```

### Subtitle

```
数字を頼りに、線路を敷く
```

### Promotional Text

```
216の駅、24の路線。すべての盤面に解は一つだけ——ソルバーで検証済みです。当てずっぽうはありません。まだ見つけていない筋道があるだけです。
```

### Description

```
線路を敷く。数字を合わせる。列車を走らせる。

Tsugiは、数独の流儀でつくられた鉄道パズルです。盤面の行と列にはそれぞれ数字が添えてあり、その数字が、その線に置くべき線路の数そのものです。入口のトンネルから出口まで一本の線路をつなぎ、なおかつすべての数字を合わせる——それがあなたの仕事です。

急かすタイマーはありません。ライフもありません。買うヒントもありません。あるのは澄んだ推論と、あなたが敷いた線路を走る列車だけです。


24路線・216駅

すべての盤面は、解が一つだけであることをソルバーで検証しています。だから運任せの場面は一度もありません。次の一手が見えないときは、まだ見つけていないだけです。

路線は進むほどに開いていきます。一つの路線のすべての駅で星を取れば、次の路線が路線図に加わります。


星は三つ、望むなら

駅を解き終えると、実際のプレイから決めた基準時間で評価されます。時計は隅で静かに動いているだけ——追いかけても、無視しても、パズルは変わりません。


いつでも置ける

盤面は敷きながら自動で保存されます。ホームで閉じて、家で開けば、線路も時計もそのままです。


路線を共有する

解き終えた駅は、チャットに貼れる小さなテキストカードになります。写るのは問題だけで、あなたの解答は決して含まれません。


4言語対応

English、日本語、Español、Français——メニューだけでなく、ゲーム全体が対応しています。


広告なし。追跡なし。アカウントなし。

Tsugiは何も集めず、何も送りません。サインインもメールアドレスも解析も広告も、そもそも通信するコードが一行もありません。機内モードのままで遊べます。一度の購入で、216駅すべてを。
```

### Keywords

```
パズル,数独,電車,鉄道,線路,論理,脳トレ,暇つぶし,オフライン,一筆書き,無料,思考,地下鉄,ひとり
```

### What's New

```
はじめての発車です。

24路線216駅、4言語、そしてあなたが敷いた線路をそのまま走る列車。

ご乗車ありがとうございます。
```

---

## 3 · App Store Connect — Español (es-ES / es-MX)

### App Name

```
Tsugi: Puzzle Ferroviario
```

### Subtitle

```
Un sudoku de vías de tren
```

### Promotional Text

```
216 estaciones. 24 líneas. Cada tablero tiene una única solución, verificada por solucionador: nunca hay que adivinar, solo deducir lo que aún no has visto.
```

### Description

```
Tiende las vías. Cuadra los números. Deja pasar el tren.

Tsugi es un puzzle ferroviario con el espíritu del sudoku. Cada tablero es una cuadrícula con un número junto a cada fila y cada columna, y ese número es exactamente cuántas piezas de vía debe contener esa línea. Tu tarea es tender una ruta continua desde el túnel de entrada hasta el de salida, y que todos los números cuadren.

Sin cronómetro que te apremie. Sin vidas. Sin pistas de pago. Solo una deducción limpia y un tren que recorre la línea que has construido.


216 ESTACIONES EN 24 LÍNEAS

Cada tablero ha sido verificado por solucionador: tiene una solución y solo una. Nunca juegas a la lotería ni te quedas atascado en una moneda al aire. Si todavía no ves la siguiente pieza, está ahí.

Las líneas se abren a medida que avanzas: consigue una estrella en cada estación de una línea y la siguiente aparece en el mapa de la red.


TRES ESTRELLAS, SI LAS QUIERES

Al terminar una estación recibes una valoración según tiempos fijados jugando de verdad, no con una fórmula. El reloj está en una esquina, discreto: persíguelo o ignóralo, el puzzle no cambia.


HECHO PARA DEJARLO A MEDIAS

Cada tablero se guarda solo mientras lo tiendes, a mitad de partida y a mitad de idea. Ciérralo en el andén, ábrelo en casa, y la vía sigue donde la dejaste. El reloj también.


COMPARTE LA LÍNEA

Cada estación terminada genera una pequeña tarjeta de texto que puedes pegar en un chat. Muestra el puzzle, nunca tu solución: es una invitación, no un spoiler.


EN CUATRO IDIOMAS

English, 日本語, Español, Français — el juego entero, no solo los menús.


SIN ANUNCIOS. SIN RASTREO. SIN CUENTA.

Tsugi no recoge nada ni envía nada. No hay inicio de sesión, ni correo, ni analíticas, ni SDK publicitario: la aplicación no tiene una sola línea de código de red. Funciona en modo avión y siempre lo hará. Paga una vez, juega las 216 estaciones.
```

### Keywords

```
logica,tren,cerebro,via,metro,ingenio,zen,sinconexion,vias,japon,relax,mente,pasatiempo,solitario
```

### What's New

```
La primera salida.

216 estaciones en 24 líneas, cuatro idiomas y un tren que recorre exactamente la ruta que construyas.

Gracias por viajar con nosotros.
```

---

## 4 · App Store Connect — Français (fr-FR)

### App Name

```
Tsugi : Puzzle Ferroviaire
```

### Subtitle

```
Un sudoku de voies ferrées
```

### Promotional Text

```
216 gares. 24 lignes. Chaque grille n'a qu'une seule solution, vérifiée par solveur : on ne devine jamais, on déduit ce qu'on n'a pas encore vu.
```

### Description

```
Posez les rails. Faites tomber les chiffres juste. Laissez passer le train.

Tsugi est un puzzle ferroviaire dans l'esprit du sudoku. Chaque grille porte un chiffre en regard de chaque ligne et de chaque colonne, et ce chiffre indique exactement combien de morceaux de voie cette ligne doit contenir. À vous de poser un tracé continu du tunnel d'entrée jusqu'à la sortie, et de faire tomber tous les chiffres juste.

Aucun chronomètre pour vous presser. Aucune vie. Aucun indice à acheter. Seulement une déduction nette, et un train qui parcourt la ligne que vous avez construite.


216 GARES SUR 24 LIGNES

Chaque grille a été vérifiée par solveur : elle admet une solution et une seule. Vous ne jouez jamais à pile ou face et vous ne restez jamais bloqué sur un coup de chance. Si vous ne voyez pas encore la pièce suivante, elle est là.

Les lignes s'ouvrent au fil du jeu : décrochez une étoile à chaque gare d'une ligne et la suivante rejoint le plan du réseau.


TROIS ÉTOILES, SI VOUS Y TENEZ

À la fin d'une gare, vous êtes noté sur des temps établis en jouant réellement, pas par une formule. L'horloge reste discrète dans un coin : courez après ou ignorez-la, le puzzle ne change pas.


FAIT POUR ÊTRE REPOSÉ

Chaque grille s'enregistre toute seule pendant que vous la posez, en pleine partie et en pleine réflexion. Fermez sur le quai, rouvrez à la maison : la voie est exactement où vous l'aviez laissée, l'horloge aussi.


PARTAGEZ LA LIGNE

Chaque gare terminée produit une petite carte en texte à coller dans une conversation. Elle montre le puzzle, jamais votre solution : c'est une invitation, pas un spoiler.


EN QUATRE LANGUES

English, 日本語, Español, Français — le jeu entier, pas seulement les menus.


AUCUNE PUBLICITÉ. AUCUN PISTAGE. AUCUN COMPTE.

Tsugi ne collecte rien et n'envoie rien. Pas de connexion, pas d'adresse e-mail, pas d'analytique, pas de régie publicitaire : l'application ne contient pas une seule ligne de code réseau. Elle fonctionne en mode avion, et ce sera toujours le cas. Payez une fois, jouez les 216 gares.
```

### Keywords

```
logique,train,cerveau,voie,metro,rail,zen,horsligne,japon,detente,reflexion,casse,tete,solitaire
```

### What's New

```
Le premier départ.

216 gares sur 24 lignes, quatre langues, et un train qui parcourt exactement le tracé que vous posez.

Merci d'avoir voyagé avec nous.
```

---

## 5 · Google Play (Android, second release)

Play splits the description differently from Apple: an 80-character **short description** that shows
under the title, and a 4000-character **full description**. The App Store description above drops
straight into the full-description field in each language; only these fields are new.

### Title — max 30 characters

```
Tsugi: Railway Puzzle
```

### Short Description — max 80 characters

| Locale | String | Chars |
|---|---|---|
| en | `A sudoku of railway lines. 216 stations, one solution each, no ads ever.` | 71 |
| ja | `数独の流儀でつくられた鉄道パズル。216駅、解は一つだけ、広告なし。` | 33 |
| es | `Un sudoku de vías de tren. 216 estaciones, una solución cada una, sin anuncios.` | 78 |
| fr | `Un sudoku de voies ferrées. 216 gares, une seule solution, sans publicité.` | 73 |

### Full Description

Use the App Store **Description** from sections 1–4 verbatim. Two Play-specific notes: Play renders
no Markdown but does honour blank lines, so the headed blocks survive the paste intact; and Play's
policy forbids fake urgency or a claim of ranking, neither of which appears above.

### Play Console — other required fields

| Field | Value |
|---|---|
| App or game | Game |
| Category | Puzzle |
| Tags | Brain games, Puzzle, Logic |
| Email | *(a real address you monitor — see the checklist)* |
| Website | `https://tsugi-flax.vercel.app` |
| Privacy Policy | `https://tsugi-flax.vercel.app/privacy` |
| Content rating | Everyone (fill the IARC questionnaire: no violence, no UGC, no data sharing) |
| Ads | **Contains no ads** |
| Data safety | No data collected, no data shared |
| Target audience | 13+ recommended — see the checklist for why not "all ages" |

---

## 6 · The one-liners, for everything else

Kept here so the same words appear on the site, in the press kit and in a tweet.

| Use | Text |
|---|---|
| Six words | A sudoku of railway lines. |
| One line | Lay the rails, satisfy every number, and let the train run the route you built. |
| Two lines | Tsugi is a railway puzzle in the spirit of sudoku: every row and column tells you how many pieces of track it must hold, and you lay one continuous line from tunnel to tunnel. 216 stations, every one proven to have a single solution. |
| Tagline under the logo | Next station. |
