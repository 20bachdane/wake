using System;
using Aqua.Option;
using BeauData;
using BeauUtil;
using EasyBugReporter;

namespace Aqua.Profile {
    public struct SaveSummaryData : ISerializedObject, ISerializedVersion {
        public string Id;
        public uint ActId;
        public StringHash32 CurrentLocation;
        public StringHash32 CurrentStation;

        #region ISerializedObject

        public ushort Version { get { return 1; } }

        public void Serialize(Serializer ioSerializer) {
            ioSerializer.Serialize("profileId", ref Id);
            ioSerializer.Serialize("actId", ref ActId);
            ioSerializer.UInt32Proxy("currentLocation", ref CurrentLocation);
            ioSerializer.UInt32Proxy("currentStation", ref CurrentStation);
        }

        #endregion // ISerializedObject

        static public SaveSummaryData FromSave(SaveData data) {
            SaveSummaryData summary;
            summary.Id = data.Id ?? string.Empty;
            summary.ActId = data.Script.ActIndex;
            summary.CurrentLocation = data.Map.SavedSceneId();
            summary.CurrentStation = data.Map.CurrentStationId();
            return summary;
        }
    }
}