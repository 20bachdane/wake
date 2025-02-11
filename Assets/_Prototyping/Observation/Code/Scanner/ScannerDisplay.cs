using System;
using UnityEngine;
using BeauRoutine;
using System.Collections;
using UnityEngine.UI;
using Aqua;
using BeauUtil;
using EasyAssetStreaming;
using AquaAudio;

namespace ProtoAqua.Observation
{
    public class ScannerDisplay : SharedPanel
    {
        private const float LoopStartingVolume = 0.5f;
        private const float LoopStartingPitch = 0.5f;
        private const float LoopFadeTime = 0.1f;

        #region Inspector

        [Header("Scanner")]

        [SerializeField] private Image m_Background = null;

        [Header("Scanning")]
        [SerializeField] private CanvasGroup m_ScanningGroup = null;
        [SerializeField] private Image m_ScanProgressBar = null;

        [Header("Data")]
        [SerializeField] private CanvasGroup m_DataGroup = null;
        [SerializeField] private LocText m_HeaderText = null;
        [SerializeField] private StreamedImageSetDisplay m_ImageDisplay = null;
        [SerializeField] private LocText m_DescriptionText = null;

        #endregion // Inspector

        [NonSerialized] private ScanData m_CurrentScanData;
        [NonSerialized] private Routine m_BounceRoutine;
        [NonSerialized] private Routine m_TypeRoutine;
        [NonSerialized] private AudioHandle m_ScanLoop;
        [NonSerialized] private bool m_IsInProgress;

        [NonSerialized] private Color m_DefaultBackgroundColor;
        [NonSerialized] private Color m_DefaultHeaderColor;
        [NonSerialized] private Color m_DefaultTextColor;
        [NonSerialized] private Vector4 m_DefaultTextMargins;

        protected override void Awake()
        {
            base.Awake();

            m_DefaultBackgroundColor = m_Background.color;
            m_DefaultHeaderColor = m_HeaderText.Graphic.color;
            m_DefaultTextColor = m_DescriptionText.Graphic.color;
            m_DefaultTextMargins = m_DescriptionText.Graphic.margin;
        }

        #region Scanning

        public void ShowProgress(float inProgress)
        {
            bool wasShowing = m_RootTransform.gameObject.activeSelf;
            Show();

            m_CurrentScanData = null;

            if (wasShowing && !m_IsInProgress)
            {
                m_BounceRoutine.Replace(this, BounceAnim());
            }

            m_DataGroup.gameObject.SetActive(false);

            if (!m_ScanningGroup.gameObject.activeSelf)
            {
                m_Background.SetColor(m_DefaultBackgroundColor);
                m_HeaderText.Graphic.SetColor(m_DefaultHeaderColor);
                m_DescriptionText.Graphic.SetColor(m_DefaultBackgroundColor);

                m_ScanningGroup.gameObject.SetActive(true);

                LayoutRebuilder.MarkLayoutForRebuild(m_RootTransform);
            }

            if (!m_IsInProgress)
            {
                m_IsInProgress = true;
                m_ScanLoop = Services.Audio.PostEvent("ROV.Scanner.Loop");
                m_ScanLoop.SetVolume(LoopStartingVolume).SetVolume(1, LoopFadeTime);
                m_ScanLoop.SetPitch(LoopStartingPitch).SetPitch(1, LoopFadeTime);
            }

            m_ScanProgressBar.fillAmount = inProgress;
        }

