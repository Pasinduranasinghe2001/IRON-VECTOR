using UnityEngine;

namespace IronVector
{
    public enum GameMode
    {
        TeamDeathmatch = 0,
        SearchAndDestroy = 1,
        Domination = 2,
        FreeForAll = 3
    }

    public enum MatchState
    {
        Waiting = 0,
        Playing = 1,
        RoundEnd = 2,
        Ended = 3
    }

    public enum BombState
    {
        Unplanted = 0,
        Planted = 1,
        Exploded = 2,
        Defused = 3
    }

    /// <summary>4 COD4-style tactical strategies / perk loadouts.</summary>
    public enum TacticalStrategy
    {
        Breacher = 0,   // Stopping Power: +15% bullet damage
        Ghost = 1,      // UAV Jammer: hidden from enemy radar + steady aim
        Rusher = 2,     // Extreme Conditioning & Sleight of Hand: +18% move speed, -35% reload
        Juggernaut = 3  // Body Armor & Danger Close: 125 Max HP + stronger explosives
    }

    public static class MapCatalog
    {
        public static readonly string[] DisplayNames = { "AdminHall (HQ Building)", "Outpost Vector (Compound)" };
        public static readonly string[] SceneNames = { "AdminHall", "Arena" };
    }

    public static class StrategyInfo
    {
        public static readonly string[] Names =
        {
            "Assault · Stopping Power (+15% Dmg)",
            "Recon · Ghost (Stealth Radar)",
            "Rusher · Speed + Fast Reload",
            "Support · 125 Armor + Heavy Frags"
        };

        public static readonly string[] ShortNames = { "STOPPING POWER", "GHOST", "RUSHER", "JUGGERNAUT" };
    }

    public static class GameModeInfo
    {
        public static readonly string[] Names =
        {
            "Team Deathmatch",
            "Search & Destroy",
            "Domination",
            "Free For All"
        };
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
                default: return "SOLO";
            }
        }

        public static Color TeamColor(int team)
        {
            switch (team)
            {
                case Alpha: return new Color(0.30f, 0.62f, 1.00f);
                case Bravo: return new Color(1.00f, 0.36f, 0.26f);
                default: return new Color(0.95f, 0.78f, 0.28f);
            }
        }

        public static string Hex(int team) => ColorUtility.ToHtmlStringRGB(TeamColor(team));
    }
}
