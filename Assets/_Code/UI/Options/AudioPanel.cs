using UnityEngine;
using AquaAudio;

namespace Aqua.Option
{
    public class AudioPanel : OptionsDisplay.Panel
    {
        #region Inspector

        [SerializeField] private SoundOptionBar m_MasterBus = null;
        
        [Header("SubBusses")]
        [SerializeField] private CanvasGroup m_SubBusGroup = null;
        [SerializeField] private SoundOptionBar m_MusicBus = null;
        [SerializeField] private SoundOptionBar m_SFXBus = null;
        [SerializeField] private SoundOptionBar m_VoiceBus = null;

        // [Header("Mono")]
        // [SerializeField] private CheckboxOption m_MonoCheckbox = null;

        #endregion // Inspector

        protected override void Init()
        {
            m_MasterBus.OnChanged = OnMasterBusChanged;

            m_MasterBus.Initialize(AudioBusId.Master, 0.9f);
            m_MusicBus.Initialize(AudioBusId.Music, 0.8f);
            m_SFXBus.Initialize(AudioBusId.SFX, 0.8f);
            m_VoiceBus.Initialize(AudioBusId.Voice, 0.8f);

            // m_MonoCheckbox.Initialize("options.audio.mono.label", "options.audio.mono.description", (b) => Data.Audio.Mono = b);
        }

        public override void Load(OptionsData inOptions)
        {
            base.Load(inOptions);
            
            m_MasterBus.Sync(inOptions.Audio.Master);
            m_MusicBus.Sync(inOptions.Audio.Music);
            m_SFXBus.Sync(inOptions.Audio.SFX);
            m_VoiceBus.Sync(inOptions.Audio.Voice);
            // m_MonoCheckbox.Sync(inOptions.Audio.Mono);

            OnMasterBusChanged(AudioBusId.Master, inOptions.Audio.Master);
        }

        private void OnMasterBusChanged(AudioBusId inBusId, OptionAudioBus inBus)
        {
            if (inBus.Mute)
            {
                m_SubBusGroup.interactable = false;
                m_SubBusGroup.alpha = 0.5f;
            }
            else
            {
                m_SubBusGroup.interactable = true;
                m_SubBusGroup.alpha = 1;
            }
        }
    }
}