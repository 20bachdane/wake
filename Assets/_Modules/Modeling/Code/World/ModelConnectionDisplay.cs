using System;
using BeauPools;
using UnityEngine;
using UnityEngine.UI;
using BeauUtil;
using BeauUtil.UI;

namespace Aqua.Modeling {
    public class ModelConnectionDisplay : MonoBehaviour, IPoolAllocHandler {

        #region Inspector

        public RectTransform Transform;
        public CanvasGroup CanvasGroup;
        public RawImage Texture;
        public ScrollTiledRawImage Scroll;
        public Graphic Arrow;
        public GameObject Fader;
        public Image Icon;

        #endregion // Inspector

        [NonSerialized] public BFBase Fact;
        [NonSerialized] public BFBase Fact2;
        [NonSerialized] public int Key;
        [NonSerialized] public ushort IndexA;
        [NonSerialized] public ushort IndexB;
        [NonSerialized] public int Order;
        [NonSerialized] public int ConnectionIndex;
        [NonSerialized] public WorldFilterMask Mask;

        void IPoolAllocHandler.OnAlloc() {
        }

        void IPoolAllocHandler.OnFree() {
            Fact = null;
            Fact2 = null;
        }
    }
}