using System;
using UnityEngine;
using UnityEngine.UI;
using BeauUtil;
using TMPro;
using BeauPools;
using BeauUtil.UI;

namespace Aqua
{
    public class JobTaskDisplay : MonoBehaviour, IPoolAllocHandler
    {
        [Serializable] public class Pool : SerializablePool<JobTaskDisplay> { }

        #region Inspector

        [Required] public RectTransform Root;
        public CanvasGroup Group;

        [Header("Display")]
        [Required] public LocText Label;
        public Graphic Background;
        public Graphic Checkmark;
        public PointerListener Click;

        [Header("Animation")]
        public Graphic Flash;

        #endregion // Inspector

        [NonSerialized] public JobTask Task;
        [NonSerialized] public bool Completed;
        [NonSerialized] internal float DesiredY;

        public void Populate(JobTask inTask, bool inbCompleted)
        {
            Task = inTask;
            Populate(inTask.LabelId, inbCompleted);
        }

        public void Populate(TextId inLabelId,  bool inbCompleted)
        {
            Label.SetText(inLabelId);

            if (Checkmark)
                Checkmark.enabled = inbCompleted;

            Completed = inbCompleted;
        }

        void IPoolAllocHandler.OnAlloc()
        {
            if (Flash)
                Flash.enabled = false;
        }

        void IPoolAllocHandler.OnFree()
        {
            Task = null;
            
            if (Flash)
                Flash.enabled = false;
            if (Checkmark)
                Checkmark.enabled = false;
        }
    }
}