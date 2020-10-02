using System;
using System.Collections;
using System.Collections.Generic;
using BeauData;
using BeauRoutine;
using BeauUtil;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProtoAqua
{
    public class EventService : ServiceBehaviour
    {
        #region Types

        private class HandlerBlock
        {
            private RingBuffer<Handler> m_Actions = new RingBuffer<Handler>(8, RingBufferMode.Expand);
            private bool m_DeletesQueued;
            private int m_ExecutionDepth;

            public void Invoke(object inContext)
            {
                if (m_Actions.Count == 0)
                    return;

                ++m_ExecutionDepth;

                for(int i = m_Actions.Count - 1; i >= 0; --i)
                {
                    ref Handler handler = ref m_Actions[i];
                    if (!handler.Invoke(inContext))
                        m_DeletesQueued = true;
                }

                if (--m_ExecutionDepth == 0)
                {
                    if (m_DeletesQueued)
                        Cleanup();
                }
            }

            public void Add(in Handler inHandler)
            {
                m_Actions.PushBack(inHandler);
            }

            public void Delete(UnityEngine.Object inObject)
            {
                for(int i = m_Actions.Count - 1; i >= 0; --i)
                {
                    ref Handler handler = ref m_Actions[i];
                    if (handler.Match(inObject))
                    {
                        if (m_ExecutionDepth > 0)
                        {
                            handler.Delete = true;
                            m_DeletesQueued = true;
                        }
                        else
                        {
                            m_Actions.FastRemoveAt(i);
                        }
                    }
                }
            }

            public void Delete(Action inAction)
            {
                for(int i = m_Actions.Count - 1; i >= 0; --i)
                {
                    ref Handler handler = ref m_Actions[i];
                    if (handler.Match(inAction))
                    {
                        if (m_ExecutionDepth > 0)
                        {
                            handler.Delete = true;
                            m_DeletesQueued = true;
                        }
                        else
                        {
                            m_Actions.FastRemoveAt(i);
                        }
                    }
                }
            }

            public void Delete(Action<object> inAction)
            {
                for(int i = m_Actions.Count - 1; i >= 0; --i)
                {
                    ref Handler handler = ref m_Actions[i];
                    if (handler.Match(inAction))
                    {
                        if (m_ExecutionDepth > 0)
                        {
                            handler.Delete = true;
                            m_DeletesQueued = true;
                        }
                        else
                        {
                            m_Actions.FastRemoveAt(i);
                        }
                    }
                }
            }

            public void Clear()
            {
                if (m_ExecutionDepth > 0)
                {
                    for(int i = m_Actions.Count - 1; i >= 0; --i)
                    {
                        ref Handler handler = ref m_Actions[i];
                        handler.Delete = true;
                    }
                    m_DeletesQueued = true;
                }
                else
                {
                    m_Actions.Clear();
                    m_DeletesQueued = false;
                }
            }

            public void Cleanup()
            {
                if (m_ExecutionDepth > 0)
                {
                    m_DeletesQueued = true;
                }
                else
                {
                    for(int i = m_Actions.Count - 1; i >= 0; --i)
                    {
                        if (m_Actions[i].ShouldDelete())
                        {
                            m_Actions.FastRemoveAt(i);
                        }
                    }
                }
            }
        }

        private struct Handler
        {
            private UnityEngine.Object m_Binding;
            private Action m_Action;
            private Action<object> m_ActionWithContext;

            public bool Delete;

            public Handler(Action inAction, UnityEngine.Object inBinding)
            {
                m_Binding = inBinding;
                m_Action = inAction;
                m_ActionWithContext = null;
                Delete = false;
            }

            public Handler(Action<object> inActionWithContext, UnityEngine.Object inBinding)
            {
                m_Binding = inBinding;
                m_Action = null;
                m_ActionWithContext = inActionWithContext;
                Delete = false;
            }

            public bool Match(UnityEngine.Object inBinding)
            {
                return m_Binding.IsReferenceEquals(inBinding);
            }

            public bool Match(Action inAction)
            {
                return m_Action == inAction;
            }

            public bool Match(Action<object> inActionWithContext)
            {
                return m_ActionWithContext == inActionWithContext;
            }

            public bool Invoke(object inContext)
            {
                if (ShouldDelete())
                    return false;
                
                if (m_ActionWithContext != null)
                    m_ActionWithContext(inContext);
                else if (m_Action != null)
                    m_Action();

                return !Delete;
            }

            public bool ShouldDelete()
            {
                return Delete || m_Binding.IsReferenceDestroyed();
            }
        }

        #endregion // Types

        #region Inspector

        [SerializeField] private float m_CleanupInterval = 30;

        #endregion // Inspector

        private Routine m_CleanupRoutine;
        private readonly Dictionary<StringHash32, HandlerBlock> m_Handlers = new Dictionary<StringHash32, HandlerBlock>(64);

        #region Registration

        /// <summary>
        /// Registers an event handler, optionally bound to a given object.
        /// </summary>
        public EventService Register(StringHash32 inEventId, Action inAction, UnityEngine.Object inBinding = null)
        {
            HandlerBlock block;
            if (!m_Handlers.TryGetValue(inEventId, out block))
            {
                block = new HandlerBlock();
                m_Handlers.Add(inEventId, block);
            }
            block.Add(new Handler(inAction, inBinding));

            return this;
        }

        /// <summary>
        /// Registers an event handler, optionally bound to a given object.
        /// </summary>
        public EventService Register(StringHash32 inEventId, Action<object> inActionWithContext, UnityEngine.Object inBinding = null)
        {
            HandlerBlock block;
            if (!m_Handlers.TryGetValue(inEventId, out block))
            {
                block = new HandlerBlock();
                m_Handlers.Add(inEventId, block);
            }
            block.Add(new Handler(inActionWithContext, inBinding));

            return this;
        }

        /// <summary>
        /// Deregisters an event handler.
        /// </summary>
        public EventService Deregister(StringHash32 inEventId, Action inAction)
        {
            HandlerBlock block;
            if (m_Handlers.TryGetValue(inEventId, out block))
            {
                block.Delete(inAction);
            }

            return this;
        }

        /// <summary>
        /// Deregisters an event handler.
        /// </summary>
        public EventService Deregister(StringHash32 inEventId, Action<object> inActionWithContext)
        {
            HandlerBlock block;
            if (m_Handlers.TryGetValue(inEventId, out block))
            {
                block.Delete(inActionWithContext);
            }

            return this;
        }

        /// <summary>
        /// Deregisters all handlers for the given event.
        /// </summary>
        public EventService DeregisterAll(StringHash32 inEventId)
        {
            HandlerBlock block;
            if (m_Handlers.TryGetValue(inEventId, out block))
            {
                block.Clear();
            }

            return this;
        }

        /// <summary>
        /// Deregisters all handlers associated with the given object.
        /// </summary>
        public EventService DeregisterAll(UnityEngine.Object inBinding)
        {
            if (inBinding.IsReferenceNull())
                return this;

            foreach(var block in m_Handlers.Values)
            {
                block.Delete(inBinding);
            }

            return this;
        }

        #endregion // Registration

        #region Operations

        /// <summary>
        /// Dispatches the given event with an optional argument.
        /// </summary>
        public void Dispatch(StringHash32 inEventId, object inContext = null)
        {
            HandlerBlock block;
            if (m_Handlers.TryGetValue(inEventId, out block))
            {
                block.Invoke(inContext);
            }
        }

        /// <summary>
        /// Cleans up all floating handlers.
        /// </summary>
        public void Cleanup()
        {
            foreach(var block in m_Handlers.Values)
            {
                block.Cleanup();
            }
        }

        #endregion // Operations

        #region Maintenance

        private IEnumerator MaintenanceRoutine()
        {
            object delay = m_CleanupInterval;
            while(true)
            {
                yield return delay;
                Cleanup();
            }
        }

        private void OnSceneLoad(SceneBinding inScene, object inContext)
        {
            Cleanup();
        }

        #endregion // Maintenance

        #region IService

        public override FourCC ServiceId()
        {
            return ServiceIds.Events;
        }

        protected override void OnRegisterService()
        {
            m_CleanupRoutine.Replace(this, MaintenanceRoutine());
            SceneHelper.OnSceneLoaded += OnSceneLoad;
        }

        protected override void OnDeregisterService()
        {
            m_Handlers.Clear();
            m_CleanupRoutine.Stop();

            SceneHelper.OnSceneLoaded -= OnSceneLoad;
        }

        #endregion // IService
    }
}