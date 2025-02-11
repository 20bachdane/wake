using System;
using System.Collections;
using Aqua.Scripting;
using BeauRoutine;
using BeauRoutine.Extensions;
using BeauUtil;
using BeauUtil.Debugger;
using Leaf.Runtime;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using Aqua.JobBoard;

namespace Aqua.StationInterior
{
    public sealed class InteriorUICtrl : SharedManager
    {
        #region Inspector

        [Header("Back Button")]
        [SerializeField] private CanvasGroup m_BackButtonGroup = null;
        [SerializeField] private Button m_BackButton = null;

        [Header("Currency UI")]
        [SerializeField] private RectTransform m_CurrencyUI = null;
        [SerializeField] private float m_CurrencyOffscreenPos = 64;
        [SerializeField] private float m_CurrencyOnscreenPos = -8;
        [SerializeField] private float m_CurrencyJobOffsetX = 0;
        [SerializeField] private float m_CurrencyShopOffsetX = 0;

        [Header("Animations")]
        [SerializeField] private TweenSettings m_SharedOnAnim = new TweenSettings(0.2f);
        [SerializeField] private TweenSettings m_SharedOffAnim = new TweenSettings(0.2f);

        [Header("Panels")]
        [SerializeField] private JobBoard.JobBoard m_JobBoard = null;
        [SerializeField] private BasePanel m_ShopPanel = null;

        [Header("Leave")]
        [SerializeField] private RectTransform m_LeavePanel = null;
        [SerializeField] private CanvasGroup m_LeavePanelGroup = null;
        [SerializeField] private ScriptInspectable m_WarpButton = null;

        #endregion // Inspector

        [NonSerialized] private BaseInputLayer m_BaseInput;
        [NonSerialized] private BasePanel m_CurrentPanel;
        [NonSerialized] private Routine m_SharedAnim;
        [NonSerialized] private Routine m_LeaveAnim;

        protected override void Awake() {
            base.Awake();

            Script.OnSceneLoad(OnSceneLoad, 5);

            Services.Events.Register<StringHash32>(GameEvents.ViewLeaving, OnViewLeaving, this)
                .Register<StringHash32>(GameEvents.ViewArrived, OnViewArrived, this);
        }

        private void Start() {
            m_BackButtonGroup.Hide();
            m_CurrencyUI.gameObject.SetActive(false);
            m_CurrencyUI.SetAnchorPos(m_CurrencyOffscreenPos, Axis.Y);

            m_JobBoard.OnHideEvent.AddListener((_) => OnPanelHide(m_JobBoard));
            m_ShopPanel.OnHideEvent.AddListener((_) => OnPanelHide(m_ShopPanel));

            m_BackButton.onClick.AddListener(OnBackClicked);

            m_BaseInput = BaseInputLayer.Find(this);

            m_LeavePanelGroup.Hide();
            m_LeavePanel.SetAnchorPos(-64, Axis.Y);
        }

        protected override void OnDestroy() {
            Services.Events?.DeregisterAll(this);
            base.OnDestroy();
        }

        #region Handlers

        private void OnSceneLoad() {
            StringHash32 stationScene = Save.Map.CurrentStationId();
            StringHash32 entrance = Assets.Map(MapDB.LookupCurrentMap()).Parent().Id();
            bool hasSeenSurface = Save.Map.HasVisitedLocation(stationScene);
            m_WarpButton.Config.TargetId = stationScene;
            m_WarpButton.Config.TargetEntranceId = entrance;
            m_WarpButton.Locked = !hasSeenSurface;
            m_WarpButton.RefreshState();
        }

        private void OnBackClicked() {
            m_CurrentPanel?.Hide();
        }

        private void OnPanelHide(BasePanel panel) {
            if (m_CurrentPanel != panel) {
                return;
            }

            m_CurrentPanel = null;
            m_BaseInput.PopPriority();
            m_SharedAnim.Replace(this, AnimateSharedOff());
        }

        private void SetPanel(BasePanel panel, float currencyOffsetX) {
            if (m_CurrentPanel == panel) {
                return;
            }

            var old = m_CurrentPanel;
            if (old != null) {
                m_CurrentPanel = null;
                old.Hide();
            }

            if (panel != null) {
                m_CurrentPanel = panel;
                m_BaseInput.PushPriority();
                m_CurrentPanel.Show();
                m_SharedAnim.Replace(this, AnimateSharedOn(currencyOffsetX));
                Services.Events.Dispatch(GameEvents.HotbarHide);
            } else {
                m_SharedAnim.Replace(this, AnimateSharedOff());
            }
        }

        private void OnViewLeaving(StringHash32 viewId) {
            if (viewId != "Station") {
                return;
            }

            m_LeaveAnim.Replace(this, AnimateLeaveOff());
        }

        private void OnViewArrived(StringHash32 viewId) {
            if (viewId != "Station") {
                return;
            }

            m_LeaveAnim.Replace(this, AnimateLeaveOn());
        }

        #endregion // Handlers

        #region Animations

        private IEnumerator AnimateSharedOn(float offsetX) {
            IAsyncLoadPanel load = m_CurrentPanel as IAsyncLoadPanel;
            if (load != null) {
                while (load.IsLoading()) {
                    yield return null;
                }
            }
            
            m_CurrencyUI.gameObject.SetActive(true);
            m_CurrencyUI.SetAnchorPos(offsetX, Axis.X);
            yield return Routine.Combine(
                m_BackButtonGroup.Show(m_SharedOnAnim.Time),
                m_CurrencyUI.AnchorPosTo(m_CurrencyOnscreenPos, m_SharedOnAnim, Axis.Y)
            );
        }

        private IEnumerator AnimateSharedOff() {
            yield return 0.1f;
            Services.Events.Dispatch(GameEvents.HotbarShow);
            yield return Routine.Combine(
                m_BackButtonGroup.Hide(m_SharedOffAnim.Time),
                m_CurrencyUI.AnchorPosTo(m_CurrencyOffscreenPos, m_SharedOffAnim, Axis.Y)
            );
            m_CurrencyUI.gameObject.SetActive(false);
        }

        private IEnumerator AnimateLeaveOn() {
            yield return Routine.Combine(
                m_LeavePanel.AnchorPosTo(0, 0.3f, Axis.Y).Ease(Curve.CubeOut),
                m_LeavePanelGroup.Show(0.25f)
            );
        }

        private IEnumerator AnimateLeaveOff() {
            yield return Routine.Combine(
                m_LeavePanel.AnchorPosTo(-64, 0.3f, Axis.Y).Ease(Curve.CubeIn),
                m_LeavePanelGroup.Hide(0.25f)
            );
        }

        #endregion // Animations
    
        #region Leaf

        [LeafMember("InteriorOpenJobBoard"), Preserve]
        static private void LeafOpenJobBoard(ScriptObject inCameraPose = null) {
            var ctrl = Services.State.FindManager<InteriorUICtrl>();
            Assert.NotNull(ctrl);
            ctrl.m_JobBoard.OverrideZoomPose(inCameraPose);
            ctrl.SetPanel(ctrl.m_JobBoard, ctrl.m_CurrencyJobOffsetX);
        }

        [LeafMember("InteriorOpenShopBoard"), Preserve]
        static private void LeafOpenShopBoard() {
            var ctrl = Services.State.FindManager<InteriorUICtrl>();
            Assert.NotNull(ctrl);
            ctrl.SetPanel(ctrl.m_ShopPanel, ctrl.m_CurrencyShopOffsetX);
        }
        #endregion // Leaf
    }
}