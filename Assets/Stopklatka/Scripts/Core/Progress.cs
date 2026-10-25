using UnityEngine;

namespace Stopklatka
{
    /// <summary>
    /// Which levels you have cleared, remembered between sessions (PlayerPrefs, one bit per level).
    /// START plays the first level not cleared yet; the level cubes in the menu show the cleared ones.
    /// </summary>
    public static class Progress
    {
        const string Key = "stopklatka.levelsDone";
        const int MaxLevels = 30;

        static int Mask => PlayerPrefs.GetInt(Key, 0);

        /// <param name="level">1-based.</param>
        public static bool IsDone(int level) => level >= 1 && level <= MaxLevels && (Mask & (1 << (level - 1))) != 0;

        public static void MarkDone(int level)
        {
            if (level < 1 || level > MaxLevels || IsDone(level)) return;
            PlayerPrefs.SetInt(Key, Mask | (1 << (level - 1)));
            PlayerPrefs.Save();
        }

        public static bool AllDone(int count)
        {
            for (int i = 1; i <= count; i++) if (!IsDone(i)) return false;
            return count > 0;
        }

        /// <summary>The first level not cleared yet, or 1 when all are cleared (start again).</summary>
        public static int FirstUnfinished(int count)
        {
            for (int i = 1; i <= count; i++) if (!IsDone(i)) return i;
            return 1;
        }

        /// <summary>"POZIOM 1, POZIOM 3": the levels not cleared yet, for the menu.</summary>
        public static string Unfinished(int count)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= count; i++)
            {
                if (IsDone(i)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append("POZIOM ").Append(i);
            }
            return sb.ToString();
        }
    }
}
