﻿using Unity.Services.RemoteConfig.Authoring.Editor.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Fetch;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.IO;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Json;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Networking;

namespace Unity.Services.RemoteConfig.Authoring.Editor.Fetch
{
    class EditorRemoteConfigFetchHandler : RemoteConfigFetchHandler
    {
        public EditorRemoteConfigFetchHandler(
            IRemoteConfigClient client, 
            IFileSystem fileSystem, 
            IJsonConverter jsonConverter, 
            IRemoteConfigValidator remoteConfigValidator,
            IRemoteConfigParser remoteConfigParser) 
            : base(client, fileSystem, jsonConverter, remoteConfigValidator, remoteConfigParser)
        {
            
        }

        protected override IRemoteConfigFile ConstructRemoteConfigFile(string path)
        {
            return new RemoteConfigFile()
            {
                Path = path
            };
        }
    }
}