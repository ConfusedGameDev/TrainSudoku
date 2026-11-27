using System.Collections.Generic;
using TrainSudoku.XR.Rules;

namespace TrainSudoku.XR.Editor
{
    /// <summary>
    /// XR's copy in all four locales (XR-PRD 9, X24): the source <see cref="XRLanguageSetup"/> writes into the `XR` String
    /// Table. Edit the words here and run Window > TrainSudoku > XR > Write XR String Table, then rebake the XR ja atlas.
    /// </summary>
    /// <remarks>
    /// The words are the phone's `UI` table's wherever the meaning is the same, so the two editions speak alike; what
    /// differs is the hand: the phone taps and long-presses, the headset pinches, drops, lifts and throws. The ja tutorial
    /// lines keep the phone's spaced style. TSUGI, NEXT STATION and station and line names are proper nouns, not here.
    /// </remarks>
    internal static class XRCopy
    {
        internal readonly struct Row
        {
            public readonly string En, Ja, Es, Fr;

            public Row(string en, string ja, string es, string fr)
            {
                En = en;
                Ja = ja;
                Es = es;
                Fr = fr;
            }

            public string For(string code)
            {
                switch (code.Split('-')[0])
                {
                    case "ja": return Ja;
                    case "es": return Es;
                    case "fr": return Fr;
                    default: return En;
                }
            }
        }

