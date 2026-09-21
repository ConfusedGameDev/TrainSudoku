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
A logic puzzle about laying train track. 216 boards, each with exactly one solution, so you never have to guess. No ads, no in-app purchases, and it works offline.
```

*(163 chars. This is the one field you can change without submitting a build — use it for launch
notes, sales, or a new line later.)*

### Description — max 4000 characters

```
Tsugi is a logic puzzle where you build a railway.

Each board is a grid with a number beside every row and column. The number tells you how many pieces of track go in that row or column. Lay one unbroken track from the entrance tunnel to the exit tunnel so that every number is right. When you do, a train comes out of the tunnel and drives the route you built.

If you like sudoku, nonograms or other pencil puzzles, this is that kind of game. Every board has exactly one solution and can be solved by logic alone. You never need to guess.


HOW IT PLAYS

Tap a cell and the game shows which sides the track can connect to. Pick two sides and the piece is laid. If only one piece fits, it goes down in one tap. Hold a piece to lift it again. Numbers turn green when a row or column is complete and red when you have put in too many.

The first station is a short tutorial that walks you through all of it.


WHAT'S IN IT

• 216 boards, called stations, on 24 lines
• Boards grow from 6x6 to 8x8, and later lines give you less track to start from
• Finish the nine stations on a line and the next line opens on the map
• Up to three stars per station for a fast time. The clock can never fail you, so you can ignore it
• Progress saves by itself, even in the middle of a board
• Share a result as a text card that shows the puzzle but not your solution
• English, Japanese, Spanish and French


PRICE AND PRIVACY

Tsugi is a one-time purchase. There are no ads, no in-app purchases and no accounts. The game collects no data and never connects to the internet, so it works anywhere, including on a plane or underground.

"Tsugi" (つぎ) means "next" in Japanese. It is the first word of every station announcement on a Japanese train.
```

*(≈1730 chars of 4,000. Written to be read as a person explaining their game: what it is, how you play,
what you get, what it costs. Every claim in it is checked against the code — in particular it no
longer says the star times were "set by real play", which they were not.)*

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
First release. Thanks for playing.

If you find a bug or a board that confuses you, the support link on this page reaches me directly.
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
線路を敷いて数字を合わせるロジックパズル。全216問、どれも答えは一つだけなので、勘に頼る場面はありません。広告もアプリ内課金もなく、オフラインで遊べます。
```

### Description

```
Tsugiは、線路を敷いて解くロジックパズルです。

盤面の行と列には、それぞれ数字が付いています。数字は、その行や列に置く線路の数です。すべての数字に合うように、入口のトンネルから出口のトンネルまで線路を一本につなげてください。つながると、トンネルから列車が出てきて、あなたが敷いた線路を走ります。

数独やお絵かきロジックのようなペンシルパズルが好きな方に向いています。どの盤面も答えは一つだけで、論理だけで解けます。勘で置く必要はありません。


遊び方

マスをタップすると、線路をつなげられる方向が表示されます。方向を二つ選ぶと線路が置かれます。置ける形が一つしかないときは、タップ一回で置かれます。長押しすると線路を外せます。行や列がそろうと数字が緑に、置きすぎると赤になります。

最初の駅はチュートリアルで、操作をひととおり案内します。


内容

・24路線、216駅(全216問)
・盤面は6×6から8×8まで。先の路線ほど、最初から置かれている線路が少なくなります
・路線の9駅をすべてクリアすると、次の路線が路線図に開きます
・クリアタイムに応じて星が最大三つ。時間切れはないので、時計は気にしなくてもかまいません
・進行状況は自動で保存されます。解いている途中の盤面もそのまま残ります
・結果をテキストのカードで共有できます。カードに載るのは問題だけで、解答は載りません
・日本語、英語、スペイン語、フランス語に対応


価格とプライバシー

買い切りです。広告、アプリ内課金、アカウント登録はありません。データは一切収集せず、インターネットにも接続しないので、機内や地下でも遊べます。

「つぎ」は、電車の車内放送「つぎは…」の最初の言葉です。
```

### Keywords

```
パズル,数独,電車,鉄道,線路,論理,脳トレ,暇つぶし,オフライン,一筆書き,ナンプレ,思考,地下鉄,ひとり
```

### What's New

```
最初のリリースです。遊んでいただきありがとうございます。

不具合や分かりにくい点があれば、このページのサポートリンクからご連絡ください。
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
Un puzzle de lógica en el que construyes una vía de tren. 216 tableros, cada uno con una sola solución: nunca hay que adivinar. Sin anuncios y funciona sin conexión.
```

### Description

