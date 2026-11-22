using UnityEngine;

namespace IronVector
{
    public enum GameMode
    {
        TeamDeathmatch = 0,
        Domination = 1
    }

    public enum MatchState
    {
        Waiting = 0,
        Playing = 1,
        Ended = 2
    }

    /// <summary>Team ids and helpers. Teams are plain ints so they sync cheaply.</summary>
    public static class Teams
    {
        public const int None = -1;
        public const int Alpha = 0;
        public const int Bravo = 1;

        public static string Name(int team)
        {
            switch (team)
            {
                case Alpha: return "ALPHA";
                case Bravo: return "BRAVO";
                default: return "NEUTRAL";
            }
        }

        public static Color TeamColor(int team)
        {
            switch (team)
            {
                case Alpha: return new Color(0.30f, 0.60f, 1.00f);
                case Bravo: return new Color(1.00f, 0.38f, 0.28f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        public static string Hex(int team) => ColorUtility.ToHtmlStringRGB(TeamColor(team));
    }
}
