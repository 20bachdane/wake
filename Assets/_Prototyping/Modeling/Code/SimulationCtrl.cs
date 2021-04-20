using System;
using Aqua;
using Aqua.Debugging;
using BeauUtil;
using UnityEngine;

namespace ProtoAqua.Modeling
{
    public class SimulationCtrl : MonoBehaviour, ISceneLoadHandler, IInputHandler
    {
        #region Inspector

        [SerializeField, Required] private ModelingUI m_ModelingUI = null;
        [SerializeField, Required] private SimulationUI m_SimulationUI = null;
        [SerializeField, Required] private BaseInputLayer m_Input = null;

        #pragma warning disable CS0414

        [Header("-- DEBUG -- ")]
        [SerializeField] private ModelingScenarioData m_TestScenario = null;

        #pragma warning restore CS0414
        
        #endregion // Inspector

        [NonSerialized] private SimulationBuffer m_Buffer;
        [NonSerialized] private ModelingState m_State;
        [NonSerialized] private bool m_BufferDirty = false;

        void ISceneLoadHandler.OnSceneLoad(SceneBinding inScene, object inContext)
        {
            ModelingScenarioData scenario = Services.Data.CurrentJob()?.Job.FindAsset<ModelingScenarioData>();
            
            #if UNITY_EDITOR
            if (!scenario && BootParams.BootedFromCurrentScene)
                scenario = m_TestScenario;
            #endif // UNITY_DEITOR

            m_Buffer = new SimulationBuffer();

            if (DebugService.IsLogging(LogMask.Modeling))
                m_Buffer.Flags = SimulatorFlags.Debug;
            
            m_Buffer.SetScenario(scenario);

            m_ModelingUI.SetScenario(scenario, scenario && BootParams.BootedFromCurrentScene);
            m_ModelingUI.PopulateMap(Services.Data.Profile.Bestiary);
            m_ModelingUI.ConceptMap.SetBuffer(m_Buffer);

            m_ModelingUI.ScenarioPanel.OnSimulateSelect = OnScenarioSimulateClick;
            m_SimulationUI.OnAdvanceClicked = OnSimulationAdvanceClicked;
            m_SimulationUI.OnBackClicked = OnSimulationBackClicked;

            m_ModelingUI.gameObject.SetActive(true);
            m_SimulationUI.gameObject.SetActive(false);

            Services.Data.SetVariable(SimulationConsts.Var_HasScenario, m_Buffer.Scenario() != null);
            SyncPhaseScriptVar();

            m_Buffer.OnUpdate = () => m_BufferDirty = true;
            OnBufferUpdated();

            m_Input.Device.RegisterHandler(this);
        }

        private void LateUpdate()
        {
            if (m_BufferDirty)
            {
                m_BufferDirty = false;
                OnBufferUpdated();
            }
        }

        private void OnBufferUpdated()
        {
            var updateFlags = m_Buffer.Refresh();

            if (updateFlags == 0)
                return;

            switch(m_State.Phase)
            {
                case ModelingPhase.Universal:
                    {
                        m_ModelingUI.OnBufferUpdate(m_Buffer, updateFlags);
                        break;
                    }

                case ModelingPhase.Sync:
                case ModelingPhase.Predict:
                    {
                        UpdateSync();
                        m_SimulationUI.Refresh(m_State, updateFlags);
                        break;
                    }
            }
        }

        private void UpdateSync()
        {
            float error = m_Buffer.CalculateModelError();
            int sync = 100 - Mathf.CeilToInt(error * 100);
            
            if (m_State.Phase == ModelingPhase.Sync && m_State.ModelSync != 100 && sync == 100)
            {
                Services.Audio.PostEvent("modelSync");
            }

            m_State.ModelSync = sync;
            
            error = m_Buffer.CalculatePredictionError();
            sync = 100 - Mathf.CeilToInt(error * 100);
            
            if (m_State.Phase == ModelingPhase.Predict && m_State.PredictSync != 100 && sync == 100)
            {
                Services.Audio.PostEvent("modelSync");
            }

            m_State.PredictSync = sync;

            Services.Data.SetVariable(SimulationConsts.Var_ModelSync, m_State.ModelSync);
            Services.Data.SetVariable(SimulationConsts.Var_PredictSync, m_State.PredictSync);
        }