```
Tsugi es un puzzle de lógica en el que construyes una vía de tren.

Cada tablero es una cuadrícula con un número junto a cada fila y cada columna. El número indica cuántas piezas de vía van en esa fila o columna. Tienes que tender una vía continua desde el túnel de entrada hasta el de salida de forma que todos los números se cumplan. Cuando lo consigues, un tren sale del túnel y recorre la ruta que has construido.

Si te gustan el sudoku, los nonogramas u otros pasatiempos de lógica, este es ese tipo de juego. Cada tablero tiene una única solución y se resuelve solo con lógica. Nunca hace falta adivinar.


CÓMO SE JUEGA

Toca una casilla y el juego te muestra hacia qué lados puede conectarse la vía. Elige dos lados y la pieza queda colocada. Si solo cabe una pieza, se coloca con un toque. Mantén pulsada una pieza para quitarla. Los números se ponen verdes cuando la fila o columna está completa y rojos cuando te has pasado.

La primera estación es un tutorial corto que te lo enseña todo.


QUÉ INCLUYE

• 216 tableros, llamados estaciones, repartidos en 24 líneas
• Los tableros crecen de 6x6 a 8x8, y las líneas más avanzadas te dan menos vía colocada de inicio
• Termina las nueve estaciones de una línea y se abre la siguiente en el mapa
• Hasta tres estrellas por estación según tu tiempo. El reloj nunca te hace perder, así que puedes ignorarlo
• El progreso se guarda solo, incluso a mitad de un tablero
• Comparte tu resultado como una tarjeta de texto que muestra el puzzle pero no tu solución
• En español, inglés, japonés y francés


PRECIO Y PRIVACIDAD

Tsugi se paga una sola vez. No tiene anuncios, compras dentro de la app ni cuentas. El juego no recoge ningún dato y nunca se conecta a internet, así que funciona en cualquier sitio, también en un avión o en el metro.

«Tsugi» (つぎ) significa «siguiente» en japonés. Es la primera palabra de cada anuncio de estación en los trenes de Japón.
```

### Keywords

```
logica,tren,cerebro,via,metro,ingenio,zen,sinconexion,vias,japon,relax,mente,pasatiempo,solitario
```

### What's New

```
Primera versión. Gracias por jugar.

Si encuentras un fallo o algo que no se entiende, el enlace de soporte de esta página me llega directamente.
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
Un jeu de logique où l'on construit une voie ferrée. 216 grilles, une seule solution chacune : jamais besoin de deviner. Sans publicité, jouable hors ligne.
```

### Description

```
Tsugi est un jeu de logique où l'on construit une voie ferrée.

Chaque grille porte un chiffre à côté de chaque ligne et de chaque colonne. Ce chiffre indique combien de morceaux de voie vont dans cette ligne ou cette colonne. Il faut poser une voie continue du tunnel d'entrée au tunnel de sortie en respectant tous les chiffres. Quand c'est fait, un train sort du tunnel et parcourt le trajet que vous avez construit.

Si vous aimez le sudoku, les nonogrammes ou les autres jeux de logique sur grille, c'est ce genre de jeu. Chaque grille a une seule solution et se résout uniquement par la logique. Il n'y a jamais besoin de deviner.


COMMENT ON JOUE

Touchez une case et le jeu montre de quels côtés la voie peut se raccorder. Choisissez deux côtés et la pièce est posée. Si une seule pièce convient, elle se pose en un seul geste. Maintenez le doigt sur une pièce pour la retirer. Les chiffres passent au vert quand la ligne ou la colonne est complète, et au rouge quand il y a trop de voie.

La première gare est un court tutoriel qui explique tout cela.


CONTENU

• 216 grilles, appelées gares, réparties sur 24 lignes
• Les grilles passent de 6x6 à 8x8, et les lignes avancées donnent moins de voie posée au départ
• Terminez les neuf gares d'une ligne et la suivante s'ouvre sur le plan
• Jusqu'à trois étoiles par gare selon votre temps. Le chrono ne fait jamais perdre, vous pouvez donc l'ignorer
• La progression s'enregistre toute seule, même au milieu d'une grille
• Partagez un résultat sous forme de carte texte qui montre le puzzle mais pas votre solution
• En français, anglais, japonais et espagnol


PRIX ET VIE PRIVÉE

Tsugi s'achète une seule fois. Pas de publicité, pas d'achats intégrés, pas de compte. Le jeu ne collecte aucune donnée et ne se connecte jamais à Internet : il fonctionne partout, y compris en avion ou dans le métro.

« Tsugi » (つぎ) veut dire « suivant » en japonais. C'est le premier mot de chaque annonce de gare dans les trains japonais.
```

### Keywords

```
logique,train,cerveau,voie,metro,rail,zen,horsligne,japon,detente,reflexion,casse,tete,solitaire
```

### What's New

```
Première version. Merci de jouer.

Si vous trouvez un bug ou quelque chose de confus, le lien d'assistance de cette page m'arrive directement.
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
| en | `Lay track to match the numbers. 216 logic puzzles, no guessing, no ads.` | 71 |
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
| One line | A logic puzzle where you lay train track to match the numbers on each row and column. |
| Two lines | Tsugi is a logic puzzle where you build a railway. Each row and column has a number saying how many pieces of track it holds, and you lay one track from tunnel to tunnel. 216 boards, each with exactly one solution. |
| Tagline under the logo | Next station. |