        public void ShowScan(ScanData inData, Sprite inImageOverride, ScanResult inResult)
        {
            Show();

            if (m_IsInProgress)
            {
                m_IsInProgress = false;
                m_ScanLoop.SetPitch(LoopStartingPitch, LoopFadeTime);
                m_ScanLoop.Stop(LoopFadeTime);
            }

            var mgr = ScanSystem.Find<ScanSystem>();
            var config = mgr.GetConfig(inData == null ? 0 : inData.Flags());

            m_CurrentScanData = inData;
            m_BounceRoutine.Replace(this, BounceAnim());

            m_ScanningGroup.gameObject.SetActive(false);
            m_TypeRoutine.Stop();

            m_DataGroup.gameObject.SetActive(true);
            if (inData == null)
            {
                m_HeaderText.SetTextFromString("Missing Scan");
                m_ImageDisplay.gameObject.SetActive(false);
                m_DescriptionText.SetTextFromString("This scan data was either not set or not loaded.");
            }
            else
            {
                string header = inData.Header();
                if (string.IsNullOrEmpty(header))
                {
                    m_HeaderText.SetTextFromString(string.Empty);
                    m_HeaderText.gameObject.SetActive(false);

                    m_DescriptionText.Graphic.margin = Vector4.zero;
                }
                else
                {
                    m_HeaderText.SetTextFromString(header);
                    m_HeaderText.gameObject.SetActive(true);

                    m_DescriptionText.Graphic.margin = m_DefaultTextMargins;
                }

                string text = inData.Text();
                if (string.IsNullOrEmpty(text))
                {
                    m_DescriptionText.SetTextFromString(string.Empty);
                    m_DescriptionText.gameObject.SetActive(false);
                }
                else
                {
                    m_DescriptionText.SetTextFromString(text);
                    m_DescriptionText.gameObject.SetActive(true);

                    m_DescriptionText.Graphic.maxVisibleCharacters = 0;

                    m_TypeRoutine.Replace(this, TypeOut(inData.TypingDuration()));
                }

                StreamedImageSet imageSet;
                if (inImageOverride != null)
                {
                    imageSet = inImageOverride;
                }
                else
                {
                    BestiaryDesc bestiary = Assets.Bestiary(inData.BestiaryId());
                    imageSet = new StreamedImageSet(string.IsNullOrEmpty(inData.ImagePath()) ? bestiary?.SketchPath() : null, bestiary?.Icon());
                }
                
                if (imageSet.IsEmpty)
                {
                    m_ImageDisplay.gameObject.SetActive(false);
                    m_ImageDisplay.Clear();
                }
                else
                {
                    m_ImageDisplay.Display(imageSet);
                    m_ImageDisplay.gameObject.SetActive(true);
                }
            }

            m_Background.SetColor(config.BackgroundColor);
            m_HeaderText.Graphic.SetColor(config.HeaderColor);
            m_DescriptionText.Graphic.SetColor(config.TextColor);

            LayoutRebuilder.ForceRebuildLayoutImmediate(m_RootTransform);

            Services.Audio.PostEvent(config.OpenSound);
        }

        public void CancelIfProgress()
        {
            if (m_CurrentScanData == null)
            {
                Hide();
            }
        }

        #endregion // Scanning

        #region Animations

        protected override void InstantTransitionToShow()
        {
            m_RootTransform.gameObject.SetActive(true);
            m_RootTransform.SetScale(1);
            m_RootGroup.alpha = 1;
        }

        protected override IEnumerator TransitionToShow()
        {
            if (!m_RootTransform.gameObject.activeSelf)
            {
                m_RootGroup.alpha = 0;
                m_RootTransform.SetScale(new Vector3(0.25f, 0.5f), Axis.XY);
                m_RootTransform.gameObject.SetActive(true);
            }

            yield return Routine.Combine(
                m_RootGroup.FadeTo(1, 0.25f),
                m_RootTransform.ScaleTo(1, 0.25f, Axis.XY).Ease(Curve.BackOut)
            );
        }

        protected override void InstantTransitionToHide()
        {
            m_RootTransform.gameObject.SetActive(false);
            m_RootTransform.SetScale(1);
            m_RootGroup.alpha = 0;
        }

        protected override IEnumerator TransitionToHide()
        {
            if (m_RootTransform.gameObject.activeSelf)
            {
                yield return Routine.Combine(
                    m_RootGroup.FadeTo(0, 0.1f),
                    m_RootTransform.ScaleTo(new Vector2(0.25f, 0.5f), 0.1f, Axis.XY).Ease(Curve.CubeIn)
                );

                m_RootTransform.gameObject.SetActive(false);
            }
        }

        private IEnumerator BounceAnim()
        {
            return m_RootTransform.AnchorPosTo(-15, 0.1f, Axis.Y).Ease(Curve.BackOut).From().ForceOnCancel();
        }

        private IEnumerator TypeOut(float inTypingDurationMultiplier)
        {
            return Tween.Int(0, m_DescriptionText.Metrics.VisibleCharCount, (c) => m_DescriptionText.Graphic.maxVisibleCharacters = c, 0.8f * inTypingDurationMultiplier);
        }

        #endregion // Animations

        #region Callbacks

        protected override void OnHide(bool inbInstant)
        {
            m_TypeRoutine.Stop();
            m_CurrentScanData = null;
            m_BounceRoutine.Stop();

            if (m_IsInProgress)
            {
                m_IsInProgress = false;
                m_ScanLoop.SetPitch(LoopStartingPitch, LoopFadeTime);
                m_ScanLoop.Stop(LoopFadeTime);
            }
        }

        protected override void OnHideComplete(bool inbInstant)
        {
            m_HeaderText.SetTextFromString(string.Empty);
            m_DescriptionText.SetTextFromString(string.Empty);
            m_ImageDisplay.Clear();
        }

        #endregion // Callbacks
    }
}