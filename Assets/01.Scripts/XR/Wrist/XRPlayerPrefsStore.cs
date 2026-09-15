using TrainSudoku.XR.Rules;
using UnityEngine;

namespace TrainSudoku.XR
{
    /// <summary>The headset's settings in PlayerPrefs, beside the board's anchor, under <c>tsugi.xr.*</c>.</summary>
    public sealed class XRPlayerPrefsStore : IPreferenceStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);

        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }
    }
}
