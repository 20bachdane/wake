using System;
using System.Collections;
using BeauPools;
using BeauRoutine;
using BeauRoutine.Extensions;
using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.Tags;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aqua {
    public class PopupPanel : BasePanel {
        static public readonly StringHash32 Option_Okay = "Okay";
        static public readonly StringHash32 Option_Submit = "Submit";
        static public readonly StringHash32 Option_Yes = "Yes";
        static public readonly StringHash32 Option_No = "";
        static public readonly StringHash32 Option_Cancel = "";

        static public readonly NamedOption[] DefaultOkay = new NamedOption[] { new NamedOption(Option_Okay, "ui.popup.okay") };
        static public readonly NamedOption[] DefaultYesNo = new NamedOption[] { new NamedOption(Option_Yes, "ui.popup.yes"), new NamedOption(Option_No, "ui.popup.no") };
        static public readonly NamedOption[] DefaultAddToBestiary = new NamedOption[] { new NamedOption(Option_Okay, "ui.popup.addToBestiaryButton") };
        static public readonly NamedOption[] DefaultDismiss = new NamedOption[] { new NamedOption(Option_Okay, "ui.popup.dismiss") };

        [Serializable]
        private struct ButtonConfig {
            public Transform Root;
            public TMP_Text Text;
            public Button Button;
            public CursorInteractionHint Tooltip;

            [NonSerialized] public StringHash32 OptionId;
        }

        #region Inspector

        [Header("Canvas")]
        [SerializeField] private InputRaycasterLayer m_RaycastBlocker = null;

        [Header("Contents")]
        [SerializeField] private LayoutGroup m_Layout = null;
        [SerializeField] private LocText m_HeaderText = null;
        [SerializeField] private StreamedImageSetDisplay m_ImageDisplay = null;
        [SerializeField] private LocText m_ContentsText = null;
        [SerializeField] private FactPools m_FactPools = null;
        [SerializeField] private RectTransformPool m_KnownFactBadgePool = null;
        [SerializeField] private VerticalLayoutGroup m_VerticalFactLayout = null;
        [SerializeField] private GridLayoutGroup m_GridFactLayout = null;
        [SerializeField] private GameObject m_DividerGroup = null;
        [SerializeField] private ButtonConfig[] m_Buttons = null;
        [SerializeField] private Button m_CloseButton = null;
        [SerializeField] private float m_AutoCloseDelay = 0.01f;

        #endregion // Inspector

        [NonSerialized] private Routine m_DisplayRoutine;
        [NonSerialized] private Routine m_BoxAnim;

        [NonSerialized] private StringHash32 m_SelectedOption;
        [NonSerialized] private bool m_OptionWasSelected;
        [NonSerialized] private int m_OptionCount;

        [NonSerialized] private BFBase[] m_CachedFactsSet = new BFBase[1];
        [NonSerialized] private BFDiscoveredFlags[] m_CachedFlagsSet = new BFDiscoveredFlags[1];

        #region Initialization

        protected override void Awake() {
            base.Awake();

            for (int i = 0; i < m_Buttons.Length; ++i) {
                int cachedIdx = i;
                m_Buttons[i].Button.onClick.AddListener(() => OnButtonClicked(cachedIdx));
            }

            m_CloseButton.onClick.AddListener(OnCloseClicked);
        }

        #endregion // Initialization

        #region Display

        public Future<StringHash32> Display(string inHeader, string inText, StreamedImageSet inImage = default, PopupFlags inPopupFlags = default) {
            return Present(inHeader, inText, inImage, inPopupFlags, DefaultOkay);
        }

        public Future<StringHash32> DisplayWithClose(string inHeader, string inText, StreamedImageSet inImage = default, PopupFlags inPopupFlags = default) {
            return Present(inHeader, inText, inImage, inPopupFlags | PopupFlags.ShowCloseButton, null);
        }

        public Future<StringHash32> AskYesNo(string inHeader, string inText, StreamedImageSet inImage = default, PopupFlags inPopupFlags = default) {
            return Present(inHeader, inText, inImage, inPopupFlags, DefaultYesNo);
        }

        public Future<StringHash32> Present(string inHeader, string inText, StreamedImageSet inImage, PopupFlags inPopupFlags, params NamedOption[] inOptions) {
            Future<StringHash32> future = new Future<StringHash32>();
            m_DisplayRoutine.Replace(this, PresentMessageRoutine(future, inHeader, inText, inImage, inOptions, inPopupFlags));
            return future;
        }

        public Future<StringHash32> PresentFact(string inHeader, string inText, StreamedImageSet inImage, BFBase inFact, BFDiscoveredFlags inFlags, PopupFlags inPopupFlags = default, params NamedOption[] inOptions) {
            Future<StringHash32> future = new Future<StringHash32>();
            if (inOptions == null || inOptions.Length == 0) {
                inOptions = DefaultAddToBestiary;
            }
            m_DisplayRoutine.Replace(this, PresentFactRoutine(future, inHeader, inText, inImage, TempFacts(inFact, inFlags), inOptions, inPopupFlags)).Tick();
            return future;
        }

        public Future<StringHash32> PresentFacts(string inHeader, string inText, StreamedImageSet inImage, PopupFacts inFacts, PopupFlags inPopupFlags = default, params NamedOption[] inOptions) {
            Future<StringHash32> future = new Future<StringHash32>();
            if (inOptions == null || inOptions.Length == 0) {
                inOptions = DefaultAddToBestiary;
            }
            m_DisplayRoutine.Replace(this, PresentFactRoutine(future, inHeader, inText, inImage, inFacts, inOptions, inPopupFlags)).Tick();
            return future;
        }

        public Future<StringHash32> PresentFactDetails(BFDetails inDetails, BFBase inFact, BFDiscoveredFlags inFlags, PopupFlags inPopupFlags, params NamedOption[] inOptions) {
            Future<StringHash32> future = new Future<StringHash32>();
            m_DisplayRoutine.Replace(this, PresentFactRoutine(future, inDetails.Header, inDetails.Description, inDetails.Image, TempFacts(inFact, inFlags), inOptions, inPopupFlags)).Tick();
            return future;
        }

        private PopupFacts TempFacts(BFBase fact, BFDiscoveredFlags flags) {
            m_CachedFactsSet[0] = fact;
            m_CachedFlagsSet[0] = flags;
            return new PopupFacts() {
                Facts = m_CachedFactsSet,
                Flags = m_CachedFlagsSet
            };
        }

        private void Configure(string inHeader, string inText, StreamedImageSet inImage, ListSlice<NamedOption> inOptions, PopupFlags inPopupFlags) {
            if (!string.IsNullOrEmpty(inHeader)) {
                m_HeaderText.SetTextFromString(inHeader);
                m_HeaderText.gameObject.SetActive(true);
            } else {
                m_HeaderText.gameObject.SetActive(false);
                m_HeaderText.SetTextFromString(string.Empty);
            }

            if (!string.IsNullOrEmpty(inText)) {
                m_ContentsText.SetTextFromString(inText);
                m_ContentsText.gameObject.SetActive(true);
            } else {
                m_ContentsText.gameObject.SetActive(false);
                m_ContentsText.SetTextFromString(string.Empty);
            }

            if (inImage.IsEmpty) {
                m_ImageDisplay.gameObject.SetActive(false);
            } else {
                m_ImageDisplay.gameObject.SetActive(true);
                if ((inPopupFlags & PopupFlags.TallImage) != 0) {
                    m_ImageDisplay.Layout.preferredHeight = 260;
                } else {
                    m_ImageDisplay.Layout.preferredHeight = 160;
                }
                m_ImageDisplay.Display(inImage);
            }

            m_OptionCount = inOptions.Length;
            if (m_OptionCount == 0) {
                inPopupFlags |= PopupFlags.ShowCloseButton;
            }
            for (int i = 0; i < m_Buttons.Length; ++i) {
                ref ButtonConfig config = ref m_Buttons[i];

                if (i < m_OptionCount) {
                    NamedOption option = inOptions[i];
                    config.Text.SetText(Loc.Find(option.TextId));
                    config.OptionId = option.Id;
                    config.Root.gameObject.SetActive(true);
                    config.Tooltip.TooltipOverride = config.Text.text;
                    config.Button.interactable = option.Enabled;
                } else {
                    config.OptionId = null;
                    config.Root.gameObject.SetActive(false);
                    config.Tooltip.TooltipOverride = null;
                }
            }

            m_DividerGroup.SetActive(m_OptionCount > 0);
            m_CloseButton.gameObject.SetActive((inPopupFlags & PopupFlags.ShowCloseButton) != 0);
        }

        private void ConfigureFacts(PopupFacts inFacts) {
            var factList = inFacts.Facts;

            m_FactPools.FreeAll();
            m_KnownFactBadgePool.Reset();

            Vector2 gridCellSize = m_GridFactLayout.cellSize;
            gridCellSize.y = 0;

            if (factList.IsEmpty) {
                m_VerticalFactLayout.gameObject.SetActive(false);
                m_GridFactLayout.gameObject.SetActive(false);
                return;
            }

            bool bUsedGrid = false, bUsedVertical = false, bCurrentIsGrid = false;

            Transform target;
            MonoBehaviour factView;
            RectTransform factTransform;

            for (int i = 0; i < factList.Length; i++) {
                BFBase fact = factList[i];
                BFDiscoveredFlags flags = i >= inFacts.Flags.Length ? BFType.DefaultDiscoveredFlags(factList[i]) : inFacts.Flags[i];
                if (factList.Length > 1) {
                    switch (fact.Type) {
                        case BFTypeId.WaterProperty:
                        case BFTypeId.WaterPropertyHistory:
                        case BFTypeId.Population:
                        case BFTypeId.PopulationHistory:
                            target = m_GridFactLayout.transform;
                            bUsedGrid = true;
                            bCurrentIsGrid = true;
                            break;

                        default: {
                                target = m_VerticalFactLayout.transform;
                                bUsedVertical = true;
                                bCurrentIsGrid = false;
                                break;
                            }
                    }
                } else {
                    target = m_VerticalFactLayout.transform;
                    bUsedVertical = true;
                }

                factView = m_FactPools.Alloc(factList[i], null, flags, target);
                factTransform = (RectTransform)factView.transform;
                if (bCurrentIsGrid) {
                    gridCellSize.y = Mathf.Max(gridCellSize.y, factTransform.sizeDelta.y);
                }

                if (inFacts.ShowNew != null) {
                    bool isNew = inFacts.ShowNew(factList[i]);
                    if (!isNew) {
                        m_KnownFactBadgePool.Alloc(factTransform);
                    }
                }
            }

            if (bUsedVertical) {
                m_VerticalFactLayout.gameObject.SetActive(true);
                m_VerticalFactLayout.ForceRebuild();
            } else {
                m_VerticalFactLayout.gameObject.SetActive(false);
            }

            if (bUsedGrid) {
                m_GridFactLayout.cellSize = gridCellSize;
                m_GridFactLayout.gameObject.SetActive(true);
                m_GridFactLayout.ForceRebuild();
            } else {
                m_GridFactLayout.gameObject.SetActive(false);
            }
        }

        private void ShowOrBounce() {
            if (IsShowing()) {
                m_BoxAnim.Replace(this, BounceAnim());
            } else {
                Show();
            }
        }

        private IEnumerator PresentMessageRoutine(Future<StringHash32> ioFuture, string inHeader, string inText, StreamedImageSet inImage, ListSlice<NamedOption> inOptions, PopupFlags inPopupFlags) {
            using (ioFuture) {
                Configure(inHeader, inText, inImage, inOptions, inPopupFlags);
                ConfigureFacts(default);

                Services.Events.QueueForDispatch(GameEvents.PopupOpened);

                ShowOrBounce();

                SetInputState(true);
                AttemptTTS(inHeader, inText, inImage);

                m_SelectedOption = StringHash32.Null;
                m_OptionWasSelected = false;
                while (!m_OptionWasSelected) {
                    if (inOptions.Length <= 1 && m_RaycastBlocker.Device.KeyPressed(KeyCode.Space)) {
                        m_OptionWasSelected = true;
                        if (inOptions.Length == 1)
                            m_SelectedOption = inOptions[0].Id;
                        break;
                    }

                    yield return null;
                }

                Services.TTS.Cancel();
                SetInputState(false);

                ioFuture.Complete(m_SelectedOption);
                m_SelectedOption = StringHash32.Null;

                yield return null;

                if (m_AutoCloseDelay > 0)
                    yield return m_AutoCloseDelay;

                Hide();
            }
        }

        private IEnumerator PresentFactRoutine(Future<StringHash32> ioFuture, string inHeader, string inText, StreamedImageSet inImage, PopupFacts inFacts, ListSlice<NamedOption> inOptions, PopupFlags inPopupFlags) {
            using (ioFuture) {
                Configure(inHeader, inText, inImage, inOptions, inPopupFlags);
                ConfigureFacts(inFacts);

                Services.Events.QueueForDispatch(GameEvents.PopupOpened);

                ShowOrBounce();

                SetInputState(true);
                AttemptTTS(inHeader, inText, inImage);

                m_SelectedOption = StringHash32.Null;
                m_OptionWasSelected = false;
                while (!m_OptionWasSelected) {
                    if (inOptions.Length <= 1 && m_RaycastBlocker.Device.KeyPressed(KeyCode.Space)) {
                        m_OptionWasSelected = true;
                        if (inOptions.Length == 1)
                            m_SelectedOption = inOptions[0].Id;
                        break;
                    }

                    yield return null;
                }

                Services.TTS.Cancel();
                SetInputState(false);

                ioFuture.Complete(m_SelectedOption);
                m_SelectedOption = StringHash32.Null;

                yield return null;

                if (m_AutoCloseDelay > 0)
                    yield return m_AutoCloseDelay;

                Hide();
            }
        }

        private void AttemptTTS(string inHeader, string inText, StreamedImageSet inImage) {
            if (Accessibility.TTSFull) {
                using (PooledStringBuilder psb = PooledStringBuilder.Create()) {
                    if (!string.IsNullOrEmpty(inHeader)) {
                        psb.Builder.Append(inHeader).Append("\n\n");
                    }
                    if (!inImage.IsEmpty && !string.IsNullOrEmpty(inImage.Tooltip)) {
                        psb.Builder.Append(inImage.Tooltip).Append("\n\n");
                    }
                    if (!string.IsNullOrEmpty(inText)) {
                        psb.Builder.Append(inText);
                    }
                    Services.TTS.Text(psb.Builder.Flush());
                }
            }
        }

        public bool IsDisplaying() {
            return m_DisplayRoutine;
        }

        #endregion // Display

        #region Animation

        private IEnumerator BounceAnim() {
            m_RootGroup.alpha = 0.5f;
            m_RootTransform.SetScale(0.5f);
            yield return Routine.Combine(
                m_RootTransform.ScaleTo(1, 0.2f).ForceOnCancel().Ease(Curve.BackOut),
                m_RootGroup.FadeTo(1, 0.2f)
            );
        }

        protected override IEnumerator TransitionToShow() {
            float durationMultiplier = 1;
            if (m_RootGroup.alpha > 0 && m_RootGroup.alpha < 1)
                durationMultiplier = 0.5f;

            m_RootGroup.alpha = durationMultiplier < 1 ? 0.5f : 0;
            m_RootTransform.SetScale(durationMultiplier < 1 ? 0.75f : 0.5f);
            m_RootTransform.gameObject.SetActive(true);

            m_Layout.ForceRebuild();

            yield return Routine.Combine(
                m_RootGroup.FadeTo(1, 0.2f * durationMultiplier),
                m_RootTransform.ScaleTo(1f, 0.2f * durationMultiplier).Ease(Curve.BackOut)
            );
        }

        protected override IEnumerator TransitionToHide() {
            yield return Routine.Combine(
                m_RootGroup.FadeTo(0, 0.15f),
                m_RootTransform.ScaleTo(0.5f, 0.15f).Ease(Curve.CubeIn)
            );

            m_RootTransform.gameObject.SetActive(false);
        }

        #endregion // Animation

        #region Callbacks

        private void OnButtonClicked(int inIndex) {
            m_SelectedOption = m_Buttons[inIndex].OptionId;
            m_OptionWasSelected = true;
        }

        private void OnCloseClicked() {
            m_SelectedOption = StringHash32.Null;
            m_OptionWasSelected = true;
        }

        #endregion // Callbacks

        #region BasePanel

        protected override void OnShow(bool inbInstant) {
            SetInputState(false);
            m_RaycastBlocker.Override = null;

            if (!WasShowing()) {
                Services.Input?.PushPriority(m_RaycastBlocker);
                Services.Input?.PushFlags(m_RaycastBlocker);
            }
        }

        protected override void OnHide(bool inbInstant) {
            SetInputState(false);

            m_BoxAnim.Stop();
            m_DisplayRoutine.Stop();

            m_RaycastBlocker.Override = false;

            if (WasShowing()) {
                Services.Events?.QueueForDispatch(GameEvents.PopupClosed);
            }

            if (WasShowing()) {
                Services.Input?.PopFlags(m_RaycastBlocker);
                Services.Input?.PopPriority(m_RaycastBlocker);
            }
        }

        protected override void OnHideComplete(bool inbInstant) {
            m_KnownFactBadgePool.Reset();
            m_FactPools.FreeAll();
            m_ImageDisplay.Clear();
            m_RootGroup.alpha = 0;

            m_CachedFactsSet[0] = default;
            m_CachedFlagsSet[0] = default;

            base.OnHideComplete(inbInstant);
        }

        #endregion // BasePanel
    }

    public struct PopupFacts {
        public ListSlice<BFBase> Facts;
        public ListSlice<BFDiscoveredFlags> Flags;
        public Predicate<BFBase> ShowNew;

        public PopupFacts(ListSlice<BFBase> facts) {
            Facts = facts;
            Flags = default;
            ShowNew = null;
        }

        public PopupFacts(ListSlice<BFBase> facts, ListSlice<BFDiscoveredFlags> flags) {
            Facts = facts;
            Flags = flags;
            ShowNew = null;
        }
    }

    [Flags]
    public enum PopupFlags {
        ShowCloseButton = 0x01,
        TallImage = 0x02
    }
}