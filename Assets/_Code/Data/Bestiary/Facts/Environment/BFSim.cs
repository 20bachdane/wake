using BeauUtil;
using UnityEngine;

namespace Aqua {
    [CreateAssetMenu(menuName = "Aqualab Content/Fact/Simulation")]
    public class BFSim : BFBase
    {
        #region Inspector

        [Header("Sync")]

        [KeyValuePair("Id", "Population")] public ActorCountU32[] InitialActors = null;
        public WaterPropertyBlockF32 InitialWater = default;
        [Range(1, 12)] public uint SyncTickCount = 10;

        [Header("Predict")]

        [Range(1, 12)] public uint PredictTickCount = 10;

        [Header("Resources")]

        public float OxygenPerTick = 4;
        public float CarbonDioxidePerTick = 4;

        [Header("Tweaks")]

        public float EatRateMultiplier = 1;
        public float ReproductionRateMultiplier = 1;
        public float DeathRateMultiplier = 1;

        #endregion // Inspector

        private BFSim() : base(BFTypeId.Sim) { }

        #region Behavior

        static public void Configure()
        {
            BFType.DefineAttributes(BFTypeId.Sim, BFShapeId.None, BFFlags.EnvironmentFact, BFDiscoveredFlags.All, null);
            BFType.DefineMethods(BFTypeId.Sim, null, null, null, null, null);
            BFType.DefineEditor(BFTypeId.Sim, null, BFMode.Internal);
        }

        #endregion // Behavior
    }
}