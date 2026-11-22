using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace IronVector
{
    /// <summary>Tracks active game mode, team scores, match countdown, Domination ticks and killfeed.</summary>
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("Rules")]
        public int tdmScoreLimit = 30;
        public int dominationScoreLimit = 200;
        public int matchDurationSeconds = 600;
        public float postMatchDuration = 10f;

        public readonly SyncVar<GameMode> Mode = new SyncVar<GameMode>(GameMode.TeamDeathmatch);
        public readonly SyncVar<MatchState> State = new SyncVar<MatchState>(MatchState.Playing);
        public readonly SyncVar<int> AlphaScore = new SyncVar<int>(0);
        public readonly SyncVar<int> BravoScore = new SyncVar<int>(0);
        public readonly SyncVar<int> TimeRemaining = new SyncVar<int>(600);
        public readonly SyncVar<int> WinnerTeam = new SyncVar<int>(Teams.None);

        public bool DamageAllowed => State.Value == MatchState.Playing;
        public bool RespawnAllowed => State.Value != MatchState.Ended;
        public int ScoreLimit => Mode.Value == GameMode.Domination ? dominationScoreLimit : tdmScoreLimit;

        float _secondAccumulator;

        void Awake()
        {
            Instance = this;
        }

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
            TimeRemaining.Value = matchDurationSeconds;
            WinnerTeam.Value = Teams.None;
            State.Value = MatchState.Playing;
            _secondAccumulator = 0f;
        }

        void Update()
        {
            if (!IsServerInitialized || State.Value != MatchState.Playing) return;

            _secondAccumulator += Time.deltaTime;
            if (_secondAccumulator < 1f) return;
            _secondAccumulator -= 1f;

            TimeRemaining.Value = Mathf.Max(0, TimeRemaining.Value - 1);

            if (Mode.Value == GameMode.Domination)
            {
                int alphaPoints = 0;
                int bravoPoints = 0;
                foreach (CapturePoint cp in CapturePoint.All)
                {
                    if (cp == null) continue;
                    if (cp.OwnerTeam.Value == Teams.Alpha) alphaPoints++;
                    else if (cp.OwnerTeam.Value == Teams.Bravo) bravoPoints++;
                }
                if (alphaPoints > 0) AlphaScore.Value = Mathf.Min(ScoreLimit, AlphaScore.Value + alphaPoints);
                if (bravoPoints > 0) BravoScore.Value = Mathf.Min(ScoreLimit, BravoScore.Value + bravoPoints);
            }

            if (AlphaScore.Value >= ScoreLimit || BravoScore.Value >= ScoreLimit || TimeRemaining.Value <= 0)
                ServerEndMatch();
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

                if (AlphaScore.Value >= ScoreLimit || BravoScore.Value >= ScoreLimit)
                    ServerEndMatch();
            }
        }

        void ServerEndMatch()
        {
            if (State.Value == MatchState.Ended) return;
            State.Value = MatchState.Ended;

            if (AlphaScore.Value > BravoScore.Value) WinnerTeam.Value = Teams.Alpha;
            else if (BravoScore.Value > AlphaScore.Value) WinnerTeam.Value = Teams.Bravo;
            else WinnerTeam.Value = Teams.None;

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
