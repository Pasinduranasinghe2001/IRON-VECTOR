using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>
    /// Authoritative match state machine supporting 4 COD4 modes:
    /// Team Deathmatch, Search & Destroy (bomb plant/defuse, no mid-round respawn), Domination, and Free For All.
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("Rules")]
        public int tdmScoreLimit = 30;
        public int dominationScoreLimit = 200;
        public int ffaScoreLimit = 20;
        public int sndRoundsToWin = 4;
        public int matchDurationSeconds = 600;
        public int sndRoundSeconds = 120;
        public int sndBombFuseSeconds = 35;
        public float postMatchDuration = 9f;

        public readonly SyncVar<GameMode> Mode = new SyncVar<GameMode>(GameMode.TeamDeathmatch);
        public readonly SyncVar<MatchState> State = new SyncVar<MatchState>(MatchState.Playing);
        public readonly SyncVar<int> AlphaScore = new SyncVar<int>(0);
        public readonly SyncVar<int> BravoScore = new SyncVar<int>(0);
        public readonly SyncVar<int> TimeRemaining = new SyncVar<int>(600);
        public readonly SyncVar<int> WinnerTeam = new SyncVar<int>(Teams.None);
        public readonly SyncVar<string> WinnerName = new SyncVar<string>("");

        // Search & Destroy specific state
        public readonly SyncVar<int> CurrentRound = new SyncVar<int>(1);
        public readonly SyncVar<BombState> BombStatus = new SyncVar<BombState>(BombState.Unplanted);
        public readonly SyncVar<int> PlantedSiteIndex = new SyncVar<int>(-1);
        public readonly SyncVar<string> RoundBanner = new SyncVar<string>("");

        public bool IsFreeForAll => Mode.Value == GameMode.FreeForAll;
        public bool IsSearchAndDestroy => Mode.Value == GameMode.SearchAndDestroy;
        public bool DamageAllowed => State.Value == MatchState.Playing;
        public bool RespawnAllowed => State.Value == MatchState.Playing && !IsSearchAndDestroy;

        public int ScoreLimit
        {
            get
            {
                switch (Mode.Value)
                {
                    case GameMode.Domination: return dominationScoreLimit;
                    case GameMode.SearchAndDestroy: return sndRoundsToWin;
                    case GameMode.FreeForAll: return ffaScoreLimit;
                    default: return tdmScoreLimit;
                }
            }
        }

        float _secondAccumulator;
        float _bombSoundNext;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Mode.Value = NetworkGameServer.SelectedMode;
            ServerBeginMatch();
        }

        void ServerBeginMatch()
        {
            AlphaScore.Value = 0;
            BravoScore.Value = 0;
            CurrentRound.Value = 1;
            WinnerTeam.Value = Teams.None;
            WinnerName.Value = "";
            ServerBeginRoundState();
        }

        void ServerBeginRoundState()
        {
            BombStatus.Value = BombState.Unplanted;
            PlantedSiteIndex.Value = -1;
            RoundBanner.Value = "";
            TimeRemaining.Value = IsSearchAndDestroy ? sndRoundSeconds : matchDurationSeconds;
            State.Value = MatchState.Playing;
            _secondAccumulator = 0f;

            foreach (BombSite bs in BombSite.All)
                if (bs != null) bs.IsPlantedHere.Value = false;
        }

        void Update()
        {
            if (FxUtil.Enabled && IsSearchAndDestroy && BombStatus.Value == BombState.Planted && Time.time >= _bombSoundNext)
            {
                _bombSoundNext = Time.time + (TimeRemaining.Value <= 10 ? 0.35f : 0.85f);
                FxUtil.PlaySound2D(FxUtil.ClickClip, 0.35f, TimeRemaining.Value <= 10 ? 1.7f : 1.3f);
            }

            if (!IsServerInitialized || State.Value != MatchState.Playing) return;

            _secondAccumulator += Time.deltaTime;
            if (_secondAccumulator < 1f) return;
            _secondAccumulator -= 1f;

            TimeRemaining.Value = Mathf.Max(0, TimeRemaining.Value - 1);

            if (Mode.Value == GameMode.Domination)
            {
                int alphaPoints = 0, bravoPoints = 0;
                foreach (CapturePoint cp in CapturePoint.All)
                {
                    if (cp == null) continue;
                    if (cp.OwnerTeam.Value == Teams.Alpha) alphaPoints++;
                    else if (cp.OwnerTeam.Value == Teams.Bravo) bravoPoints++;
                }
                if (alphaPoints > 0) AlphaScore.Value = Mathf.Min(ScoreLimit, AlphaScore.Value + alphaPoints);
                if (bravoPoints > 0) BravoScore.Value = Mathf.Min(ScoreLimit, BravoScore.Value + bravoPoints);
                if (AlphaScore.Value >= ScoreLimit || BravoScore.Value >= ScoreLimit) ServerEndMatch();
            }
            else if (IsSearchAndDestroy)
            {
                if (BombStatus.Value == BombState.Planted)
                {
                    if (TimeRemaining.Value <= 0)
                        ServerBombExploded();
                }
                else
                {
                    if (TimeRemaining.Value <= 0)
                        ServerEndRound(Teams.Bravo, "TIME EXPIRED — DEFENDERS WIN");
                    else
                        CheckSndElimination();
                }
            }
            else
            {
                if (TimeRemaining.Value <= 0) ServerEndMatch();
            }
        }

        public void ServerOnKill(PlayerIdentity killer, PlayerIdentity victim, string source, bool headshot)
        {
            if (!IsServerInitialized) return;

            string killerName = killer != null ? killer.PlayerName.Value : "";
            int killerTeam = killer != null ? killer.Team.Value : Teams.None;
            string victimName = victim != null ? victim.PlayerName.Value : "Operator";
            int victimTeam = victim != null ? victim.Team.Value : Teams.None;

            ObserversKillFeed(killerName, killerTeam, victimName, victimTeam, source, headshot);

            if (State.Value != MatchState.Playing) return;

            if (Mode.Value == GameMode.TeamDeathmatch && killer != null && victim != null && killer != victim && killerTeam != victimTeam)
            {
                if (killerTeam == Teams.Alpha) AlphaScore.Value++;
                else if (killerTeam == Teams.Bravo) BravoScore.Value++;
                if (AlphaScore.Value >= ScoreLimit || BravoScore.Value >= ScoreLimit) ServerEndMatch();
            }
            else if (Mode.Value == GameMode.FreeForAll && killer != null && killer != victim)
            {
                int topKills = 0;
                foreach (PlayerIdentity p in PlayerIdentity.All)
                    if (p != null && p.Kills.Value > topKills) topKills = p.Kills.Value;
                AlphaScore.Value = topKills;
                if (topKills >= ScoreLimit)
                {
                    WinnerName.Value = killer.PlayerName.Value;
                    ServerEndMatch();
                }
            }
            else if (IsSearchAndDestroy)
            {
                CheckSndElimination();
            }
        }

        public void ServerBombPlanted(int siteIndex, PlayerIdentity planter)
        {
            if (!IsServerInitialized || !IsSearchAndDestroy || State.Value != MatchState.Playing || BombStatus.Value != BombState.Unplanted) return;

            BombStatus.Value = BombState.Planted;
            PlantedSiteIndex.Value = siteIndex;
            TimeRemaining.Value = sndBombFuseSeconds;

            foreach (BombSite bs in BombSite.All)
                if (bs != null) bs.IsPlantedHere.Value = (bs.SiteIndex.Value == siteIndex);

            if (planter != null) planter.Score.Value += 200;
            string siteLetter = siteIndex == 0 ? "A" : "B";
            ObserversKillFeed(planter != null ? planter.PlayerName.Value : "ALPHA", Teams.Alpha, $"SITE {siteLetter}", Teams.None, "BOMB PLANTED", false);
        }

        public void ServerBombDefused(PlayerIdentity defuser)
        {
            if (!IsServerInitialized || !IsSearchAndDestroy || State.Value != MatchState.Playing || BombStatus.Value != BombState.Planted) return;
            BombStatus.Value = BombState.Defused;
            if (defuser != null) defuser.Score.Value += 250;
            ObserversKillFeed(defuser != null ? defuser.PlayerName.Value : "BRAVO", Teams.Bravo, "BOMB", Teams.None, "DEFUSED", false);
            ServerEndRound(Teams.Bravo, "BOMB DEFUSED — BRAVO WINS ROUND");
        }

        void ServerBombExploded()
        {
            BombStatus.Value = BombState.Exploded;
            foreach (BombSite bs in BombSite.All)
            {
                if (bs != null && bs.SiteIndex.Value == PlantedSiteIndex.Value)
                {
                    Explosions.ServerApply(bs.transform.position, 18f, 250f, null, "C4 Detonation");
                    ObserversBombBoom(bs.transform.position);
                    break;
                }
            }
            ServerEndRound(Teams.Alpha, "TARGET DESTROYED — ALPHA WINS ROUND");
        }

        [ObserversRpc]
        void ObserversBombBoom(Vector3 pos) => FxUtil.Explosion(pos, 14f);

        void CheckSndElimination()
        {
            if (!IsSearchAndDestroy || State.Value != MatchState.Playing) return;
            int alphaTotal = 0, alphaAlive = 0, bravoTotal = 0, bravoAlive = 0;
            foreach (PlayerIdentity p in PlayerIdentity.All)
            {
                if (p == null) continue;
                bool alive = p.Health != null && p.Health.IsAlive;
                if (p.Team.Value == Teams.Alpha) { alphaTotal++; if (alive) alphaAlive++; }
                else if (p.Team.Value == Teams.Bravo) { bravoTotal++; if (alive) bravoAlive++; }
            }

            if (alphaTotal == 0 || bravoTotal == 0) return; // Don't end rounds when playing 1-player solo test.
            if (bravoAlive == 0)
                ServerEndRound(Teams.Alpha, "DEFENDERS ELIMINATED — ALPHA WINS");
            else if (alphaAlive == 0 && BombStatus.Value != BombState.Planted)
                ServerEndRound(Teams.Bravo, "ATTACKERS ELIMINATED — BRAVO WINS");
        }

        void ServerEndRound(int winningTeam, string message)
        {
            if (State.Value != MatchState.Playing) return;
            if (winningTeam == Teams.Alpha) AlphaScore.Value++;
            else if (winningTeam == Teams.Bravo) BravoScore.Value++;

            RoundBanner.Value = message;
            if (AlphaScore.Value >= ScoreLimit || BravoScore.Value >= ScoreLimit)
            {
                ServerEndMatch();
                return;
            }

            State.Value = MatchState.RoundEnd;
            StartCoroutine(NextRoundRoutine());
        }

        IEnumerator NextRoundRoutine()
        {
            yield return new WaitForSeconds(4.0f);
            if (!IsServerInitialized) yield break;

            CurrentRound.Value++;
            foreach (PlayerIdentity p in PlayerIdentity.All)
                if (p != null && p.Health != null) p.Health.ServerRespawn();

            ServerBeginRoundState();
        }

        void ServerEndMatch()
        {
            if (State.Value == MatchState.Ended) return;
            State.Value = MatchState.Ended;

            if (IsFreeForAll)
            {
                PlayerIdentity top = null;
                foreach (PlayerIdentity p in PlayerIdentity.All)
                    if (p != null && (top == null || p.Kills.Value > top.Kills.Value)) top = p;
                WinnerName.Value = top != null ? top.PlayerName.Value : "OPERATOR";
                WinnerTeam.Value = Teams.None;
            }
            else
            {
                if (AlphaScore.Value > BravoScore.Value) WinnerTeam.Value = Teams.Alpha;
                else if (BravoScore.Value > AlphaScore.Value) WinnerTeam.Value = Teams.Bravo;
                else WinnerTeam.Value = Teams.None;
            }

            StartCoroutine(RestartAfterPostMatch());
        }

        IEnumerator RestartAfterPostMatch()
        {
            yield return new WaitForSeconds(postMatchDuration);
            if (!IsServerInitialized) yield break;

            foreach (CapturePoint cp in CapturePoint.All)
                if (cp != null) cp.ServerReset();

            foreach (PlayerIdentity p in PlayerIdentity.All)
            {
                if (p == null) continue;
                p.Kills.Value = 0;
                p.Deaths.Value = 0;
                p.Score.Value = 0;
                if (p.Health != null) p.Health.ServerRespawn();
            }

            ServerBeginMatch();
        }

        [ObserversRpc]
        void ObserversKillFeed(string killerName, int killerTeam, string victimName, int victimTeam, string source, bool headshot)
        {
            GameHUD.AddKillFeed(killerName, killerTeam, victimName, victimTeam, source, headshot);
        }
    }
}