        private void OnScenarioSimulateClick()
        {
            m_ModelingUI.gameObject.SetActive(false);
            m_SimulationUI.gameObject.SetActive(true);

            m_State.Phase = ModelingPhase.Sync;
            SyncPhaseScriptVar();

            m_SimulationUI.SetBuffer(m_Buffer);
            m_SimulationUI.Refresh(m_State, SimulationBuffer.UpdateFlags.ALL);
            m_SimulationUI.DisplayInitial();
        }

        private void OnSimulationAdvanceClicked()
        {
            switch(m_State.Phase)
            {
                case ModelingPhase.Sync:
                    {
                        if (m_State.ModelSync < 100)
                        {
                            Services.Audio.PostEvent("syncDenied");
                            break;
                        }

                        AdvanceToPredict();
                        break;
                    }

                case ModelingPhase.Predict:
                    {
                        if (m_State.PredictSync < 100)
                        {
                            Services.Audio.PostEvent("syncDenied");
                            break;
                        }

                        CompleteActivity();
                        break;
                    }
            }
        }

        private void OnSimulationBackClicked()
        {
            m_ModelingUI.gameObject.SetActive(true);
            m_SimulationUI.gameObject.SetActive(false);

            m_State.Phase = ModelingPhase.Universal;
            SyncPhaseScriptVar();

            m_Buffer.ClearFacts();
            m_Buffer.ClearSelectedCritters();

            m_ModelingUI.ScenarioPanel.ForceShow();
        }

        private void AdvanceToPredict()
        {
            // if no prediction is necessary, just complete it
            if (m_Buffer.Scenario().PredictionTicks() <= 0)
            {
                CompleteActivity();
                return;
            }

            m_State.Phase = ModelingPhase.Predict;
            SyncPhaseScriptVar();
            m_SimulationUI.SwitchToPredict();

            Services.Audio.PostEvent("modelSynced");
            Services.Script.TriggerResponse(SimulationConsts.Trigger_Synced);
        }

        private void CompleteActivity()
        {
            m_State.Phase = ModelingPhase.Completed;
            SyncPhaseScriptVar();

            StringHash32 fact = m_Buffer.Scenario().BestiaryModelId();
            if (!fact.IsEmpty)
                Services.Data.Profile.Bestiary.RegisterFact(fact);
            m_SimulationUI.Complete();

            Services.Audio.PostEvent("predictionSynced");
            Services.Script.TriggerResponse(SimulationConsts.Trigger_Completed);

            m_Input.Device.DeregisterHandler(this);
        }

        private void SyncPhaseScriptVar()
        {
            switch(m_State.Phase)
            {
                case ModelingPhase.Universal:
                    Services.Data.SetVariable(SimulationConsts.Var_ModelPhase, SimulationConsts.ModelPhase_Universal);
                    break;

                case ModelingPhase.Sync:
                    Services.Data.SetVariable(SimulationConsts.Var_ModelPhase, SimulationConsts.ModelPhase_Model);
                    break;

                case ModelingPhase.Predict:
                    Services.Data.SetVariable(SimulationConsts.Var_ModelPhase, SimulationConsts.ModelPhase_Predict);
                    break;

                case ModelingPhase.Completed:
                    Services.Data.SetVariable(SimulationConsts.Var_ModelPhase, SimulationConsts.ModelPhase_Completed);
                    break;
            }
        }

        void IInputHandler.HandleInput(DeviceInput inInput)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            
            if (inInput.KeyPressed(KeyCode.F8) || inInput.KeyPressed(KeyCode.R))
            {
                m_Buffer.Flags |= SimulatorFlags.Debug;
                m_Buffer.ReloadScenario();
            }

            #endif // UNITY_EDITOR || DEVELOPMENT_BUILD
        }
    }
}