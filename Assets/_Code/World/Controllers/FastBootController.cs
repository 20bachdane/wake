#if !UNITY_EDITOR && UNITY_WEBGL
#define USE_JSLIB
#endif // !UNITY_EDITOR && UNITY_WEBGL

using System;
using System.Collections;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using NativeUtils;
using UnityEngine;
using System.Runtime.InteropServices;
using TMPro;
using System.Collections.Generic;
using EasyAssetStreaming;

namespace Aqua {
    public class FastBootController : MonoBehaviour, ISceneLoadHandler {

        static private readonly StringHash32 NextPreloadGroup = "Scene/Title";

        #if USE_JSLIB

        [DllImport("__Internal")]
        static private extern void FastBoot_Initialize();

        #endif // USE_JSLIB

        private enum ReadyPhase {
            Loading,
            AudioClick,
            Ready
        }

        [Header("Loading")]
        public TMP_Text LoadingText;
        public TMP_Text ErrorText;
        
        [Header("Ready")]
        // public TMP_Text ReadyText;
        public TMP_Text PromptText;

        [Header("Run")]
        public AudioSource BootAudio;
        public ParticleSystem ClickParticles;

        [NonSerialized] private ReadyPhase m_ReadyPhase = 0;
        [NonSerialized] private List<StreamingAssetHandle> m_AssetPrefetch = new List<StreamingAssetHandle>(8);
        private Routine m_LoadTimeWarning;

        private void Awake() {
            NativeInput.OnMouseDown += OnNativeMouseDown;
            Services.Assets.PreloadGroup(NextPreloadGroup);
        }

        private void OnDestroy() {
            NativeInput.OnMouseDown -= OnNativeMouseDown;
        }
        
        private IEnumerator SwapToPrompt() {
            yield return Routine.Combine(
                LoadingText.FadeTo(0, 0.2f)
            );
            LoadingText.gameObject.SetActive(false);

            // ReadyText.gameObject.SetActive(true);
            PromptText.gameObject.SetActive(true);
            // ReadyText.alpha = 0;
            PromptText.alpha = 0;
            yield return Routine.Combine(
                PromptText.FadeTo(1, 0.2f)
                // ReadyText.FadeTo(1, 0.2f)
            );
        }

        private IEnumerator FadeInError() {
            yield return 15;

            ErrorText.gameObject.SetActive(true);
            ErrorText.alpha = 0;
            yield return ErrorText.FadeTo(1, 0.2f);
        }

        private void OnNativeMouseDown(float x, float y) {
            // Log.Msg("native click at {0}, {1}", x, y);

            if (m_ReadyPhase != ReadyPhase.AudioClick) {
                return;
            }
            
            #if USE_JSLIB
            FastBoot_Initialize();
            #endif // USE_JSLIB

            if (BootAudio != null) {
                BootAudio.Play();
            }

            if (ClickParticles != null) {
                Vector3 pos;
                pos.y = (y - 0.5f) * 10;
                pos.x = (x - 0.5f) * 10 * Services.Camera.AspectRatio;
                pos.z = 0;
                ClickParticles.transform.localPosition = pos;
                ClickParticles.Play();
            }

            m_ReadyPhase = ReadyPhase.Ready;
        }

        public void OnSceneLoad(SceneBinding inScene, object inContext) {
            int buildIdx = SceneHelper.ActiveScene().BuildIndex + 1;
            SceneBinding nextScene = SceneHelper.FindSceneByIndex(buildIdx);
            Async.InvokeAsync(() => {
                Services.State.LoadScene(nextScene, null, null, SceneLoadFlags.DoNotDispatchPreUnload | SceneLoadFlags.SuppressTriggers);
                Services.State.OnSceneLoadReady(SceneLoadReady);
            });
        }

        private IEnumerator SceneLoadReady() {
            Services.Assets.StreamingPreloadGroup(NextPreloadGroup, m_AssetPrefetch);

            m_LoadTimeWarning.Replace(this, FadeInError());

            while(Streaming.IsLoading()) {
                yield return 0.1f;
            }

            if (Streaming.ErrorCount() > 0) {
                while(Streaming.ErrorCount() > 0) {
                    Streaming.RetryErrored();
                    while(Streaming.IsLoading()) {
                        yield return 0.1f;
                    }
                }
            }

            while(!Services.Assets.PreloadGroupIsPrimaryLoaded(NextPreloadGroup)) {
                yield return 0.1f;
            }

            if (!ErrorText.gameObject.activeSelf) {
                m_LoadTimeWarning.Stop();
            }

            m_ReadyPhase = ReadyPhase.AudioClick;
            Routine.Start(this, SwapToPrompt());

            while (m_ReadyPhase < ReadyPhase.Ready) {
                yield return null;
            }

            if (ClickParticles != null) {
                yield return 0.75f;
            }

            var fader = Services.UI.WorldFaders.AllocFader();
            Services.State.OnLoad(() => {
                fader.Dispose();
            }, 0);
            yield return fader.Object.Show(Color.black, 0.3f);

            // LoadingIcon.Queue();

            if (BootAudio != null) {
                yield return BootAudio.WaitToComplete();
            }

            Services.Assets.CancelStreamingPreloadGroup(m_AssetPrefetch);
        }
    }
}