using System;
using System.Collections;
using System.Collections.Generic;
using AquaAudio;
using BeauData;
using BeauPools;
using BeauRoutine;
using BeauUtil;
using BeauUtil.IO;
using BeauUtil.Services;
using BeauUtil.Tags;
using BeauUtil.Variants;
using UnityEngine;

namespace Aqua.DebugConsole
{
    [ServiceDependency(typeof(ScriptingService), typeof(AudioMgr), typeof(DataService), typeof(UIMgr), typeof(StateMgr))]
    public partial class DebugService : ServiceBehaviour
    {
        #region Inspector

        [SerializeField, Required] private Canvas m_Canvas = null;
        [SerializeField] private KeyCode m_ToggleMinimalKey = KeyCode.BackQuote;
        [SerializeField, Required] private CanvasGroup m_MinimalLayer = null;
        [SerializeField, Required] private CanvasGroup m_KeyboardReference = null;
        [SerializeField, Required] private ConsoleTimeDisplay m_TimeDisplay = null;

        #endregion // Inspector

        [NonSerialized] private DeviceInput m_Input;
        [NonSerialized] private bool m_MinimalOn;

        private void LateUpdate()
        {
            if (m_Input == null)
                return;

            if (m_Input.KeyPressed(m_ToggleMinimalKey))
            {
                SetMinimalLayer(!m_MinimalOn);
            }

            if (m_Input.KeyPressed(KeyCode.Backslash))
            {
                m_KeyboardReference.alpha = 1 - m_KeyboardReference.alpha;
            }

            if (!Services.State.IsLoadingScene() && !Services.UI.IsLoadingScreenVisible() && !Services.UI.Popup.IsShowing())
            {
                if (m_Input.KeyPressed(KeyCode.Escape))
                {
                    Routine.Start(this, RequestQuit());
                }
                else if (m_Input.KeyPressed(KeyCode.F6))
                {
                    Routine.Start(this, TryModifyVars());
                }
            }

            if (m_Input.KeyDown(KeyCode.LeftShift))
            {
                if (m_Input.KeyPressed(KeyCode.Minus))
                {
                    SetTimescale(Time.timeScale / 2);
                }
                else if (m_Input.KeyPressed(KeyCode.Equals))
                {
                    if (Time.timeScale * 2 < 100)
                        SetTimescale(Time.timeScale * 2);
                }
                else if (m_Input.KeyPressed(KeyCode.Alpha0))
                {
                    SetTimescale(1);
                }
            }

            if (m_Input.KeyDown(KeyCode.LeftControl))
            {
                if (m_Input.KeyPressed(KeyCode.D))
                {
                    DumpScriptingState();
                }
                else if (m_Input.KeyPressed(KeyCode.F))
                {
                    ClearScriptingState();
                }
                else if (m_Input.KeyPressed(KeyCode.Return))
                {
                    TryReloadAssets();
                }
                else if (m_Input.KeyPressed(KeyCode.G))
                {
                    UnlockAllBestiaryEntries(false);
                }
                else if (m_Input.KeyPressed(KeyCode.H))
                {
                    UnlockAllBestiaryEntries(true);
                }
                else if (m_Input.KeyPressed(KeyCode.Space))
                {
                    SkipCutscene();
                }
            }
        }

        private void SetTimescale(float inTimeScale)
        {
            Time.timeScale = inTimeScale;
            Services.Audio.DebugMix.Pitch = inTimeScale;
            Services.Audio.DebugMix.Volume = Mathf.Clamp01(1 / inTimeScale);

            m_TimeDisplay.UpdateTimescale(inTimeScale);
        }

        private void SkipCutscene()
        {
            var cutscene = Services.Script.GetCutscene();
            if (cutscene.IsRunning())
            {
                cutscene.Skip();
            }
        }

        private void DumpScriptingState()
        {
            var resolver = (CustomVariantResolver) Services.Data.VariableResolver;
            using(PooledStringBuilder psb = PooledStringBuilder.Create())
            {
                psb.Builder.Append("[DebugService] Dumping Script State");
                foreach(var table in resolver.AllTables())
                {
                    psb.Builder.Append('\n').Append(table.ToDebugString());
                }

                psb.Builder.Append("\nAll Visited Nodes");
                foreach(var node in Services.Data.Profile.Script.ProfileNodeHistory)
                {
                    psb.Builder.Append("\n  ").Append(node.ToDebugString());
                }

                psb.Builder.Append("\nAll Visited in Current Session");
                foreach(var node in Services.Data.Profile.Script.SessionNodeHistory)
                {
                    psb.Builder.Append("\n  ").Append(node.ToDebugString());
                }

                psb.Builder.Append("\nRecent Node History");
                foreach(var node in Services.Data.Profile.Script.RecentNodeHistory)
                {
                    psb.Builder.Append("\n  ").Append(node.ToDebugString());
                }

                Debug.Log(psb.Builder.Flush());
            }
        }

        private void ClearScriptingState()
        {
            var resolver = (CustomVariantResolver) Services.Data.VariableResolver;
            foreach(var table in resolver.AllTables())
            {
                table.Clear();
            }
            Services.Data.Profile.Script.Reset();
            Debug.LogWarningFormat("[DebugService] Cleared all scripting state");
        }

        private void UnlockAllBestiaryEntries(bool inbIncludeFacts)
        {
            foreach(var entry in Services.Assets.Bestiary.Objects)
            {
                Services.Data.Profile.Bestiary.RegisterEntity(entry.Id());
                if (inbIncludeFacts)
                {
                    foreach(var fact in entry.Facts)
                        Services.Data.Profile.Bestiary.RegisterFact(fact.Id());
                }
            }
        }

        private IEnumerator RequestQuit()
        {
            Future<StringHash32> request = Services.UI.Popup.AskYesNo("Quit to Title Screen", "Return to the title screen?");
            yield return request;
            if (request == PopupPanel.Option_Yes)
            {
                Services.Script.KillAllThreads();
                Services.UI.HideAll();
                Services.Audio.StopAll();
                Services.State.LoadScene("DebugTitle");
            }
        }

        private IEnumerator TryModifyVars()
        {
            Future<PopupInputResult> request = Services.UI.Popup.CancelableTextEntry("Modify Vars", "Enter variable modification strings");
            yield return request;
            if (request.Get().Option == PopupPanel.Option_Submit)
            {
                Services.Data.VariableResolver.TryModify(null, request.Get().Input);
            }
        }

        private void SetMinimalLayer(bool inbOn)
        {
            m_MinimalOn = inbOn;
            m_MinimalLayer.alpha = inbOn ? 1 : 0;
            m_MinimalLayer.blocksRaycasts = inbOn;
            m_Canvas.enabled = inbOn;
        }

        #region Asset Reloading

        private void OnSceneLoaded(SceneBinding inBinding, object inContext)
        {
            TryReloadAssets();
        }

        private void TryReloadAssets()
        {
            ReloadableAssetCache.TryReloadAll();
        }

        #endregion // Asset Reloading

        #region IService

        protected override void Initialize()
        {
            #if PREVIEW
            SetMinimalLayer(false);
            #else
            SetMinimalLayer(m_MinimalLayer.alpha > 0);
            #endif // PREVIEW

            SceneHelper.OnSceneLoaded += OnSceneLoaded;

            m_Canvas.gameObject.SetActive(true);
            m_Input = BaseInputLayer.Find(m_Canvas).Device;
        }

        protected override void Shutdown()
        {
            SceneHelper.OnSceneLoaded -= OnSceneLoaded;
        }

        #endregion // IService
    }
}