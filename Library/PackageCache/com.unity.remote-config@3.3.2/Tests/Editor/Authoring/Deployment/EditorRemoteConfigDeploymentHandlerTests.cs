#if NUGET_MOQ_AVAILABLE

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Unity.Services.DeploymentApi.Editor;
using Unity.Services.RemoteConfig.Authoring.Editor.Analytics;
using Unity.Services.RemoteConfig.Authoring.Editor.Deployment;
using Unity.Services.RemoteConfig.Authoring.Editor.Formatting;
using Unity.Services.RemoteConfig.Authoring.Editor.Json;
using Unity.Services.RemoteConfig.Authoring.Editor.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.ErrorHandling;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Formatting;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.IO;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Networking;
using Unity.Services.RemoteConfig.Tests.Editor.Authoring.Configs;
using Unity.Services.RemoteConfig.Tests.Editor.Authoring.Shared;
using UnityEngine.TestTools;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Deployment
{
    class EditorRemoteConfigDeploymentHandlerTests
    {
        Mock<IRemoteConfigClient> m_MockClient;
        Mock<IRemoteConfigParser> m_MockParser;
        Mock<IRemoteConfigValidator> m_MockValidator;
        Mock<IFormatValidator> m_MockFormatValidator;
        Mock<IFileSystem> m_MockFileSystem;
        Mock<IConfigAnalytics> m_ConfigAnalytics;

        RemoteConfigDeploymentHandler m_DeploymentHandler;
        List<IRemoteConfigFile> m_ConfigFiles;
        
        [SetUp]
        public void SetUp()
        {
            m_MockClient = new Mock<IRemoteConfigClient>();
            m_MockParser = new Mock<IRemoteConfigParser>();
            m_MockValidator = new Mock<IRemoteConfigValidator>();
            m_MockFormatValidator = new Mock<IFormatValidator>();
            m_MockFileSystem = new Mock<IFileSystem>();
            m_ConfigAnalytics = new Mock<IConfigAnalytics>();
            
            m_ConfigFiles = new List<IRemoteConfigFile>
            {
                TestFileGetter.GetTestFile(AssetFilePaths.ValidBase, new RemoteConfigParser(new ConfigTypeDeriver())),
                new RemoteConfigFile()
                {
                    Name = "myFile",
                    Path = "path/myFile.rc",
                    Entries = new List<RemoteConfigEntry>()
                }
            };
            
            m_MockClient
                .Setup(c => c.CreateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()))
                .Returns(Task.CompletedTask);
            m_MockClient
                .Setup(c => c.UpdateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()))
                .Returns(Task.CompletedTask);
            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(Task.FromResult(new GetConfigsResult(true, new List<RemoteConfigEntry>())));
            m_MockFormatValidator
                .Setup(
                    validator => validator.Validate(
                        It.IsAny<RemoteConfigFile>(),
                        It.IsAny<RemoteConfigFileContent>(),
                        It.IsAny<ICollection<RemoteConfigDeploymentException>>()))
                .Returns(true);
            m_MockValidator
                .Setup(
                    validator => validator.FilterValidEntries(
                        It.IsAny<IReadOnlyList<IRemoteConfigFile>>(),
                        It.IsAny<IReadOnlyList<RemoteConfigEntry>>(),
                        It.IsAny<ICollection<RemoteConfigDeploymentException>>()))
                .Returns(
                    m_ConfigFiles
                        .SelectMany(file=>file.Entries)
                        .ToList());
            
            foreach (var file in m_ConfigFiles)
            {
                m_MockParser
                    .Setup(parser => parser.ParseFile(
                        It.IsAny<RemoteConfigFileContent>(),
                        file))
                    .Returns(file.Entries);
            }
            
            m_MockFileSystem
                .Setup(fr => fr.ReadAllText(It.IsAny<string>(), CancellationToken.None))
                .Returns(Task.FromResult(new JsonConverter().SerializeObject(new RemoteConfigFileContent())));

            m_DeploymentHandler = new EditorRemoteConfigDeploymentHandler(m_MockClient.Object,
                m_MockParser.Object,
                m_MockValidator.Object,
                m_MockFormatValidator.Object,
                new ConfigMerger(),
                new JsonConverter(),
                m_MockFileSystem.Object, 
                m_ConfigAnalytics.Object);
        }

        [UnityTest]
        public IEnumerator UpdateStatus_HasMessageDetail() => AsyncTest.AsCoroutine(DeployAsync_HasMessageDetailAsync_Success);
        async Task DeployAsync_HasMessageDetailAsync_Success()
        {
            var configFile =(RemoteConfigFile) m_ConfigFiles[0];
            await m_DeploymentHandler.DeployAsync(m_ConfigFiles);
            
            Assert.AreEqual(configFile.Status.MessageDetail, "Deployed Successfully");
        }
        
        [UnityTest]
        public IEnumerator DeployAsync_HasMessageDetailAsync_Failure() => AsyncTest.AsCoroutine(DeployAsync_HasMessageDetailAsync_FailureAsync);

        async Task DeployAsync_HasMessageDetailAsync_FailureAsync()
        {
            var configFile = (RemoteConfigFile)m_ConfigFiles[1];
            m_MockFormatValidator.Setup(fv =>
                    fv.Validate(
                        It.IsAny<IRemoteConfigFile>(),
                        It.IsAny<RemoteConfigFileContent>(),
                        It.IsAny<ICollection<RemoteConfigDeploymentException>>()))
                .Callback<IRemoteConfigFile, RemoteConfigFileContent, ICollection<RemoteConfigDeploymentException>>((_, _, exceptions) =>
                {
                    exceptions.Add(new FileParseException(configFile));
                });

            try
            {
                await m_DeploymentHandler.DeployAsync(m_ConfigFiles);
                Assert.Fail();
            }
            catch (FileParseException e)
            {
                Assert.AreEqual(e.StatusDetail, configFile.Status.MessageDetail);
            }
        }
        
        [UnityTest]
        public IEnumerator DeployAsync_MalformedJson_Failure() => AsyncTest.AsCoroutine(DeployAsync_MalformedJson_FailureAsync);

        async Task DeployAsync_MalformedJson_FailureAsync()
        {
            var configFile = (RemoteConfigFile)m_ConfigFiles[1];
  
            m_MockFileSystem.SetupFileSystemText(string.Empty);
            m_MockFormatValidator.SetupFormatValidator(exceptions => exceptions.Add(new FileParseException(configFile)));

            try
            {
                await m_DeploymentHandler.DeployAsync(m_ConfigFiles);
                Assert.Fail();
            }
            catch (FileParseException e)
            {
                Assert.AreEqual(e.StatusDetail, configFile.Status.MessageDetail);
            }
        }
        
        [UnityTest]
        public IEnumerator Deploy_GetConfigFailsSetsStatus() => AsyncTest.AsCoroutine(Deploy_GetConfigFailsSetsStatusAsync);
        Task Deploy_GetConfigFailsSetsStatusAsync()
        {
            m_MockClient
                .Setup(c => c.GetAsync())
                .Throws<Exception>();
            
            Assert.ThrowsAsync<Exception>(async() => await m_DeploymentHandler.DeployAsync(m_ConfigFiles));

            foreach (var file in m_ConfigFiles.Cast<RemoteConfigFile>())
            {
                Assert.AreEqual(DeploymentStatus.FailedToDeploy.Message, file.Status.Message);
            }
            return Task.CompletedTask;
        }
    }
}
#endif