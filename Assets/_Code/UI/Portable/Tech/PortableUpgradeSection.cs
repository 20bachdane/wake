using UnityEngine;
using BeauPools;
using Aqua.Profile;
using UnityEngine.UI;
using BeauUtil;
using BeauRoutine;
using System.Collections;
using System;
using BeauUtil.Debugger;
using EasyAssetStreaming;

namespace Aqua.Portable
{
    public class PortableUpgradeSection : MonoBehaviour
    {
        #region Inspector

        [SerializeField] private GameObject m_NoUpgradesGroup = null;
        [SerializeField] private GameObject m_HasUpgradesGroup = null;
        [NonSerialized] private PortableUpgradeButton[] m_UpgradeIcons = null;

        #endregion // Inspector

        private void Awake()
        {
            m_UpgradeIcons = m_HasUpgradesGroup.GetComponentsInChildren<PortableUpgradeButton>(true);
            foreach(var icon in m_UpgradeIcons) {
                PortableUpgradeButton cached = icon;
                icon.Button.onClick.AddListener(() => OnClickUpgrade(cached));
            }
        }

        static private void OnClickUpgrade(PortableUpgradeButton icon) {
            Script.PopupItemDetails(icon.CachedItem);
        }

        public void Clear()
        {
            m_HasUpgradesGroup.SetActive(false);
            m_NoUpgradesGroup.SetActive(true);
        }

        public void Load(ListSlice<InvItem> inUpgrades)
        {
            if (inUpgrades.Length == 0)
            {
                Clear();
                return;
            }

            Assert.True(inUpgrades.Length <= m_UpgradeIcons.Length, "Too few icons ({0}) to handle {1} upgrades", m_UpgradeIcons.Length, inUpgrades.Length);

            m_NoUpgradesGroup.SetActive(false);
            m_HasUpgradesGroup.SetActive(true);

            for(int i = 0; i < inUpgrades.Length; i++)
            {
                Populate(m_UpgradeIcons[i], inUpgrades[i]);
            }

            for(int i = inUpgrades.Length; i < m_UpgradeIcons.Length; i++)
            {
                m_UpgradeIcons[i].gameObject.SetActive(false);
            }
        }

        static private void Populate(PortableUpgradeButton ioIcon, InvItem inInv)
        {
            ioIcon.Cursor.TooltipId = inInv.DescriptionTextId();
            ioIcon.Title.SetText(inInv.NameTextId());
            ioIcon.Icon.sprite = inInv.Icon();
            ioIcon.gameObject.SetActive(true);
            ioIcon.CachedItem = inInv;
        }
    }
}