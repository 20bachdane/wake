using UnityEngine;
using BeauUtil;
using BeauRoutine;
using System.Collections;
using Aqua;

namespace ProtoAqua.Observation
{
    public class ToolBarUI : SharedPanel, ISceneLoadHandler
    {
        [SerializeField] private ToolButton m_ScannerButton = null;
        [SerializeField] private ToolButton m_TaggerButton = null;
        [SerializeField] private GameObject m_Divider = null;
        [SerializeField] private ToolButton m_FlashlightButton = null;

        protected override void Awake()
        {
            m_ScannerButton.Toggle.onValueChanged.AddListener((b) => { if (b) OnToolSelected(PlayerROV.ToolId.Scanner); });
            m_TaggerButton.Toggle.onValueChanged.AddListener((b) => { if (b) OnToolSelected(PlayerROV.ToolId.Tagger); });
            m_FlashlightButton.Toggle.onValueChanged.AddListener((b) => { OnToolToggled(PlayerROV.ToolId.Flashlight, b); });

            Services.Events.Register<PlayerROV.ToolState>(PlayerROV.Event_ToolSwitched, OnToolSwitched, this);
            Services.Events.Register(GameEvents.InventoryUpdated, RefreshItemList, this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Services.Events?.DeregisterAll(this);
        }

        private void OnToolSelected(PlayerROV.ToolId inToolId)
        {
            Services.Events.Dispatch(PlayerROV.Event_RequestToolSwitch, inToolId);
        }

        private void OnToolToggled(PlayerROV.ToolId inToolId, bool state)
        {
            Services.Events.Dispatch(PlayerROV.Event_RequestToolToggle, new PlayerROV.ToolState(inToolId, state));
        }

        private void OnToolSwitched(PlayerROV.ToolState state)
        {
            switch(state.Id)
            {
                case PlayerROV.ToolId.Scanner:
                    m_ScannerButton.Toggle.SetIsOnWithoutNotify(state.Active);
                    break;

                case PlayerROV.ToolId.Tagger:
                    m_TaggerButton.Toggle.SetIsOnWithoutNotify(state.Active);
                    break;

                case PlayerROV.ToolId.Flashlight:
                    m_FlashlightButton.Toggle.SetIsOnWithoutNotify(state.Active);
                    break;
            }
        }

        private void RefreshItemList()
        {
            bool bHasScanner = Save.Inventory.HasUpgrade(ItemIds.ROVScanner);
            bool bHasTagger = Save.Inventory.HasUpgrade(ItemIds.ROVTagger);
            bool bHasFlashlight = Save.Inventory.HasUpgrade(ItemIds.Flashlight);

            bool bHasPrimary = bHasScanner || bHasTagger;
            bool bHasSecondary = bHasFlashlight;
            
            int itemCount = 0;
            if (bHasScanner)
                itemCount++;
            if (bHasTagger)
                itemCount++;
            if (bHasFlashlight)
                itemCount++;

            if (itemCount < 2 && !bHasSecondary)
            {
                Hide();
                return;
            }

            Show();
            m_ScannerButton.gameObject.SetActive(bHasScanner);
            m_TaggerButton.gameObject.SetActive(bHasTagger);
            m_FlashlightButton.gameObject.SetActive(bHasFlashlight);

            m_Divider.SetActive(bHasPrimary && bHasSecondary);
        }

        void ISceneLoadHandler.OnSceneLoad(SceneBinding inScene, object inContext)
        {
            RefreshItemList();
        }
    }
}