using System;
using System.Collections.Generic;
using Unity.Services.DeploymentApi.Editor;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;

namespace Unity.Services.RemoteConfig.Authoring.Editor.Model
{
    [Serializable]
    class RemoteConfigFile : DeploymentItem, IRemoteConfigFile
    {
        public sealed override string Path
        {
            get => base.Path;
            set
            {
                base.Path = value;
                Name = System.IO.Path.GetFileName(value);
            }
        }
        public List<RemoteConfigEntry> Entries { get; set; }
    }
}
