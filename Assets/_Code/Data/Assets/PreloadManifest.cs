using System.Collections;
using System.Collections.Generic;
using BeauData;
using BeauUtil;
using BeauUtil.Blocks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aqua
{
    public class PreloadManifest : ISerializedObject {
        public PreloadGroup[] Groups;

        public void Serialize(Serializer ioSerializer) {
            ioSerializer.ObjectArray("groups", ref Groups);
        }
    }

    public class PreloadGroup : ISerializedObject {
        // Serialized
        public string Id;
        public string[] IncludeBefore;
        public string[] IncludeAfter;
        public string[] Paths;

        // Non-serialized
        public int RefCount;

        public void Serialize(Serializer ioSerializer) {
            ioSerializer.Serialize("id", ref Id, FieldOptions.PreferAttribute);
            ioSerializer.Array("include", ref IncludeBefore, FieldOptions.Optional);
            ioSerializer.Array("lowPriority", ref IncludeAfter, FieldOptions.Optional);
            ioSerializer.Array("paths", ref Paths, FieldOptions.Optional);
        }
    }
}