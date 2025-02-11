﻿using System;
using System.Collections;
using System.Collections.Generic;
using Aqua;
using Aqua.Cameras;
using BeauData;
using BeauPools;
using BeauRoutine;
using BeauRoutine.Extensions;
using BeauUtil;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aqua.JobBoard {
    public class JobBoard : BasePanel, IAsyncLoadPanel {
        [Serializable] private class HeaderPool : SerializablePool<ListHeader> { }
        [Serializable] private class ButtonPool : SerializablePool<JobButton> { }

        #region Inspector

        [Header("Pools")]
        [SerializeField] private HeaderPool m_HeaderPool = null;
        [SerializeField] private ButtonPool m_ButtonPool = null;

        [Header("Groups")]
        [SerializeField] private ToggleGroup m_JobToggle = null;
        [SerializeField] private LayoutGroup m_JobListLayout = null;
        [SerializeField] private ScrollRect m_JobListScroll = null;
        [SerializeField] private JobInfo m_Info = null;

        [Inline(InlineAttribute.DisplayType.HeaderLabel)]
        [SerializeField] private JobButton.ButtonAppearanceConfig m_ButtonAppearance = default;

        [Header("Not Selected")]
        [SerializeField] private LocText m_NotSelectedLabel = null;
        [SerializeField] private TextId m_NotSelectedHasJobsText = null;
        [SerializeField] private TextId m_NotSelectedLockedJobsText = null;
        [SerializeField] private TextId m_NotSelectedNoJobsText = null;

        [Header("Animation")]
        [SerializeField] private TweenSettings m_TweenOnAnim = new TweenSettings(0.2f);
        [SerializeField] private TweenSettings m_TweenOffAnim = new TweenSettings(0.2f);
        [SerializeField] private float m_OffscreenPos = 0;
        [SerializeField] private float m_OnscreenPos = 0;
        [SerializeField] private FlashAnim m_AcceptFlash = null;

        [Header("Camera")]
        [SerializeField, InstanceOnly] private CameraPose m_DefaultPose = null;
        [SerializeField, InstanceOnly] private CameraPose m_ZoomedPose = null;

        #endregion

        [NonSerialized] private JobButton m_SelectedJobButton = null;
        [NonSerialized] private ListHeader[] m_GroupHeaderMap = new ListHeader[5];
        [NonSerialized] private Dictionary<StringHash32, JobButton> m_JobButtonMap = new Dictionary<StringHash32, JobButton>(32);
        [NonSerialized] private bool m_HasLockedJobs = false;
        [NonSerialized] private bool m_HasAvailableJobs = false;
        [NonSerialized] private CameraPose m_OverrideZoomPose = null;
        [NonSerialized] private Routine m_AsyncLoadRoutine;

        #region Unity Events

        protected override void Awake() {
            base.Awake();

            Services.Events.Register(GameEvents.JobSwitched, RefreshButtons, this)
                .Register(GameEvents.JobCompleted, RefreshButtons, this);

            m_Info.OnActionClicked.AddListener(OnJobAction);
        }

        private void OnDestroy() {
            Services.Events?.DeregisterAll(this);
        }

        #endregion // Unity Events

        public void OverrideZoomPose(ScriptObject obj) {
            if (obj != null) {
                m_OverrideZoomPose = obj.GetComponent<CameraPose>();
            } else {
                m_OverrideZoomPose = null;
            }
        }

        #region Handlers

        private void OnButtonSelected(JobButton inJobButton) {
            m_SelectedJobButton = inJobButton;
            m_Info.Populate(inJobButton.Job, inJobButton.Status);
            Services.Audio.PostEvent("Menu.Fill");
            m_JobToggle.allowSwitchOff = false;
        }

        private void OnJobAction() {
            var profileJobData = Save.Jobs;
            switch (m_SelectedJobButton.Group) {
                case JobProgressCategory.Available:
                case JobProgressCategory.InProgress: {
                        if (m_SelectedJobButton.Group == JobProgressCategory.Available) {
                            Services.Audio.PostEvent("JobBoard.Accept");
                        } else {
                            Services.Audio.PostEvent("JobBoard.Switch");
                        }
                        m_AcceptFlash.Ping();
                        profileJobData.SetCurrentJob(m_SelectedJobButton.Job.Id());
                        Routine.Start(this, WaitToExit()).ExecuteWhileDisabled().Tick();
                        break;
                    }
            }
        }

        private IEnumerator WaitToExit() {
            using (Script.Letterbox()) {
                yield return 0.5f;
                while (Script.ShouldBlockIgnoreLetterbox()) {
                    yield return null;
                }
                yield return Hide();
            }
        }

        #endregion // Handlers

        #region Job Buttons

        public bool IsLoading() {
            return m_AsyncLoadRoutine;
        }

        private IEnumerator LoadButtons() {
            m_JobListLayout.enabled = false;
            m_JobListScroll.enabled = false;
            
            yield return Async.Schedule(AllocateButtons(), AsyncFlags.MainThreadOnly);
            UpdateButtonStatuses();
            yield return null;
            OrderButtons();

            m_JobListLayout.enabled = true;
            m_JobListScroll.enabled = true;
            yield return null;

            UpdateUnselectedLabel();
            DisplayPercentage();
            AnimateButtonsOn();
        }

        private IEnumerator AllocateButtons() {
            StringHash32 id;
            JobButton button;
            foreach (var job in JobUtils.VisibleJobs(JobUtils.JobQueryFlags.IncludeCompleted)) {
                id = job.JobId;
                if (!m_JobButtonMap.TryGetValue(id, out button)) {
                    button = m_ButtonPool.Alloc();
                    button.Initialize(m_JobToggle, OnButtonSelected);
                    button.Populate(job.Job, JobStatusFlags.Hidden);
                    m_JobButtonMap[id] = button;
                    yield return null;
                }
            }
        }

        private void RefreshButtons() {
            if (UpdateButtonStatuses()) {
                OrderButtons();
                UpdateUnselectedLabel();
                DisplayPercentage();

                if (m_SelectedJobButton != null)
                    m_Info.UpdateStatus(m_SelectedJobButton.Job, m_SelectedJobButton.Status);
            }
        }

        private bool UpdateButtonStatuses() {
            if (!IsShowing()) {
                return false;
            }

            bool bUpdated = false;

            var profileJobData = Save.Jobs;
            PlayerJob job;
            foreach (var button in m_ButtonPool.ActiveObjects) {
                job = JobUtils.GetJobStatus(button.Job, Save.Current, true);
                bUpdated |= button.UpdateStatus(job.Status, m_ButtonAppearance);
                button.gameObject.SetActive(ShouldShowButton(job));
            }

            return bUpdated;
        }

        private bool ShouldShowButton(PlayerJob job) {
            if ((job.Status & JobStatusFlags.InProgress) != 0 && (job.Status & JobStatusFlags.Active) == 0) {
                return job.Job.StationId() == Save.Map.CurrentStationId();
            }

            // return (job.Status & JobStatusFlags.Completed) == 0;
            return true;
        }

        private void OrderButtons() {
            JobButton active = null;

            m_HasLockedJobs = false;
            m_HasAvailableJobs = false;

            using (PooledList<JobButton> progress = PooledList<JobButton>.Create())
            using (PooledList<JobButton> available = PooledList<JobButton>.Create())
            using (PooledList<JobButton> locked = PooledList<JobButton>.Create())
            using (PooledList<JobButton> completed = PooledList<JobButton>.Create()) {
                foreach (var button in m_ButtonPool.ActiveObjects) {
                    if (!button.gameObject.activeSelf)
                        continue;

                    switch (button.Group) {
                        case JobProgressCategory.Active:
                            active = button;
                            m_HasAvailableJobs = true;
                            break;

                        case JobProgressCategory.InProgress:
                            progress.Add(button);
                            m_HasAvailableJobs = true;
                            break;

                        case JobProgressCategory.Available:
                            available.Add(button);
                            m_HasAvailableJobs = true;
                            break;

                        case JobProgressCategory.Locked:
                            locked.Add(button);
                            m_HasLockedJobs = false;
                            break;

                        case JobProgressCategory.Completed:
                            completed.Add(button);
                            break;
                    }
                }

                int siblingIndex = 0;
                OrderList(JobProgressCategory.Active, active, ref siblingIndex);
                OrderList(JobProgressCategory.InProgress, progress, ref siblingIndex);
                OrderList(JobProgressCategory.Available, available, ref siblingIndex);
                OrderList(JobProgressCategory.Locked, locked, ref siblingIndex);
                OrderList(JobProgressCategory.Completed, completed, ref siblingIndex);
            }
        }

        private void OrderList(JobProgressCategory inGroup, JobButton inButton, ref int ioSiblingIndex) {
            if (inButton == null) {
                FindHeader(inGroup, false)?.gameObject.SetActive(false);
                return;
            }

            ListHeader header = FindHeader(inGroup, true);
            header.gameObject.SetActive(true);
            header.Transform.SetSiblingIndex(ioSiblingIndex++);
            header.ListCount = 1;

            inButton.Transform.SetSiblingIndex(ioSiblingIndex++);
        }

        private void OrderList(JobProgressCategory inGroup, List<JobButton> inButtons, ref int ioSiblingIndex) {
            if (inButtons.Count <= 0) {
                FindHeader(inGroup, false)?.gameObject.SetActive(false);
                return;
            }

            ListHeader header = FindHeader(inGroup, true);
            header.gameObject.SetActive(true);
            header.Transform.SetSiblingIndex(ioSiblingIndex++);
            header.ListCount = inButtons.Count;

            foreach (var button in inButtons) {
                button.Transform.SetSiblingIndex(ioSiblingIndex++);
            }
        }

        private ListHeader FindHeader(JobProgressCategory inGroup, bool inbCreate) {
            ref ListHeader header = ref m_GroupHeaderMap[(int)inGroup];
            if (header == null && inbCreate) {
                header = m_HeaderPool.Alloc();
                switch (inGroup) {
                    case JobProgressCategory.Active:
                        header.SetText("ui.jobBoard.group.active");
                        break;

                    case JobProgressCategory.Completed:
                        // header.SetText("ui.jobBoard.group.completed");
                        break;

                    case JobProgressCategory.Available:
                        header.SetText("ui.jobBoard.group.available");
                        break;

                    case JobProgressCategory.InProgress:
                        header.SetText("ui.jobBoard.group.inProgress");
                        break;

                    case JobProgressCategory.Locked:
                        header.SetText("ui.jobBoard.group.locked");
                        break;
                }
            }

            return header;
        }

        private void UpdateUnselectedLabel() {
            if (m_HasAvailableJobs) {
                m_NotSelectedLabel.SetText(m_NotSelectedHasJobsText);
            } else if (m_HasLockedJobs) {
                m_NotSelectedLabel.SetText(m_NotSelectedLockedJobsText);
            } else {
                m_NotSelectedLabel.SetText(m_NotSelectedNoJobsText);
            }
        }

        private void AnimateButtonsOn() {
            float delay = m_TweenOnAnim.Time + 0.1f;
            RectTransform clipping = m_JobListScroll.viewport;
            foreach(RectTransform child in m_JobListLayout.transform) {
                AppearAnim anim = child.GetComponent<AppearAnim>();
                if (anim && CanvasExtensions.IsVisible(clipping, child)) {
                    delay += anim.Ping(delay) * 0.2f;
                }
            }
        }

        private void DisplayPercentage() {
            int total = Services.Assets.Jobs.JobsForStation(Save.Map.CurrentStationId()).Length;
            
            ListHeader header = FindHeader(JobProgressCategory.Completed, false);
            if (header && header.gameObject.activeSelf) {
                int percentage = (int) (100 * header.ListCount / (float) total);
                using(PooledStringBuilder psb = PooledStringBuilder.Create()) {
                    psb.Builder.AppendLoc("ui.jobBoard.group.completed").Append(" (").AppendNoAlloc(percentage).Append("%)");
                    header.SetText(psb.Builder);
                }
            }
        }

        #endregion // Job Buttons

        #region Camera

        private void MoveCameraToZoom() {
            CameraPose pose = m_OverrideZoomPose != null ? m_OverrideZoomPose : m_ZoomedPose;
            Services.Camera.MoveToPose(pose, 0.5f, Curve.Smooth, Cameras.CameraPoseProperties.All);
        }

        private void MoveCameraToDefault() {
            Services.Camera.MoveToPose(m_DefaultPose, 0.5f, Curve.Smooth, Cameras.CameraPoseProperties.All);
        }

        #endregion // Camera

        #region BasePanel

        protected override void OnShow(bool inbInstant) {
            base.OnShow(inbInstant);

            m_JobToggle.allowSwitchOff = true;
            m_AsyncLoadRoutine.Replace(this, LoadButtons());

            m_Info.Clear();
        }

        protected override void OnHideComplete(bool instant) {
            base.OnHideComplete(instant);

            m_ButtonPool.Reset();
            m_HeaderPool.Reset();
            m_JobButtonMap.Clear();
            m_JobToggle.SetAllTogglesOff(false);
            Array.Clear(m_GroupHeaderMap, 0, m_GroupHeaderMap.Length);
        }

        protected override void InstantTransitionToHide() {
            Root.SetAnchorPos(m_OffscreenPos, Axis.Y);
            CanvasGroup.Hide();
        }

        protected override void InstantTransitionToShow() {
            Root.SetAnchorPos(m_OnscreenPos, Axis.Y);
            CanvasGroup.Show();
        }

        protected override IEnumerator TransitionToHide() {
            yield return Routine.Combine(
                Root.AnchorPosTo(m_OffscreenPos, m_TweenOffAnim, Axis.Y),
                Routine.Delay(MoveCameraToDefault, m_TweenOffAnim.Time * 0.8f)
            );
            CanvasGroup.Hide();
        }

        protected override IEnumerator TransitionToShow() {
            CanvasGroup.Show();
            yield return m_AsyncLoadRoutine;
            MoveCameraToZoom();
            yield return Root.AnchorPosTo(m_OnscreenPos, m_TweenOnAnim, Axis.Y).DelayBy(0.2f);
        }

        #endregion // BasePanel
    }
}