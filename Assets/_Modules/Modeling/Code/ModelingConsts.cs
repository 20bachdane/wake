using BeauUtil;
using BeauUtil.Variants;

namespace Aqua.Modeling
{
    static public class ModelingConsts
    {
        static public readonly TableKeyPair Var_EcosystemSelected = TableKeyPair.Parse("modeling:ecosystemSelected");
        static public readonly TableKeyPair Var_HasJob = TableKeyPair.Parse("modeling:hasJob");
        static public readonly TableKeyPair Var_SimulationSync = TableKeyPair.Parse("modeling:simSync");
        static public readonly TableKeyPair Var_ModelPhase = TableKeyPair.Parse("modeling:phase");

        static public readonly StringHash32 Trigger_ConceptStarted = "VisualModelStarted";
        static public readonly StringHash32 Trigger_ConceptUpdated = "VisualModelUpdated";
        static public readonly StringHash32 Trigger_GraphStarted = "SimulationModelStarted";
        static public readonly StringHash32 Trigger_SyncError = "SimulationSyncError";
        static public readonly StringHash32 Trigger_SyncCompleted = "SimulationSyncSuccess";
        static public readonly StringHash32 Trigger_PredictCompleted = "SimulationPredictSuccess";
        static public readonly StringHash32 Trigger_InterveneError = "SimulationInterveneError";
        static public readonly StringHash32 Trigger_InterveneCompleted = "SimulationInterveneSuccess";

        static public readonly StringHash32 ModelPhase_Ecosystem = "ecosystem";
        static public readonly StringHash32 ModelPhase_Visual = "visual";
        static public readonly StringHash32 ModelPhase_Describe = "sync";
        static public readonly StringHash32 ModelPhase_Predict = "predict";
        static public readonly StringHash32 ModelPhase_Intervene = "intervene";

        static public readonly StringHash32 Event_Model_Begin = "modeling:model-begin";
        static public readonly StringHash32 Event_Simulation_Begin = "simulation:simulation-begin";
        static public readonly StringHash32 Event_Simulation_Complete = "simulation:simulation-complete";
    }
}