        internal static readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>
        {
            // ---- The signboard
            [XRKeys.MastheadNetwork] = new Row("NETWORK", "路線網", "RED", "RÉSEAU"),
            [XRKeys.MastheadHint] = new Row("Choose a line on the platform", "台の上の 路線を 選んでください", "Elige una línea en la plataforma", "Choisis une ligne sur le plateau"),
            [XRKeys.LineMapTitle] = new Row("LINE MAP", "路線図", "MAPA DE LÍNEA", "PLAN DE LIGNE"),
            [XRKeys.LineMapCleared] = new Row("{0} / {1} CLEARED", "{0} / {1} 制覇", "{0} / {1} COMPLETADAS", "{0} / {1} TERMINÉES"),
            [XRKeys.LineMapHint] = new Row("Choose a station on the platform", "台の上の 駅を 選んでください", "Elige una estación en la plataforma", "Choisis une station sur le plateau"),
            [XRKeys.MapContinue] = new Row("CONTINUE", "つづき", "CONTINUAR", "REPRENDRE"),
            [XRKeys.PauseTitle] = new Row("PAUSED", "一時停止", "EN PAUSA", "EN PAUSE"),
            [XRKeys.PauseResume] = new Row("RESUME", "再開", "CONTINUAR", "REPRENDRE"),
            [XRKeys.PauseHint] = new Row("Retry, the line map and settings are on the wrist menu",
                "やり直し・路線図・設定は 手首の メニューに あります",
                "Reintentar, el mapa y los ajustes están en el menú de la muñeca",
                "Recommencer, le plan et les réglages sont dans le menu du poignet"),
            [XRKeys.ArrivalTitle] = new Row("ARRIVAL", "到着", "LLEGADA", "ARRIVÉE"),
            [XRKeys.ArrivalThisRun] = new Row("THIS RUN  {0}", "今回  {0}", "ESTA VUELTA  {0}", "CE TRAJET  {0}"),
            [XRKeys.ArrivalBest] = new Row("BEST  {0}", "最速  {0}", "MEJOR  {0}", "MEILLEUR  {0}"),
            [XRKeys.ArrivalNewBest] = new Row("NEW BEST", "自己最速", "NUEVO RÉCORD", "NOUVEAU RECORD"),
            [XRKeys.ArrivalOnTime] = new Row("ON TIME", "定刻", "PUNTUAL", "À L'HEURE"),
            [XRKeys.ArrivalSlightDelay] = new Row("SLIGHT DELAY", "やや遅れ", "LIGERO RETRASO", "LÉGER RETARD"),
            [XRKeys.ArrivalDelayed] = new Row("DELAYED", "遅延", "RETRASADO", "RETARDÉ"),
            [XRKeys.ArrivalMap] = new Row("LINE MAP", "路線図", "MAPA", "PLAN"),
            [XRKeys.ArrivalRetry] = new Row("RUN AGAIN", "もう一度", "OTRA VEZ", "REFAIRE"),
            [XRKeys.ArrivalNext] = new Row("NEXT STATION", "つぎの駅", "SIGUIENTE", "STATION SUIVANTE"),
            [XRKeys.ArrivalToNetwork] = new Row("TO THE NETWORK", "路線網へ", "A LA RED", "VERS LE RÉSEAU"),

            // ---- Pip, on the signboard in play (the web edition's words)
            [XRPipKeys.Start] = new Row("All aboard! Lay the rails.", "出発進行! 線路を つなごう。", "¡Todos a bordo! Coloca las vías.", "Tout le monde à bord ! Pose les voies."),
            [XRPipKeys.RowClear] = new Row("Row {0} clear!", "{0}行 完了!", "¡Fila {0} completa!", "Ligne {0} complète !"),
            [XRPipKeys.ColumnClear] = new Row("Column {0} clear!", "{0}列 完了!", "¡Columna {0} completa!", "Colonne {0} complète !"),
            [XRPipKeys.RowOver] = new Row("Too many in row {0}!", "{0}行は 多すぎ!", "¡Demasiadas en la fila {0}!", "Trop de voies sur la ligne {0} !"),
            [XRPipKeys.ColumnOver] = new Row("Too many in column {0}!", "{0}列は 多すぎ!", "¡Demasiadas en la columna {0}!", "Trop de voies sur la colonne {0} !"),

            // ---- The wrist menu
            [XRKeys.WristResume] = new Row("RESUME", "再開", "CONTINUAR", "REPRENDRE"),
            [XRKeys.WristRetry] = new Row("RETRY", "やり直す", "REINTENTAR", "RECOMMENCER"),
            [XRKeys.WristLineMap] = new Row("LINE MAP", "路線図", "MAPA DE LÍNEA", "PLAN DE LIGNE"),
            [XRKeys.WristNetwork] = new Row("NETWORK", "路線網", "RED", "RÉSEAU"),
            [XRKeys.WristSettings] = new Row("SETTINGS", "設定", "AJUSTES", "RÉGLAGES"),
            [XRKeys.WristClose] = new Row("CLOSE", "閉じる", "CERRAR", "FERMER"),
            [XRKeys.SettingsHand] = new Row("HAND", "利き手", "MANO", "MAIN"),
            [XRKeys.SettingsLeft] = new Row("LEFT", "左", "IZQUIERDA", "GAUCHE"),
            [XRKeys.SettingsRight] = new Row("RIGHT", "右", "DERECHA", "DROITE"),
            [XRKeys.SettingsBoard] = new Row("BOARD", "盤", "TABLERO", "PLATEAU"),
            [XRKeys.SettingsLower] = new Row("LOWER", "下げる", "BAJAR", "BAISSER"),
            [XRKeys.SettingsRaise] = new Row("RAISE", "上げる", "SUBIR", "MONTER"),
            [XRKeys.SettingsReplace] = new Row("RE-PLACE BOARD", "盤を 置き直す", "RECOLOCAR TABLERO", "REPLACER LE PLATEAU"),
            [XRKeys.SettingsLanguage] = new Row("LANGUAGE", "言語", "IDIOMA", "LANGUE"),
            [XRKeys.SettingsMusic] = new Row("MUSIC", "音楽", "MÚSICA", "MUSIQUE"),
            [XRKeys.SettingsEffects] = new Row("EFFECTS", "効果音", "EFECTOS", "EFFETS"),
            [XRKeys.SettingsBack] = new Row("BACK", "もどる", "ATRÁS", "RETOUR"),

            // ---- Placing the board
            [XRKeys.PlacementPinch] = new Row("Pinch to put the board here", "ピンチで ここに 盤を 置きます",
                "Pellizca para poner el tablero aquí", "Pince pour poser le plateau ici"),
            [XRKeys.PlacementNoTable] = new Row(
                "No table found. Pinch to place the board here,\nor run Space Setup in the headset settings.",
                "テーブルが 見つかりません。ピンチで ここに 置くか、\nヘッドセットの 設定で スペース設定を 行ってください。",
                "No se encontró ninguna mesa. Pellizca para poner el tablero aquí,\no ejecuta la configuración del espacio en los ajustes del visor.",
                "Aucune table trouvée. Pince pour poser le plateau ici,\nou lance la configuration de l'espace dans les réglages du casque."),
            [XRKeys.PlacementLook] = new Row("Look at a table", "テーブルを 見てください", "Mira hacia una mesa", "Regarde une table"),

            // ---- The tutorial (XR-PRD 7)
            [XRTutorialKeys.LayFirst] = new Row(
                "Pinch the lit piece in the tray and drop it on the ring. The next rail goes here.",
                "トレイの 光っている 線路を つまんで、輪の 上で 離します。次の 線路は ここです。",
                "Pellizca la pieza iluminada de la bandeja y suéltala sobre el círculo. Aquí va la siguiente vía.",
                "Pince la pièce éclairée dans le bac et lâche-la sur le cercle. C'est là que va la voie suivante."),
            [XRTutorialKeys.LayNext] = new Row("Next: the lit piece, onto the ring.", "次は 光っている 線路を 輪の 上へ。",
                "Ahora, la pieza iluminada sobre el círculo.", "Ensuite : la pièce éclairée, sur le cercle."),
            [XRTutorialKeys.Clue] = new Row(
                "Each number counts rails. That line now holds exactly as many as it asks for.",
                "数字は 線路の 本数です。この 列は ちょうど その数に なりました。",
                "Cada número cuenta vías. Esa línea ya tiene justo las que pide.",
                "Chaque nombre compte les voies. Cette ligne a maintenant exactement celles qu'elle demande."),
            [XRTutorialKeys.Unlocked] = new Row("You have it. The rest of the line is yours.", "その調子です。ここから 先は おまかせ します。",
                "Ya lo tienes. El resto de la línea es tuyo.", "Tu as compris. Le reste de la ligne est à toi."),
            [XRTutorialKeys.HintLift] = new Row("This rail is not on the line. Pinch it to lift it off.",
                "この 線路は 路線に 入りません。つまんで 持ち上げましょう。",
                "Esta vía no es de la línea. Pellízcala para levantarla.", "Cette voie n'est pas sur la ligne. Pince-la pour la retirer."),
            [XRTutorialKeys.Next] = new Row("Next rail here.", "次の 線路は ここ。", "La siguiente vía, aquí.", "La voie suivante, ici."),
            [XRTutorialKeys.NoteSteered] = new Row("This one first.", "まずは こちらから。", "Primero esta.", "Celle-ci d'abord."),
            [XRTutorialKeys.NoteWrongPiece] = new Row("Take the lit piece.", "光っている 線路を 取りましょう。", "Coge la pieza iluminada.", "Prends la pièce éclairée."),
            [XRTutorialKeys.NoteFixed] = new Row("That rail was laid before you arrived. Lift one of your own.",
                "それは 初めから 敷かれた 線路です。自分で 置いた 線路を 持ち上げて ください。",
                "Esa vía ya estaba tendida. Levanta una de las tuyas.", "Cette voie était déjà posée. Retire une des tiennes."),
            [XRTutorialKeys.NoteOverfull] = new Row("That line holds more rails than its number allows. Lift one off.",
                "その 列は 線路が 多すぎます。一つ 持ち上げて 取り除いて ください。",
                "Esa línea tiene más vías de las que marca su número. Levanta una.",
                "Cette ligne a plus de voies que son nombre. Retires-en une."),

            // ---- The rules briefing (the phone's words: nothing in them is about the hand)
            [XRTutorialKeys.Brief1Title] = new Row("THE LINE", "路線", "LA LÍNEA", "LA LIGNE"),
            [XRTutorialKeys.Brief1Body] = new Row(
                "This station has no railway yet. Lay one piece of track per square so the line runs from the S tunnel to the E tunnel.",
                "この 駅には まだ 線路が ありません。一マスに 一つずつ 置き、S の トンネルから E の トンネルまで つなぎます。",
                "Esta estación aún no tiene ferrocarril. Coloca una pieza de vía por casilla para que la línea vaya del túnel S al túnel E.",
                "Cette gare n'a pas encore de chemin de fer. Pose une pièce de voie par case pour relier le tunnel S au tunnel E."),
            [XRTutorialKeys.Brief2Title] = new Row("THE NUMBERS", "数字", "LOS NÚMEROS", "LES NOMBRES"),
            [XRTutorialKeys.Brief2Body] = new Row(
                "Each number says how many pieces its row or column must hold. Exactly that many: it turns green when the count is right and red when it is over.",
                "数字は その行・列に 置く 線路の 本数です。ちょうど その数に なると 緑、超えると 赤に なります。",
                "Cada número dice cuántas piezas debe tener su fila o columna. Justo esas: se pone verde cuando acierta y rojo cuando se pasa.",
                "Chaque nombre indique combien de pièces sa ligne ou sa colonne doit contenir. Exactement autant : il passe au vert quand le compte est juste, au rouge quand il est dépassé."),
            [XRTutorialKeys.Brief3Title] = new Row("DEPARTURE", "発車", "SALIDA", "DÉPART"),
            [XRTutorialKeys.Brief3Body] = new Row(
                "When the track joins S to E and every number is matched, the train runs. Take your time — the clock only decides your stars.",
                "S から E まで つながり、すべての 数字が そろうと 列車が 走ります。時間は 星の 数を 決めるだけです。",
                "Cuando la vía une S con E y todos los números cuadran, el tren sale. Tómate tu tiempo: el reloj solo decide las estrellas.",
                "Quand la voie relie S à E et que tous les nombres sont respectés, le train part. Prends ton temps : l'horloge ne décide que des étoiles."),
            [XRTutorialKeys.BriefNext] = new Row("NEXT", "つぎへ", "SIGUIENTE", "SUIVANT"),
            [XRTutorialKeys.BriefStart] = new Row("START", "はじめる", "EMPEZAR", "COMMENCER"),

            // ---- The board lesson (XR-PRD 7): the callout's three lines, then the signboard's two cards. The closing card
            // names the wrist menu's entries in the words the menu itself uses.
            [XRTutorialKeys.MoveApproach] = new Row(
                "You can move this board. Bring your hands to the rail.",
                "この 盤は 動かせます。手を 手すりに 近づけて ください。",
                "Puedes mover este tablero. Acerca las manos a la barra.",
                "Tu peux déplacer ce plateau. Approche tes mains de la barre."),
            [XRTutorialKeys.MovePinch] = new Row(
                "Pinch the rail with both hands.",
                "両手で 手すりを つまみます。",
                "Pellizca la barra con las dos manos.",
                "Pince la barre des deux mains."),
            [XRTutorialKeys.MoveCarry] = new Row(
                "Move your hands to carry it. Spread or close them to resize it.",
                "手を 動かすと 盤も 動きます。両手を 広げたり 狭めたりして 大きさを 変えます。",
                "Mueve las manos para llevarlo. Sepáralas o júntalas para cambiar su tamaño.",
                "Bouge les mains pour le porter. Écarte-les ou rapproche-les pour changer sa taille."),
            [XRTutorialKeys.MoveTitle] = new Row("THE BOARD", "盤", "EL TABLERO", "LE PLATEAU"),
            [XRTutorialKeys.MoveBody] = new Row(
                "This board goes wherever you want it. Pinch its rail with both hands, one on each side, then carry it, turn it, or pull your hands apart to make it bigger.",
                "この 盤は 好きな 場所に 置けます。両手で 左右の 手すりを つまみ、運んだり 回したり、両手を 広げて 大きく したり できます。",
                "Este tablero va donde tú quieras. Pellizca su barra con las dos manos, una a cada lado, y llévalo, gíralo o separa las manos para agrandarlo.",
                "Ce plateau va où tu veux. Pince sa barre des deux mains, une de chaque côté, puis porte-le, tourne-le ou écarte les mains pour l'agrandir."),
            [XRTutorialKeys.MoveSkip] = new Row("SKIP", "スキップ", "SALTAR", "PASSER"),
            [XRTutorialKeys.MoveClosingTitle] = new Row("PLACE IT AGAIN", "置き直す", "RECOLOCAR", "REPLACER"),
            [XRTutorialKeys.MoveClosingBody] = new Row(
                "To place the board afresh at any time, press the button on the back of your wrist to pause, then choose SETTINGS and RE-PLACE BOARD.",
                "盤を 置き直したい ときは、手首の ボタンを 押して 一時停止し、「設定」から 「盤を 置き直す」を 選びます。",
                "Para volver a colocar el tablero en cualquier momento, pulsa el botón del dorso de la muñeca para pausar y elige AJUSTES y RECOLOCAR TABLERO.",
                "Pour replacer le plateau à tout moment, appuie sur le bouton au dos du poignet pour mettre en pause, puis choisis RÉGLAGES et REPLACER LE PLATEAU."),
            [XRTutorialKeys.MoveDone] = new Row("GOT IT", "わかった", "ENTENDIDO", "COMPRIS"),
        };
    }
}
