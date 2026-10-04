namespace TrainSudoku.XR
{
    /// <summary>
    /// Rows of the `XR` String Table (XR-PRD 9), by the surface that shows them. The tutorial's rows are
    /// <see cref="Rules.XRTutorialKeys"/>. Every constant here and there must have a row in all four locales; the editor
    /// check under Window > TrainSudoku > XR holds them to it.
    /// </summary>
    public static class XRKeys
    {
        public const string MastheadNetwork = "xr.masthead.network";
        public const string MastheadHint = "xr.masthead.hint";

        public const string LineMapTitle = "xr.linemap.title";
        public const string LineMapCleared = "xr.linemap.cleared";
        public const string LineMapHint = "xr.linemap.hint";
        public const string MapContinue = "xr.map.continue";

        public const string PauseTitle = "xr.pause.title";
        public const string PauseResume = "xr.pause.resume";
        public const string PauseHint = "xr.pause.hint";

        public const string ArrivalTitle = "xr.arrival.title";
        public const string ArrivalThisRun = "xr.arrival.this_run";
        public const string ArrivalBest = "xr.arrival.best";
        public const string ArrivalNewBest = "xr.arrival.new_best";
        public const string ArrivalOnTime = "xr.arrival.on_time";
        public const string ArrivalSlightDelay = "xr.arrival.slight_delay";
        public const string ArrivalDelayed = "xr.arrival.delayed";
        public const string ArrivalMap = "xr.arrival.map";
        public const string ArrivalRetry = "xr.arrival.retry";
        public const string ArrivalNext = "xr.arrival.next";
        public const string ArrivalToNetwork = "xr.arrival.to_network";

        public const string WristResume = "xr.wrist.resume";
        public const string WristRetry = "xr.wrist.retry";
        public const string WristLineMap = "xr.wrist.line_map";
        public const string WristNetwork = "xr.wrist.network";
        public const string WristSettings = "xr.wrist.settings";
        public const string WristClose = "xr.wrist.close";

        public const string SettingsHand = "xr.settings.hand";
        public const string SettingsLeft = "xr.settings.left";
        public const string SettingsRight = "xr.settings.right";
        public const string SettingsBoard = "xr.settings.board";
        public const string SettingsLower = "xr.settings.lower";
        public const string SettingsRaise = "xr.settings.raise";
        public const string SettingsReplace = "xr.settings.replace";
        public const string SettingsLanguage = "xr.settings.language";
        public const string SettingsMusic = "xr.settings.music";
        public const string SettingsEffects = "xr.settings.effects";
        public const string SettingsBack = "xr.settings.back";

        public const string PlacementPinch = "xr.placement.pinch";
        public const string PlacementNoTable = "xr.placement.no_table";
        public const string PlacementLook = "xr.placement.look";
    }
}
