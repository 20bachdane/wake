using System;
using BeauUtil;
using BeauUtil.Debugger;
using UnityEngine;
using UnityEngine.UI;

namespace Aqua
{
    public class InputCursor : MonoBehaviour
    {
        [SerializeField] private Image m_Image = null;
        [SerializeField] private Sprite[] m_InteractSprites = null;
        
        [NonSerialized] private RectTransform m_Transform;
        [NonSerialized] private Sprite m_OriginalSprite;

        [NonSerialized] private StringHash32 m_PosLock;
        [NonSerialized] private StringHash32 m_SpriteLock;
        [NonSerialized] private StringHash32 m_RotationLock;
        [NonSerialized] private Sprite m_SpriteLockSprite;

        [NonSerialized] private CursorImageType m_InteractionImage;

        static private readonly Vector3 MouseDownScale = new Vector3(0.75f, 0.75f, 1);
        static private readonly Vector3 MouseUpScale = new Vector3(1, 1, 1);

        #region Events

        private void Awake()
        {
            this.CacheComponent(ref m_Transform);
            m_OriginalSprite = m_Image.sprite;
        }

        private void OnEnable()
        {
            this.CacheComponent(ref m_Transform);
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.visible = true;
        }

        public Vector2 Process()
        {
            #if UNITY_EDITOR
            Cursor.visible = false;
            #endif // UNITY_EDITOR

            Vector2 position;
            
            if (m_PosLock.IsEmpty)
            {
                position = Input.mousePosition;
                m_Transform.position = position;
            }
            else
            {
                position = m_Transform.position;
            }

            if (m_InteractionImage > 0)
            {
                m_Image.sprite = m_InteractSprites[(int) m_InteractionImage - 1];
            }
            else if (!m_SpriteLockSprite.IsReferenceNull())
            {
                m_Image.sprite = m_SpriteLockSprite;
            }
            else
            {
                m_Image.sprite = m_OriginalSprite;
            }

            if (Input.GetMouseButton(0))
            {
                m_Transform.localScale = MouseDownScale;
            }
            else
            {
                m_Transform.localScale = MouseUpScale;
            }

            return position;
        }

        #endregion // Events

        #region Position Lock

        public void LockPosition(StringHash32 inHash, Vector2 inScreenPosition)
        {
            Assert.True(m_PosLock.IsEmpty || m_PosLock == inHash, "Current position lock is {0}, attempting to lock with mismatched key {1}", m_PosLock, inHash);
            m_PosLock = inHash;
            inScreenPosition.x = (float) Math.Round(inScreenPosition.x);
            inScreenPosition.y = (float) Math.Round(inScreenPosition.y);
            m_Transform.position = inScreenPosition;
        }

        public void ReleasePosition(StringHash32 inHash)
        {
            Assert.True(inHash == m_PosLock, "Current position lock is {0}, attempting to unlock with mismatched key {1}", m_PosLock, inHash);
            m_PosLock = StringHash32.Null;
        }
    
        #endregion // Position Lock

        #region Sprite Lock

        public void LockSprite(StringHash32 inHash, Sprite inSprite)
        {
            Assert.True(m_SpriteLock.IsEmpty || m_SpriteLock == inHash, "Current sprite lock is {0}, attempting to lock with mismatched key {1}", m_SpriteLock, inHash);
            m_SpriteLock = inHash;
            m_SpriteLockSprite = inSprite;
        }

        public void ReleaseSprite(StringHash32 inHash)
        {
            Assert.True(inHash == m_SpriteLock, "Current sprite lock is {0}, attempting to unlock with mismatched key {1}", m_SpriteLock, inHash);
            m_SpriteLock = StringHash32.Null;
            m_SpriteLockSprite = null;
        }

        #endregion // Sprite Lock

        #region Rotation Lock

        public void LockRotation(StringHash32 inHash, float inRotation)
        {
            Assert.True(m_RotationLock.IsEmpty || m_RotationLock == inHash, "Current rotation lock is {0}, attempting to lock with mismatched key {1}", m_RotationLock, inHash);
            m_RotationLock = inHash;
            m_Transform.localEulerAngles = new Vector3(0, 0, inRotation);
        }

        public void ReleaseRotation(StringHash32 inHash)
        {
            Assert.True(inHash == m_RotationLock, "Current rotation lock is {0}, attempting to unlock with mismatched key {1}", m_RotationLock, inHash);
            m_RotationLock = StringHash32.Null;
            m_Transform.localEulerAngles = default(Vector3);
        }

        #endregion // Rotation Lock

        #region Interaction

        public void SetInteractionHint(CursorImageType inImageType)
        {
            m_InteractionImage = inImageType;
        }

        #endregion // Interaction
    }

    [LabeledEnum]
    public enum CursorImageType : byte
    {
        [Hidden]
        None = 0,
        
        Select,
        Question,
        Tooltip,
        Camera,
        Talk
    }
}