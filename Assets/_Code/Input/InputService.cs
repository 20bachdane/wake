using Aqua.Debugging;
using BeauData;
using BeauUtil;
using BeauUtil.Debugger;
using NativeUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Aqua
{
    public class InputService : ServiceBehaviour
    {
        public const int DefaultPriority = int.MinValue;
        public const float DefaultDoubleClickBuffer = 0.8f;

        #region Types

        private struct PriorityRecord
        {
            public readonly int Priority;
            public readonly object Context;

            public PriorityRecord(int inPriority, object inContext)
            {
                Priority = inPriority;
                Context = inContext;
            }
        }

        private struct FlagsRecord
        {
            public readonly InputLayerFlags Flags;
            public readonly object Context;

            public FlagsRecord(InputLayerFlags inFlags, object inContext)
            {
                Flags = inFlags;
                Context = inContext;
            }
        }

        #endregion // Types

        #region Inspector

        [SerializeField, Required] private ExposedPointerInputModule m_InputModule = null;

        #endregion // Inspector

        [NonSerialized] private PointerInputMode m_PointerMode = PointerInputMode.Mouse;
        [NonSerialized] private readonly BufferedCollection<IInputLayer> m_AllInputLayers = new BufferedCollection<IInputLayer>(32);
        [NonSerialized] private int m_CurrentPriority = DefaultPriority;
        [NonSerialized] private InputLayerFlags m_CurrentFlags = InputLayerFlags.Default;
        [NonSerialized] private int m_PauseAllCounter = 0;
        [NonSerialized] private int m_ForceClick = 0;
        [NonSerialized] private RingBuffer<long> m_LastClickTimes = new RingBuffer<long>(2, RingBufferMode.Overwrite);

        [NonSerialized] private PointerEventData m_NativeEventData;

        [NonSerialized] private readonly List<PriorityRecord> m_PriorityStack = new List<PriorityRecord>(8);
        [NonSerialized] private readonly List<FlagsRecord> m_FlagsStack = new List<FlagsRecord>(8);

        public event Action<KeyCode> OnKeyPressed;

        #region Input Layers

        public bool RegisterInput(IInputLayer inInputLayer)
        {
            if (!m_AllInputLayers.Contains(inInputLayer))
            {
                m_AllInputLayers.Add(inInputLayer);
                inInputLayer.UpdateSystem(m_CurrentPriority, WorkingFlags(), true);
                return true;
            }

            return false;
        }

        public bool DeregisterInput(IInputLayer inInputLayer)
        {
            return m_AllInputLayers.Remove(inInputLayer);
        }

        private void BroadcastSystemUpdate()
        {
            foreach(var layer in m_AllInputLayers)
            {
                layer.UpdateSystem(m_CurrentPriority, WorkingFlags(), false);
            }
        }

        #endregion // Input Layers

        #region Priority

        public void PushPriority(IInputLayer inInputLayer)
        {
            PushPriority(inInputLayer.Priority, inInputLayer);
        }

        public void PushPriority(int inPriority, object inContext = null)
        {
            DebugService.Log(LogMask.Input, "[InputService] Pushed priority {0} with context '{1}'", inPriority, inContext);

            m_PriorityStack.Add(new PriorityRecord(inPriority, inContext));
            m_CurrentPriority = Math.Max(inPriority, m_CurrentPriority);

            BroadcastSystemUpdate();
        }

        public void PopPriority(object inContext = null)
        {
            if (m_PriorityStack.Count > 0)
            {
                for(int i = m_PriorityStack.Count - 1; i >= 0; --i)
                {
                    if (m_PriorityStack[i].Context == inContext)
                    {
                        m_PriorityStack.RemoveAt(i);
                        int end = m_PriorityStack.Count - 1;
                        m_CurrentPriority = end >= 0 ? m_PriorityStack[end].Priority : DefaultPriority;
                        DebugService.Log(LogMask.Input, "[InputService] Popped priority with context '{0}', new priority is {1} with context '{2}'", inContext, m_CurrentPriority, end < 0 ? "null" : m_PriorityStack[end].Context);

                        BroadcastSystemUpdate();
                        return;
                    }
                }
            }
            else
            {
                Log.Error("[InputService] Attempting to pop priority with nothing on stack.");
                m_CurrentPriority = DefaultPriority;
            }
        }

        #endregion // Priority

        #region Flags

        public void PushFlags(IInputLayer inLayer)
        {
            PushFlags(inLayer.Flags, inLayer);
        }

        public void PushFlags(InputLayerFlags inFlags, object inContext = null)
        {
            DebugService.Log(LogMask.Input, "[InputService] Pushed flags {0} with context '{1}'", inFlags, inContext);

            m_FlagsStack.Add(new FlagsRecord(inFlags, inContext));
            m_CurrentFlags = inFlags;

            BroadcastSystemUpdate();
        }

        public void PopFlags(object inContext = null)
        {
            if (m_FlagsStack.Count > 0)
            {
                for(int i = m_FlagsStack.Count - 1; i >= 0; --i)
                {
                    if (m_FlagsStack[i].Context == inContext)
                    {
                        m_FlagsStack.RemoveAt(i);
                        if (i == m_FlagsStack.Count)
                        {
                            m_CurrentFlags = i > 0 ? m_FlagsStack[i - 1].Flags : InputLayerFlags.Default;
                            DebugService.Log(LogMask.Input, "[InputService] Popped flags with context '{0}', new flags are {1} with context '{2}'", inContext, m_CurrentFlags, i == 0 ? "null" : m_FlagsStack[i - 1].Context);
                        }
                        else
                        {
                            DebugService.Log(LogMask.Input, "[InputService] Popped flags with context '{0}'", inContext);
                        }
                        BroadcastSystemUpdate();
                        return;
                    }
                }
            }
            else
            {
                Log.Error("[InputService] Attempting to pop flags with nothing on stack.");
                m_CurrentFlags = InputLayerFlags.Default;
            }
        }

        private InputLayerFlags WorkingFlags()
        {
            return m_PauseAllCounter > 0 ? 0 : m_CurrentFlags;
        }

        #endregion // Flags

        #region Pointer

        public PointerInputMode PointerMode() { return m_PointerMode; }

        public bool IsPointerOverUI()
        {
            return m_InputModule.IsPointerOverCanvas();
        }

        public bool IsEditingText()
        {
            return m_InputModule.IsEditingText();
        }

        public Vector2 PointerOffsetFromCenter()
        {
            Vector2 pos = Input.mousePosition;
            pos.x = (pos.x / Screen.width) - 0.5f;
            pos.y = (pos.y / Screen.height) - 0.5f;
            return pos;
        }

        public bool ExecuteClick(GameObject inRoot)
        {
            RectTransform rectTransform = inRoot.transform as RectTransform;
            if (rectTransform && !rectTransform.IsPointerInteractable())
                return false;
            
            ++m_ForceClick;
            bool bSuccess = ExecuteEvents.Execute(inRoot, m_InputModule.GetPointerEventData(), ExecuteEvents.pointerClickHandler);
            --m_ForceClick;
            return bSuccess;
        }

        public bool ForceClick(GameObject inRoot)
        {
            ++m_ForceClick;
            bool bSuccess = ExecuteEvents.Execute(inRoot, m_InputModule.GetPointerEventData(), ExecuteEvents.pointerClickHandler);
            --m_ForceClick;
            return bSuccess;
        }

        public bool IsForcingInput()
        {
            return m_ForceClick > 0;
        }

        public bool DoubleClick(float buffer = DefaultDoubleClickBuffer)
        {
            if (m_LastClickTimes.Count < 2) {
                return false;
            }

            long timeSince = m_LastClickTimes[0] - m_LastClickTimes[1];
            long bufferTicks = (long) (buffer * TimeSpan.TicksPerSecond);
            return Input.GetMouseButtonDown(0) && timeSince <= bufferTicks;
        }

        private void OnInputModeChanged(PointerInputMode inMode)
        {
            m_PointerMode = inMode;
            DebugService.Log(LogMask.Input, "[InputService] Pointer mode switched to {0}", inMode);
        }

        #endregion // Pointer

        #region Pause All

        public void PauseAll()
        {
            DebugService.Log(LogMask.Input, "[InputService] Pausing all input (depth {0})", m_PauseAllCounter);

            if (++m_PauseAllCounter == 1)
            {
                BroadcastSystemUpdate();
            }
        }

        public void ResumeAll()
        {
            if (m_PauseAllCounter == 0)
            {
                Log.Error("[InputService] Pause/Resume calls are mismatched");
                return;
            }

            DebugService.Log(LogMask.Input, "[InputService] Resuming all input (depth {0})", m_PauseAllCounter - 1);

            if (--m_PauseAllCounter == 0)
            {
                BroadcastSystemUpdate();
            }
        }

        #endregion // Pause All

        #region Unity Events

        private void LateUpdate()
        {
            if (Input.GetMouseButtonDown(0)) {
                m_LastClickTimes.PushFront(Stopwatch.GetTimestamp());
            }

            m_AllInputLayers.ForEach(UpdateDevice);
            DeviceInput.ClearBlock();
        }

        static private readonly Action<IInputLayer> UpdateDevice = (l) => l.Device.Update();

        private void OnNativeMouseDown(float x, float y)
        {
            m_NativeEventData.button = PointerEventData.InputButton.Left;
            m_InputModule.FindNativeClick(x, y, m_NativeEventData);
            GameObject press = ExecuteEvents.ExecuteHierarchy(m_NativeEventData.pointerCurrentRaycast.gameObject, m_NativeEventData, NativeMouseDownHandler);
            m_NativeEventData.pointerPress = press;
        }

        private void OnNativeMouseUp(float x, float y)
        {
            m_InputModule.FindNativeClick(x, y, m_NativeEventData);
            GameObject pressed = m_NativeEventData.pointerPress;
            if (pressed != null) {
                ExecuteEvents.Execute(pressed, m_NativeEventData, NativeMouseUpHandler);
            }
            GameObject released = ExecuteEvents.GetEventHandler<INativePointerClickHandler>(m_NativeEventData.pointerCurrentRaycast.gameObject);
            if (pressed != null && released == pressed) {
                ExecuteEvents.Execute(released, m_NativeEventData, NativeMouseClickHandler);
            }

            m_NativeEventData.Reset();
        }

        static private readonly ExecuteEvents.EventFunction<INativePointerDownHandler> NativeMouseDownHandler = (INativePointerDownHandler handler, BaseEventData evtData) => {
            handler.OnNativePointerDown(ExecuteEvents.ValidateEventData<PointerEventData>(evtData));
        };

        static private readonly ExecuteEvents.EventFunction<INativePointerDownHandler> NativeMouseUpHandler = (INativePointerDownHandler handler, BaseEventData evtData) => {
            handler.OnNativePointerDown(ExecuteEvents.ValidateEventData<PointerEventData>(evtData));
        };

        static private readonly ExecuteEvents.EventFunction<INativePointerClickHandler> NativeMouseClickHandler = (INativePointerClickHandler handler, BaseEventData evtData) => {
            handler.OnNativePointerClick(ExecuteEvents.ValidateEventData<PointerEventData>(evtData));
        };

        private void OnGUI() {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) {
                return;
            }

            if (m_PauseAllCounter > 0 || m_InputModule.IsEditingText()) {
                return;
            }

            OnKeyPressed?.Invoke(e.keyCode);
        }

        #endregion // Unity Events

        #region Service

        protected override void Initialize()
        {
            base.Initialize();

            Input.multiTouchEnabled = false;
            m_InputModule.OnModeChanged += OnInputModeChanged;

            NativeInput.Initialize();
            NativeInput.OnMouseDown += OnNativeMouseDown;
            NativeInput.OnMouseUp += OnNativeMouseUp;

            m_NativeEventData = new PointerEventData(EventSystem.current);

            useGUILayout = false;
        }

        protected override void OnDestroy()
        {
            m_InputModule.OnModeChanged -= OnInputModeChanged;

            NativeInput.OnMouseDown -= OnNativeMouseDown;
            NativeInput.OnMouseUp -= OnNativeMouseUp;
            NativeInput.Shutdown();

            base.OnDestroy();
        }

        #endregion // Service

        #region Cheats

        

        #endregion // Cheats
    }
}