#if UNITY_WEBGL && !UNITY_EDITOR
#define FIREBASE
#endif // UNITY_WEBGL && !UNITY_EDITOR

using Aqua.Argumentation;
using Aqua.Modeling;
using Aqua.Portable;
using Aqua.Profile;
using Aqua.Scripting;
using Aqua.Shop;
using BeauUtil;
using BeauUtil.Services;
using ProtoAqua.ExperimentV2;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Aqua
{
    [ServiceDependency(typeof(EventService), typeof(ScriptingService))]
    public partial class AnalyticsService : ServiceBehaviour
    {
        private const string NoActiveJobId = "no-active-job";

        #region Inspector

        [SerializeField, Required] private string m_AppId = "AQUALAB";
        [SerializeField, Required] private string m_AppVersion = "6.2";
        
        #endregion // Inspector

        #region Firebase JS Functions

        //Progression
        [DllImport("__Internal")]
        public static extern void FBAcceptJob(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBSwitchJob(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string prevJobName);
        [DllImport("__Internal")]
        public static extern void FBReceiveFact(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string factId);
        [DllImport("__Internal")]
        public static extern void FBReceiveEntity(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string entityId);
        [DllImport("__Internal")]
        public static extern void FBCompleteJob(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBCompleteTask(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string taskId);

        //Player Actions
        [DllImport("__Internal")]
        public static extern void FBSceneChanged(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName,string sceneName);
        [DllImport("__Internal")]
        public static extern void FBRoomChanged(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string roomName);
        [DllImport("__Internal")]
        public static extern void FBBeginDive(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string siteId);
        [DllImport("__Internal")]
        public static extern void FBBeginModel(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBBeginSimulation(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBAskForHelp(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string nodeId);
        [DllImport("__Internal")]
        public static extern void FBTalkWithGuide(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string nodeId);
        
        //Bestiary Events
        [DllImport("__Internal")]
        public static extern void FBOpenBestiary(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBBestiaryOpenSpeciesTab(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBBestiaryOpenEnvironmentsTab(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBBestiaryOpenModelsTab(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBBestiarySelectSpecies(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string speciesId);
        [DllImport("__Internal")]
        public static extern void FBBestiarySelectEnvironment(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string environmentId);
        [DllImport("__Internal")]
        public static extern void FBBestiarySelectModel(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string modelId);
        [DllImport("__Internal")]
        public static extern void FBCloseBestiary(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);

        //Status Events
        [DllImport("__Internal")]
        public static extern void FBOpenStatus(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBStatusOpenJobTab(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBStatusOpenItemTab(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBStatusOpenTechTab(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBCloseStatus(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);

        //Game Feedback
        [DllImport("__Internal")]
        public static extern void FBSimulationSyncAchieved(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBGuideScriptTriggered(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string nodeId);
        [DllImport("__Internal")]
        public static extern void FBScriptFired(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string nodeId);

        // Modeling Events
        [DllImport("__Internal")]
        public static extern void FBModelingStart(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBModelPhaseChanged(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string phase);
        [DllImport("__Internal")]
        public static extern void FBModelEcosystemSelected(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string ecosystem);
        [DllImport("__Internal")]
        public static extern void FBModelConceptStarted(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string ecosystem);
        [DllImport("__Internal")]
        public static extern void FBModelConceptUpdated(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string ecosystem, string status);
        [DllImport("__Internal")]
        public static extern void FBModelConceptExported(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string ecosystem);
        [DllImport("__Internal")]
        public static extern void FBModelSyncError(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string ecosystem, int sync);
        [DllImport("__Internal")]
        public static extern void FBModelPredictCompleted(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string ecosystem);
        [DllImport("__Internal")]
        public static extern void FBModelInterveneUpdate(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string ecosystem, string organism, int differenceValue);
        [DllImport("__Internal")]
        public static extern void FBModelInterveneError(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string ecosystem);
        [DllImport("__Internal")]
        public static extern void FBModelInterveneCompleted(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string ecosystem);
        [DllImport("__Internal")]
        public static extern void FBModelingEnd(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string phase, string ecosystem);

        // Shop Events
        [DllImport("__Internal")]
        public static extern void FBPurchaseUpgrade(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string itemId, string itemName, int cost);
        [DllImport("__Internal")]
        public static extern void FBInsufficientFunds(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string itemId, string itemName, int cost);
        [DllImport("__Internal")]
        public static extern void FBTalkToShopkeep(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);

        // Experimentation Events
        [DllImport("__Internal")]
        public static extern void FBAddEnvironment(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string tankType, string environment);
        [DllImport("__Internal")]
        public static extern void FBRemoveEnvironment(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string tankType, string environment);
        [DllImport("__Internal")]
        public static extern void FBAddCritter(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string tankType, string environment, string critter);
        [DllImport("__Internal")]
        public static extern void FBRemoveCritter(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string tankType, string environment, string critter);
        [DllImport("__Internal")]
        public static extern void FBBeginExperiment(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string tankType, string environment, string critters, bool stabilizerEnabled, bool autofeederEnabled);
        [DllImport("__Internal")]
        public static extern void FBEndExperiment(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string tankType, string environment, string critters, bool stabilizerEnabled, bool autofeederEnabled);

        // Argumentation Events
        [DllImport("__Internal")]
        public static extern void FBBeginArgument(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBFactSubmitted(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string factId);
        [DllImport("__Internal")]
        public static extern void FBFactRejected(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName, string factId);
        [DllImport("__Internal")]
        public static extern void FBLeaveArgument(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);
        [DllImport("__Internal")]
        public static extern void FBCompleteArgument(int index, string userCode, string appVersion, string appFlavor, int logVersion, string jobName);

        #endregion // Firebase JS Functions

        #region Logging Variables

        private string m_UserCode = string.Empty;
        private string m_AppFlavor = string.Empty;
        private int m_LogVersion = 3;
        private int m_SequenceIndex = 1;
        private StringHash32 m_CurrentJobHash = null;
        private string m_CurrentJobName = NoActiveJobId;
        private string m_PreviousJobName = NoActiveJobId;
        private PortableAppId m_CurrentPortableAppId = PortableAppId.NULL;
        private BestiaryDescCategory? m_CurrentPortableBestiaryTabId = null;
        private string m_CurrentModelPhase = string.Empty;
        private string m_CurrentModelEcosystem = string.Empty;
        private string m_CurrentTankType = string.Empty;
        private string m_CurrentEnvironment = string.Empty;
        private List<string> m_CurrentCritters = new List<string>();
        private bool m_StabilizerEnabled = true;
        private bool m_AutoFeederEnabled = false;
        private StringHash32 m_CurrentArguementId = null;

        #endregion // Logging Variables

        #region IService

        protected override void Initialize()
        {
            Services.Events.Register<StringHash32>(GameEvents.JobStarted, LogAcceptJob, this)
                .Register<string>(GameEvents.ProfileStarting, SetUserCode, this)
                .Register(GameEvents.ProfileStarted, OnProfileStarted)
                .Register<StringHash32>(GameEvents.JobSwitched, LogSwitchJob, this)
                .Register<BestiaryUpdateParams>(GameEvents.BestiaryUpdated, HandleBestiaryUpdated, this)
                .Register<StringHash32>(GameEvents.JobCompleted, LogCompleteJob, this)
                .Register<StringHash32>(GameEvents.JobTaskCompleted, LogCompleteTask, this)
                .Register<string>(GameEvents.RoomChanged, LogRoomChanged, this)
                .Register<string>(GameEvents.ScriptFired, LogScriptFired, this)
                .Register<TankType>(ExperimentEvents.ExperimentBegin, LogBeginExperiment, this)
                .Register<string>(GameEvents.BeginDive, LogBeginDive, this)
                .Register(ModelingConsts.Event_Simulation_Begin, LogBeginSimulation, this)
                .Register(ModelingConsts.Event_Simulation_Complete, LogSimulationSyncAchieved, this)
                .Register<PortableAppId>(GameEvents.PortableAppOpened, PortableAppOpenedHandler, this)
                .Register<PortableAppId>(GameEvents.PortableAppClosed, PortableAppClosedHandler, this)
                // .Register<BestiaryDescCategory>(GameEvents.PortableBestiaryTabSelected, PortableBestiaryTabSelectedHandler, this)
                .Register(ModelingConsts.Event_Begin_Model, LogBeginModel, this)
                .Register<byte>(ModelingConsts.Event_Phase_Changed, LogModelPhaseChanged, this)
                .Register<string>(ModelingConsts.Event_Ecosystem_Selected, LogModelEcosystemSelected, this)
                .Register(ModelingConsts.Event_Concept_Started, LogModelConceptStarted, this)
                .Register<ConceptualModelState.StatusId>(ModelingConsts.Event_Concept_Updated, LogModelConceptUpdated, this)
                .Register(ModelingConsts.Event_Concept_Exported, LogModelConceptExported, this)
                .Register<int>(ModelingConsts.Event_Sync_Error, LogModelSyncError, this)
                .Register(ModelingConsts.Event_Predict_Complete, LogModelPredictCompleted, this)
                .Register<InterveneUpdateData>(ModelingConsts.Event_Intervene_Update, LogModelInterveneUpdate, this)
                .Register(ModelingConsts.Event_Intervene_Error, LogModelInterveneError, this)
                .Register(ModelingConsts.Event_Intervene_Complete, LogModelInterveneCompleted, this)
                .Register(ModelingConsts.Event_End_Model, LogEndModel, this)
                .Register<BestiaryDesc> (GameEvents.PortableEntrySelected, PortableBestiaryEntrySelectedhandler, this)
                .Register(GameEvents.ScenePreloading, ClearSceneState, this)
                .Register(GameEvents.PortableClosed, PortableClosed, this)
                .Register<StringHash32>(GameEvents.InventoryUpdated, LogPurchaseUpgrade, this)
                .Register<StringHash32>(ShopConsts.Event_InsufficientFunds, LogInsufficientFunds, this)
                .Register(ShopConsts.Event_TalkToShopkeep, LogTalkToShopkeep, this)
                .Register<TankType>(ExperimentEvents.ExperimentView, SetCurrentTankType, this)
                .Register<MeasurementTank.FeatureMask>(ExperimentEvents.ExperimentEnableFeature, SetTankFeatureEnabled, this)
                .Register<MeasurementTank.FeatureMask>(ExperimentEvents.ExperimentDisableFeature, SetTankFeatureDisabled, this)
                .Register<StringHash32>(ExperimentEvents.ExperimentAddEnvironment, LogAddEnvironment, this)
                .Register<StringHash32>(ExperimentEvents.ExperimentRemoveEnvironment, LogRemoveEnvironment, this)
                .Register<StringHash32>(ExperimentEvents.ExperimentAddCritter, LogAddCritter, this)
                .Register<StringHash32>(ExperimentEvents.ExperimentRemoveCritter, LogRemoveCritter, this)
                .Register<TankType>(ExperimentEvents.ExperimentEnded, LogEndExperiment, this)
                .Register<StringHash32>(ArgueEvents.Loaded, LogBeginArgument, this)
                .Register<StringHash32>(ArgueEvents.FactSubmitted, LogFactSubmitted, this)
                .Register<StringHash32>(ArgueEvents.FactRejected, LogFactRejected, this)
                .Register(ArgueEvents.Unloaded, LogLeaveArgument, this)
                .Register<StringHash32>(ArgueEvents.Completed, LogCompleteArgument,this);
                

            Services.Script.OnTargetedThreadStarted += GuideHandler;
            SceneHelper.OnSceneLoaded += LogSceneChanged;
        }

        private void SetUserCode(string userCode)
        {
            m_UserCode = userCode;
            m_AppFlavor = BuildInfo.Branch();
        }

        protected override void Shutdown()
        {
            Services.Events?.DeregisterAll(this);
        }
        #endregion // IService

        private void ClearSceneState()
        {
            m_CurrentPortableAppId = PortableAppId.NULL;
            m_CurrentPortableBestiaryTabId = null;
        }

        #region Log Events

        private void GuideHandler(ScriptThreadHandle inThread)
        {
            if (inThread.TargetId() != GameConsts.Target_Kevin)
            {
                return;
            }

            string nodeId = inThread.RootNodeName();

            if (inThread.TriggerId() == GameTriggers.RequestPartnerHelp)
            {
                LogAskForHelp(nodeId);
            }
            else
            {
                LogGuideScriptTriggered(nodeId);
            }
        }

        private void LogSceneChanged(SceneBinding scene, object context)
        {
            string sceneName = scene.Name;

            if (sceneName != "Boot" && sceneName != "Title")
            {
                #if FIREBASE
                FBSceneChanged(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, sceneName);
                #endif

                m_SequenceIndex++;
            }
        }

        private void LogRoomChanged(string roomName)
        {
            #if FIREBASE
            FBRoomChanged(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, roomName);
            #endif

            m_SequenceIndex++;
        }

        #region bestiary handlers
        private void PortableAppOpenedHandler(PortableAppId inId)
        {

            if (m_CurrentPortableAppId != inId)
                PortableAppClosedHandler(m_CurrentPortableAppId);

            m_CurrentPortableAppId = inId;
            switch(inId)
            {
                case PortableAppId.Organisms:
                    {
                        LogOpenBestiaryOrganisms();
                        break;
                    }

                case PortableAppId.Environments:
                    {
                        LogOpenBestiaryEnvironments();
                        break;
                    }

                case PortableAppId.Job:
                    {
                        LogOpenStatus();
                        LogStatusOpenJobTab();
                        break;
                    }

                case PortableAppId.Tech:
                    {
                        LogOpenStatus();
                        LogStatusOpenTechTab();
                        break;
                    }
            }
        }

        private void PortableAppClosedHandler(PortableAppId appId)
        {
            if (m_CurrentPortableAppId != appId)
                return;

            m_CurrentPortableAppId = PortableAppId.NULL;
            switch(appId)
            {
                case PortableAppId.Environments:
                case PortableAppId.Organisms:
                    {
                        m_CurrentPortableBestiaryTabId = null;
                        LogCloseBestiary();
                        break;
                    }

                case PortableAppId.Job:
                case PortableAppId.Tech:
                    {
                        LogCloseStatus();
                        break;
                    }
            }
        }

        private void PortableClosed()
        {
            if (m_CurrentPortableAppId != PortableAppId.NULL)
                PortableAppClosedHandler(m_CurrentPortableAppId);
        }

        private void PortableBestiaryTabSelectedHandler(BestiaryDescCategory tabName)
        {
            if (tabName == m_CurrentPortableBestiaryTabId) //Tab already open, don't send another log
                return;
            else
                m_CurrentPortableBestiaryTabId = tabName;

            switch (tabName)
            {
                case (BestiaryDescCategory.Critter): //Critter Tab
                    {
                        LogBestiaryOpenSpeciesTab();
                        break;
                    }
                case (BestiaryDescCategory.Environment): //Ecosystems Tab
                    {
                        LogBestiaryOpenEnvironmentsTab();
                        break;
                    }
                // case (BestiaryDescCategory.Model): //Models Tab
                //     {
                //         LogBestiaryOpenModelsTab();
                //         break;
                //     }
            }
        }

        private void PortableBestiaryEntrySelectedhandler(BestiaryDesc selectedData)
        {
            switch (selectedData.Category())
            {
                case (BestiaryDescCategory.Critter): //Critter Selected
                    {
                        LogBestiarySelectSpecies(selectedData.name);
                        break;
                    }
                case (BestiaryDescCategory.Environment): //Ecosystem Selected
                    {
                        LogBestiarySelectEnvironment(selectedData.name);
                        break;
                    }
                // case (BestiaryDescCategory.Model): //Model Selected
                //     {
                //         LogBestiarySelectModel(selectedData.name);
                //         break;
                //     }
            }
        }
        #endregion

        private void OnProfileStarted() {
            m_PreviousJobName = NoActiveJobId;
            SetCurrentJob(Save.CurrentJobId);
        }

        private bool SetCurrentJob(StringHash32 jobId)
        {
            m_CurrentJobHash = jobId;
            m_PreviousJobName = m_CurrentJobName;

            if (jobId.IsEmpty)
            {
                m_CurrentJobName = NoActiveJobId;
            }
            else
            {
                m_CurrentJobName = Assets.Job(jobId).name;
                if (m_PreviousJobName != NoActiveJobId)
                {
                    return true;
                }
            }

            return false;
        }

        private void LogAcceptJob(StringHash32 jobId)
        {
            if (SetCurrentJob(jobId))
            {
                #if FIREBASE
                FBSwitchJob(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_PreviousJobName);
                #endif

                m_SequenceIndex++;
            }

            #if FIREBASE
            FBAcceptJob(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogSwitchJob(StringHash32 jobId)
        {
            SetCurrentJob(jobId);

            #if FIREBASE
            FBSwitchJob(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_PreviousJobName);
            #endif

            m_SequenceIndex++;
        }

        private void HandleBestiaryUpdated(BestiaryUpdateParams inParams)
        {
            if (inParams.Type == BestiaryUpdateParams.UpdateType.Fact)
            {
                string parsedFactId = Assets.Fact(inParams.Id).name;

                #if FIREBASE
                FBReceiveFact(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, parsedFactId);
                #endif

                m_SequenceIndex++;
            }
            else if (inParams.Type == BestiaryUpdateParams.UpdateType.Entity)
            {
                string parsedEntityId = Assets.Bestiary(inParams.Id).name;

                #if FIREBASE
                FBReceiveEntity(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, parsedEntityId);
                #endif

                m_SequenceIndex++;
            }
        }

        private void LogCompleteJob(StringHash32 jobId)
        {
            string parsedJobName = Assets.Job(jobId).name;

            #if FIREBASE
            FBCompleteJob(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, parsedJobName);
            #endif

            m_SequenceIndex++;

            m_PreviousJobName = m_CurrentJobName;
            m_CurrentJobHash = null;
            m_CurrentJobName = NoActiveJobId;
        }

        private void LogCompleteTask(StringHash32 inTaskId)
        {
            string taskId = Assets.Job(m_CurrentJobHash).Task(inTaskId).IdString;

            #if FIREBASE
            FBCompleteTask(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, taskId);
            #endif

            m_SequenceIndex++;
        }

        private void LogBeginDive(string inTargetScene)
        {
            #if FIREBASE
            FBBeginDive(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, inTargetScene);
            #endif

            m_SequenceIndex++;
        }

        private void LogBeginModel()
        {
            #if FIREBASE
            FBBeginModel(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogBeginSimulation()
        {
            #if FIREBASE
            FBBeginSimulation(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogAskForHelp(string nodeId)
        {
            #if FIREBASE
            FBAskForHelp(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, nodeId);
            #endif

            m_SequenceIndex++;
        }

        private void LogTalkWithGuide(string nodeId)
        {
            #if FIREBASE
            FBTalkWithGuide(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, nodeId);
            #endif

            m_SequenceIndex++;
        }

        #region Bestiary App Logging
        private void LogOpenBestiaryOrganisms()
        {
            m_CurrentPortableBestiaryTabId = BestiaryDescCategory.Critter;

            #if FIREBASE
            FBOpenBestiary(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;

            LogBestiaryOpenSpeciesTab();
        }

        private void LogOpenBestiaryEnvironments()
        {
            m_CurrentPortableBestiaryTabId = BestiaryDescCategory.Environment;

            #if FIREBASE
            FBOpenBestiary(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;

            LogBestiaryOpenEnvironmentsTab();
        }

        private void LogBestiaryOpenSpeciesTab()
        {
            #if FIREBASE
            FBBestiaryOpenSpeciesTab(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }
        private void LogBestiaryOpenEnvironmentsTab()
        {
            #if FIREBASE
            FBBestiaryOpenEnvironmentsTab(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }
        private void LogBestiaryOpenModelsTab()
        {
            #if FIREBASE
            FBBestiaryOpenModelsTab(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogBestiarySelectSpecies(string speciesId)
        {
            #if FIREBASE
            FBBestiarySelectSpecies(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, speciesId);
            #endif

            m_SequenceIndex++;
        }
        private void LogBestiarySelectEnvironment(string environmentId)
        {
            #if FIREBASE
            FBBestiarySelectEnvironment(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, environmentId);
            #endif

            m_SequenceIndex++;
        }
        private void LogBestiarySelectModel(string modelId)
        {
            #if FIREBASE
            FBBestiarySelectModel(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, modelId);
            #endif

            m_SequenceIndex++;
        }
        private void LogCloseBestiary()
        {
            #if FIREBASE
            FBCloseBestiary(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }
        #endregion

        #region Status App Logging
        private void LogOpenStatus()
        {
            //m_CurrentPortableStatusTabId = StatusApp.PageId.Job;

            #if FIREBASE
            FBOpenStatus(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;

            LogStatusOpenJobTab(); //Status starts by opening tasks tab
        }

        private void LogStatusOpenJobTab()
        {
            #if FIREBASE
            FBStatusOpenJobTab(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogStatusOpenItemTab()
        {
            #if FIREBASE
            FBStatusOpenItemTab(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogStatusOpenTechTab()
        {
            #if FIREBASE
            FBStatusOpenTechTab(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogCloseStatus()
        {
            #if FIREBASE
            FBCloseStatus(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }
        #endregion

        private void LogSimulationSyncAchieved()
        {
            #if FIREBASE
            FBSimulationSyncAchieved(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogGuideScriptTriggered(string nodeId)
        {
            #if FIREBASE
            FBGuideScriptTriggered(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, nodeId);
            #endif

            m_SequenceIndex++;
        }

        private void LogScriptFired(string nodeId)
        {
            #if FIREBASE
            FBScriptFired(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, nodeId);
            #endif

            m_SequenceIndex++;
        }

        #region Modeling Events

        private void LogStartModel()
        {
            #if FIREBASE
            FBModelingStart(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogModelPhaseChanged(byte inPhase)
        {
            m_CurrentModelPhase = ((ModelPhases)inPhase).ToString();

            #if FIREBASE
            FBModelPhaseChanged(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelPhase);
            #endif

            m_SequenceIndex++;
        }

        private void LogModelEcosystemSelected(string ecosystem)
        {
            m_CurrentModelEcosystem = ecosystem;

            #if FIREBASE
            FBModelEcosystemSelected(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelEcosystem);
            #endif

            m_SequenceIndex++;
        }

        private void LogModelConceptStarted()
        {
            #if FIREBASE
            FBModelConceptStarted(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelEcosystem);
            #endif

            m_SequenceIndex++;
        }

        private void LogModelConceptUpdated(ConceptualModelState.StatusId status)
        {
            #if FIREBASE
            FBModelConceptUpdated(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelEcosystem, status.ToString());
            #endif

            m_SequenceIndex++;
        }

        private void LogModelConceptExported()
        {
            #if FIREBASE
            FBModelConceptExported(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelEcosystem);
            #endif

            m_SequenceIndex++;
        }

        private void LogModelSyncError(int sync)
        {
            #if FIREBASE
            FBModelSyncError(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelEcosystem, sync);
            #endif

            m_SequenceIndex++;
        }

        private void LogModelPredictCompleted()
        {
            #if FIREBASE
            FBModelPredictCompleted(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelEcosystem);
            #endif

            m_SequenceIndex++;
        }

        private void LogModelInterveneUpdate(InterveneUpdateData data)
        {
            #if FIREBASE
            FBModelInterveneUpdate(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelEcosystem, data.Organism, data.DifferenceValue);
            #endif

            m_SequenceIndex++;
        }

        private void LogModelInterveneError()
        {
            #if FIREBASE
            FBModelInterveneError(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelEcosystem);
            #endif

            m_SequenceIndex++;
        }

        private void LogModelInterveneCompleted()
        {
            #if FIREBASE
            FBModelInterveneCompleted(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelEcosystem);
            #endif

            m_SequenceIndex++;
        }

        private void LogEndModel()
        {
            #if FIREBASE
            FBModelingEnd(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentModelPhase, m_CurrentModelEcosystem);
            #endif

            m_SequenceIndex++;

            m_CurrentModelPhase = string.Empty;
            m_CurrentModelEcosystem = string.Empty;
        }

        #endregion // Modeling Events

        #region Shop Events

        private void LogPurchaseUpgrade(StringHash32 inUpgradeId)
        {
            InvItem item = Services.Assets.Inventory.Get(inUpgradeId);
            string name = item.name;

            if (name != "Cash" && name != "Exp")
            {
                int cost = item.CashCost();

                #if FIREBASE
                FBPurchaseUpgrade(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, inUpgradeId.ToString(), name, cost);
                #endif

                m_SequenceIndex++;
            }
        }

        private void LogInsufficientFunds(StringHash32 inUpgradeId)
        {
            InvItem item = Services.Assets.Inventory.Get(inUpgradeId);
            string name = item.name;
            int cost = item.CashCost();

            #if FIREBASE
            FBInsufficientFunds(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, inUpgradeId.ToString(), name, cost);
            #endif

            m_SequenceIndex++;
        }

        private void LogTalkToShopkeep()
        {
            #if FIREBASE
            FBTalkToShopkeep(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        #endregion // Shop Events

        #region Experimentation Events

        private void SetCurrentTankType(TankType inTankType)
        {
            m_CurrentTankType = inTankType.ToString();
        }

        private void SetTankFeatureEnabled(MeasurementTank.FeatureMask feature)
        {
            if (feature == MeasurementTank.FeatureMask.Stabilizer)
            {
                m_StabilizerEnabled = true;
            }
            else
            {
                m_AutoFeederEnabled = true;
            }
        }

        private void SetTankFeatureDisabled(MeasurementTank.FeatureMask feature)
        {
            if (feature == MeasurementTank.FeatureMask.Stabilizer)
            {
                m_StabilizerEnabled = false;
            }
            else
            {
                m_AutoFeederEnabled = false;
            }
        }

        private void LogAddEnvironment(StringHash32 inEnvironmentId)
        {
            string environment = Services.Assets.Bestiary.Get(inEnvironmentId).name;
            m_CurrentEnvironment = environment;

            #if FIREBASE
            FBAddEnvironment(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentTankType, environment);
            #endif

            m_SequenceIndex++;
        }

        private void LogRemoveEnvironment(StringHash32 inEnvironmentId)
        {
            string environment = Services.Assets.Bestiary.Get(inEnvironmentId).ToString();
            m_CurrentEnvironment = string.Empty;

            #if FIREBASE
            FBRemoveEnvironment(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentTankType, environment);
            #endif

            m_SequenceIndex++;
        }

        private void LogAddCritter(StringHash32 inCritterId)
        {
            string critter = Services.Assets.Bestiary.Get(inCritterId).name;
            m_CurrentCritters.Add(critter);

            #if FIREBASE
            FBAddCritter(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentTankType, m_CurrentEnvironment, critter);
            #endif

            m_SequenceIndex++;
        }

        private void LogRemoveCritter(StringHash32 inCritterId)
        {
            string critter = Services.Assets.Bestiary.Get(inCritterId).name;
            m_CurrentCritters.Remove(critter);

            #if FIREBASE
            FBRemoveCritter(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, m_CurrentTankType, m_CurrentEnvironment, critter);
            #endif

            m_SequenceIndex++;
        }

        private void LogBeginExperiment(TankType inTankType)
        {
            string tankType = inTankType.ToString();
            string critters = String.Join(",", m_CurrentCritters.ToArray());

            #if FIREBASE
            FBBeginExperiment(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, tankType, m_CurrentEnvironment, critters, m_StabilizerEnabled, m_AutoFeederEnabled);
            #endif

            m_SequenceIndex++;
        }

        private void LogEndExperiment(TankType inTankType)
        {
            string tankType = inTankType.ToString();
            string critters = String.Join(",", m_CurrentCritters.ToArray());

            #if FIREBASE
            FBEndExperiment(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, tankType, m_CurrentEnvironment, critters, m_StabilizerEnabled, m_AutoFeederEnabled);
            #endif

            m_SequenceIndex++;

            m_CurrentTankType = string.Empty;
            m_CurrentEnvironment = string.Empty;
            m_CurrentCritters = new List<string>();
            m_StabilizerEnabled = true;
            m_AutoFeederEnabled = false;
        }

        #endregion Experimentation Events

        #region Argumentation Events

        private void LogBeginArgument(StringHash32 id)
        {
            m_CurrentArguementId = id;

            #if FIREBASE
            FBBeginArgument(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogFactSubmitted(StringHash32 inFactId)
        {
            string factId = Assets.Fact(inFactId).name;

            #if FIREBASE
            FBFactSubmitted(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, factId);
            #endif

            m_SequenceIndex++;
        }

        private void LogFactRejected(StringHash32 inFactId)
        {
            string factId = Assets.Fact(inFactId).name;
            
            #if FIREBASE
            FBFactRejected(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName, factId);
            #endif

            m_SequenceIndex++;
        }

        private void LogLeaveArgument()
        {
            if (ArgumentationService.LeafIsComplete(m_CurrentArguementId)) return;

            #if FIREBASE
            FBLeaveArgument(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;
        }

        private void LogCompleteArgument(StringHash32 id)
        {
            #if FIREBASE
            FBCompleteArgument(m_SequenceIndex, m_UserCode, m_AppVersion, m_AppFlavor, m_LogVersion, m_CurrentJobName);
            #endif

            m_SequenceIndex++;

            m_CurrentArguementId = null;
        }

        #endregion // Argumentation

        #endregion // Log Events
    }
}
