using System;
using Evolution.Volume;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Evolution.Runtime
{
    public sealed partial class WaterPrototype
    {
        sealed class ColonyOceanEntry
        {
            public string ScenePath;
            public int Seed;
            public bool Fresh;
        }

        static ColonyOceanEntry pendingColonyOcean;
        AsyncOperation colonyOceanLoad;

        public ColonyChallenge ColonyAttempt { get; } = new ColonyChallenge();
        public bool IsFreshColonyOcean { get; private set; }
        internal bool CanReloadColonyOcean => gameObject.scene.buildIndex >= 0
            && Application.CanStreamedLevelBeLoaded(gameObject.scene.buildIndex);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetColonyOceanEntry() => pendingColonyOcean = null;

        void Awake()
        {
            var entry = pendingColonyOcean;
            if (entry == null || entry.ScenePath != gameObject.scene.path) return;
            pendingColonyOcean = null;
            IsFreshColonyOcean = entry.Fresh;
            Seed = entry.Seed;
            if (entry.Fresh) Paused = true;
        }

        public AsyncOperation StartFreshColonyOcean() => LoadColonyOcean(true);
        public AsyncOperation ReturnToSavedOcean() => IsFreshColonyOcean ? LoadColonyOcean(false) : null;

        AsyncOperation LoadColonyOcean(bool fresh)
        {
            if (colonyOceanLoad != null) return colonyOceanLoad;
            var scene = gameObject.scene;
            if (pendingColonyOcean != null || !CanReloadColonyOcean) return null;

            var entry = new ColonyOceanEntry { ScenePath = scene.path, Seed = Seed, Fresh = fresh };
            pendingColonyOcean = entry;
            try {
                colonyOceanLoad = SceneManager.LoadSceneAsync(scene.buildIndex, LoadSceneMode.Single);
            } catch (Exception error) {
                pendingColonyOcean = null;
                Debug.LogException(error);
                return null;
            }
            if (colonyOceanLoad == null) {
                pendingColonyOcean = null;
                return null;
            }
            bool wasPaused = Paused;
            Paused = true;
            var load = colonyOceanLoad;
            load.completed += operation => {
                if (ReferenceEquals(pendingColonyOcean, entry)) {
                    pendingColonyOcean = null;
                    Paused = wasPaused;
                }
                if (colonyOceanLoad == operation) colonyOceanLoad = null;
            };
            return load;
        }

        int FreshColonyFounder()
        {
            // The default seed has a natural founder with both viable birth paths.
            int candidate = Seed == 42 ? Ocean.Find("-1_1_-2/agent/1") : -1;
            if (ColonyChallenge.CanBegin(World, candidate) && World.ConnectionBlock(candidate) == null) return candidate;

            int nearest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < World.Count; i++) {
                if (!ColonyChallenge.CanBegin(World, i)) continue;
                var a = World.Get(i);
                if (a.Biology.Genes.Transport <= 0 || World.ConnectionBlock(i) != null || a.Position.Sqr >= distance) continue;
                nearest = i;
                distance = a.Position.Sqr;
            }
            return nearest >= 0 ? nearest : ScenicSelection();
        }

        public bool StartColonyChallenge()
        {
            if (!ColonyAttempt.Begin(World, selected)) return false;
            Challenge.Stop();
            return PlaySelected();
        }

        public bool RequestPlayerDivision() => ColonyAttempt.RequestDivision(World);

        public string PlayerDivisionBlock(int index)
        {
            if (ColonyAttempt.CanChoosePath(World) && World.Controlled == index
                && World.Get(index).PlannedConnection == BirthConnection.Natural) return "Выбери способ деления";
            return World.DivisionBlock(index);
        }

        public bool FollowPlayedOffspring()
        {
            int target = ColonyAttempt.OffspringToFollow(World, EvolutionPlay.Identity);
            if (target < 0 && ColonyAttempt.IsTrackedHandoff(EvolutionPlay.Identity)) return false;
            if (!FollowOffspring(target)) return false;
            if (target >= 0 && ColonyAttempt.Path == BirthConnection.Retain) World.ControlColony = true;
            return true;
        }
    }
}
