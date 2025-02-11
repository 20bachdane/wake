using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aqua
{
    [CreateAssetMenu(menuName = "Aqualab System/Keycode Display Map")]
    public class KeycodeDisplayMap : ScriptableObject
    {
        public struct Mapping
        {
            public Sprite Image;
            public string Text;

            public Mapping(Sprite inSprite, string inText = null)
            {
                Image = inSprite;
                Text = inText;
            }
        }

        #region Inspector

        [SerializeField] private Sprite m_MouseLeft = null;
        [SerializeField] private Sprite m_MouseRight = null;
        [SerializeField] private Sprite m_Tap = null;
        [SerializeField] private Sprite m_SmallKey = null;
        [SerializeField] private Sprite m_MediumKey = null;
        // [SerializeField] private Sprite m_WideKey = null;

        #endregion // Inspector

        public bool TouchscreenMode;

        [NonSerialized] private Dictionary<int, Mapping> m_MappingCache = new Dictionary<int, Mapping>();

        public Mapping ForKey(KeyCode inKeycode)
        {
            if (inKeycode >= KeyCode.Mouse0 && inKeycode <= KeyCode.Mouse6)
                return ForMouse(inKeycode - KeyCode.Mouse0);

            Mapping mapping;
            if (m_MappingCache.TryGetValue((int) inKeycode, out mapping))
                return mapping;

            switch(inKeycode)
            {
                case KeyCode.LeftControl:
                    mapping.Image = m_MediumKey;
                    mapping.Text = "Ctrl";
                    break;

                case KeyCode.LeftShift:
                    mapping.Image = m_MediumKey;
                    mapping.Text = "Shift";
                    break;

                case KeyCode.Return:
                case KeyCode.Tab:
                case KeyCode.CapsLock:
                case KeyCode.Backspace:
                    mapping.Image = m_MediumKey;
                    break;

                case KeyCode.Space:
                    mapping.Image = m_MediumKey;
                    break;

                case KeyCode.Alpha0:
                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                case KeyCode.Alpha4:
                case KeyCode.Alpha5:
                case KeyCode.Alpha6:
                case KeyCode.Alpha7:
                case KeyCode.Alpha8:
                case KeyCode.Alpha9:
                    mapping.Image = m_SmallKey;
                    mapping.Text = ((int)(inKeycode - KeyCode.Alpha0)).ToString();
                    break;

                default:
                    mapping.Image = m_SmallKey;
                    break;
            }

            if (string.IsNullOrEmpty(mapping.Text))
                mapping.Text = inKeycode.ToString();

            m_MappingCache[(int) inKeycode] = mapping;
            return mapping;
        }

        public Mapping ForMouse(int inMouseButton)
        {
            switch(inMouseButton)
            {
                case 0:
                    return new Mapping(TouchscreenMode ? m_Tap : m_MouseLeft);
                case 1:
                    return new Mapping(m_MouseRight);
                default:
                    throw new ArgumentOutOfRangeException("inMouseButton");
            }
        }
    }
}