using System;
using BeauRoutine.Extensions;
using BeauUtil;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aqua.Portable {
    public class PortableTabToggle : MonoBehaviour {
        #region Inspector

        [SerializeField, Required] private Toggle m_Toggle = null;
        [SerializeField] private PortableAppId m_Id = default;
        [SerializeField, Required] private PortableMenuApp m_App = null;
        [SerializeField] private TMP_Text m_Label = null;

        #endregion // Inspector

        [NonSerialized] private Func<float> m_InitialDelay;

        public PortableAppId Id() { return m_Id; }

        public Toggle Toggle { get { return m_Toggle; } }
        public PortableMenuApp App { get { return m_App; } }

        public void SetInitialDelay(Func<float> delayFunc) {
            m_InitialDelay = delayFunc;
        }

        private void Awake() {
            m_Toggle.onValueChanged.AddListener(OnToggleValue);

            if (m_App != null) {
                m_App.OnShowEvent.AddListener(OnOpened);
                m_App.OnHideEvent.AddListener(OnClosed);
            }
        }

        private void OnDisable() {
            m_Toggle.isOn = false;
        }

        private void OnToggleValue(bool inbValue) {
            if (!m_App || !isActiveAndEnabled)
                return;

            if (inbValue) {
                m_App.Show(m_InitialDelay());
            } else {
                m_App.Hide();
            }
        }

        private void OnOpened(BasePanel.TransitionType inTransition) {
            if (m_Label) {
                m_Label.font = Assets.Font(FontWeight.SemiBold);
            }
            m_Toggle.SetIsOnWithoutNotify(true);
        }

        private void OnClosed(BasePanel.TransitionType inTransition) {
            if (m_Label) {
                m_Label.font = Assets.Font(FontWeight.Regular);
            }
            m_Toggle.SetIsOnWithoutNotify(false);
        }
    }
}