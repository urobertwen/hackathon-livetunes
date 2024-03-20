using System.IO;
using Unity.Services.RemoteConfig.Authoring.Editor.IO;
using Unity.Services.RemoteConfig.Authoring.Editor.Json;
using Unity.Services.RemoteConfig.Authoring.Editor.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Configs
{
    static class TestFileGetter
    {
        public static IRemoteConfigFile GetTestFile(string filePath, IRemoteConfigParser parser)
        {
            return GetTestFileAndContent(filePath, parser).Item1;
        }
        
        public static (IRemoteConfigFile, RemoteConfigFileContent) GetTestFileAndContent(string filePath, IRemoteConfigParser parser)
        {
            var file = new RemoteConfigFile()
            {
                Path = GetTestFilePath(filePath)
            };

            return (file, SetContent(file,parser));
        }
        
        static string GetTestFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                filePath = filePath.Replace("com.unity.remote-config", "com.unity.remote-config.tests");
            }

            return filePath;
        }
        
        static RemoteConfigFileContent SetContent(IRemoteConfigFile configFile, IRemoteConfigParser parser)
        {
            var converter = new JsonConverter();
            var fileReader = new FileSystem();
            var txt = fileReader.ReadAllText(configFile.Path).Result;
            var remoteConfigFileContent = converter.DeserializeObject<RemoteConfigFileContent>(
                txt, 
                true);

           configFile.Entries = remoteConfigFileContent.ToRemoteConfigEntries(configFile, parser);

           return remoteConfigFileContent;
        }
    }
}
