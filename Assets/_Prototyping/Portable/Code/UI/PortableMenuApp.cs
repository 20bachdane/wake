using UnityEngine;
using UnityEngine.UI;
using BeauRoutine;
using BeauRoutine.Extensions;
using BeauUtil;

namespace ProtoAqua.Portable
{
    public class PortableMenuApp : BasePanel
    {
        #region Inspector

        [Header("Portable App")]
        [SerializeField] private SerializedHash32 m_Id = null;

        #endregion // Inspector

        public StringHash32 Id() { return m_Id; }
    }
}