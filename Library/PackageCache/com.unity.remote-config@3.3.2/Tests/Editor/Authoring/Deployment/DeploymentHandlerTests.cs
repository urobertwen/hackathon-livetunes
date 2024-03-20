#if NUGET_MOQ_AVAILABLE
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
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
using AssetStateSeverityLevel = Unity.Services.DeploymentApi.Editor.SeverityLevel;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Deployment
{
    public class DeploymentHandlerTests
    {
        Mock<IRemoteConfigClient> m_MockClient;
        Mock<IRemoteConfigParser> m_MockParser;
        Mock<IRemoteConfigValidator> m_MockValidator;
        Mock<IFormatValidator> m_MockFormatValidator;
        Mock<IFileSystem> m_MockFileSystem;
        
        RemoteConfigDeploymentHandler m_DeploymentManager;
        List<IRemoteConfigFile> m_ConfigFiles;

        [SetUp]
        public void SetUp()
        {
            m_MockClient = new Mock<IRemoteConfigClient>();
            m_MockParser = new Mock<IRemoteConfigParser>();
            m_MockValidator = new Mock<IRemoteConfigValidator>();
            m_MockFormatValidator = new Mock<IFormatValidator>();
            m_MockFileSystem = new Mock<IFileSystem>();
            
            m_ConfigFiles = new List<IRemoteConfigFile>
            {
                TestFileGetter.GetTestFile(AssetFilePaths.ValidBase, new RemoteConfigParser(new ConfigTypeDeriver()))
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

            
            m_DeploymentManager = new RemoteConfigDeploymentHandler(m_MockClient.Object,
                m_MockParser.Object,
                m_MockValidator.Object,
                m_MockFormatValidator.Object,
                new ConfigMerger(),
                new JsonConverter(),
                m_MockFileSystem.Object);
        }


        [UnityTest]
        public IEnumerator ConfigDoesNotExist_Creates() => AsyncTest.AsCoroutine(ConfigDoesNotExist_CreatesAsync);
        async Task ConfigDoesNotExist_CreatesAsync()
        {
            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(Task.FromResult(new GetConfigsResult(false, new List<RemoteConfigEntry>())));
            await m_DeploymentManager.DeployAsync(m_ConfigFiles, false, false);
            
            m_MockClient.Verify(c => c.CreateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()), Times.Once());
            m_MockClient.Verify(c => c.UpdateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()), Times.Never());

        }

        [UnityTest]
        public IEnumerator ConfigExists_Updates() => AsyncTest.AsCoroutine(ConfigExists_UpdatesAsync);
        async Task ConfigExists_UpdatesAsync()
        {
            var remoteConfigEntry = new RemoteConfigEntry()
            {
                Key = "dummy_key",
                Value = "dummy_value"
            };

            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(Task.FromResult(new GetConfigsResult(true, new List<RemoteConfigEntry>() {remoteConfigEntry})));
            await m_DeploymentManager.DeployAsync(m_ConfigFiles,false, false);
            
            m_MockClient.Verify(c => c.CreateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()), Times.Never());
            m_MockClient.Verify(c => c.UpdateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()), Times.Once());
        }
        
        [UnityTest]
        public IEnumerator Analytics_DeployTriggered() => AsyncTest.AsCoroutine(Analytics_DeployTriggeredAsync);
        async Task Analytics_DeployTriggeredAsync()
        {
            var mockAnalytics = new Mock<IConfigAnalytics>();
            mockAnalytics
                .Setup(a => a.SendDeployedEvent(It.IsAny<int>(), It.IsAny<IEnumerable<RemoteConfigFile>>()));

            var editorDeploymentManager = new EditorRemoteConfigDeploymentHandler(
                m_MockClient.Object,
                m_MockParser.Object,
                m_MockValidator.Object,
                m_MockFormatValidator.Object,
                new ConfigMerger(),
                new JsonConverter(),
                m_MockFileSystem.Object,
                mockAnalytics.Object
            );

            var remoteConfigEntry = new RemoteConfigEntry()
            {
                Key = "dummy_key",
                Value = "dummy_value"
            };

            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(Task.FromResult(new GetConfigsResult(true, new List<RemoteConfigEntry>()
                {
                    remoteConfigEntry
                })));
            await editorDeploymentManager.DeployAsync(m_ConfigFiles);

            mockAnalytics
                .Verify(a =>
                        a.SendDeployedEvent(
                            It.IsAny<int>(),
                            It.IsAny<IEnumerable<IRemoteConfigFile>>()),
                    Times.Once());
        }

        [UnityTest]
        public IEnumerator DryRunWillNotModifyRemoteFiles() => AsyncTest.AsCoroutine(DryRunWillNotModifyRemoteFilesAsync);
        async Task DryRunWillNotModifyRemoteFilesAsync()
        {
            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(Task.FromResult(new GetConfigsResult(false, new List<RemoteConfigEntry>())));
            await m_DeploymentManager.DeployAsync(m_ConfigFiles, false, true);
            
            m_MockClient.Verify(c => c.CreateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()), Times.Never());
            m_MockClient.Verify(c => c.UpdateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()), Times.Never());
        }
        
        [UnityTest]
        public IEnumerator NoDryRunWillModifyRemoteFiles() => AsyncTest.AsCoroutine(NoDryRunWillModifyRemoteFilesAsync);
        async Task NoDryRunWillModifyRemoteFilesAsync()
        {
            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(Task.FromResult(new GetConfigsResult(false, new List<RemoteConfigEntry>())));
            await m_DeploymentManager.DeployAsync(m_ConfigFiles, false, false);
            
            m_MockClient.Verify(c => c.UpdateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()), Times.Never());
        }
        
        [UnityTest]
        public IEnumerator ReconcileWillDeleteRemoteFiles() => AsyncTest.AsCoroutine(ReconcileWillDeleteRemoteFilesAsync);
        async Task ReconcileWillDeleteRemoteFilesAsync()
        {
            var remoteConfigEntry = new RemoteConfigEntry()
            {
                Key = "dummy_key",
                Value = "dummy_value"
            };

            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(Task.FromResult(new GetConfigsResult(true, new List<RemoteConfigEntry>(){remoteConfigEntry})));
            var result = m_DeploymentManager.DeployAsync(m_ConfigFiles, true, false);

            await result;

            Assert.True(result.Result.Deleted.Any(entry => entry.Key == remoteConfigEntry.Key));
        }
        
        [UnityTest]
        public IEnumerator NoReconcileWillNotDeleteRemoteFiles() => AsyncTest.AsCoroutine(NoReconcileWillNotDeleteRemoteFilesAsync);
        async Task NoReconcileWillNotDeleteRemoteFilesAsync()
        {
            var remoteConfigEntry = new RemoteConfigEntry()
            {
                Key = "dummy_key",
                Value = "dummy_value"
            };

            List<RemoteConfigEntry> sentConfig = null;
            m_MockClient
                .Setup(c => c.UpdateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()))
                .Returns((IReadOnlyList<RemoteConfigEntry> configs) =>
                {
                    sentConfig = configs.ToList();
                    return Task.CompletedTask;
                });
            
            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(Task.FromResult(new GetConfigsResult(true, new List<RemoteConfigEntry>{remoteConfigEntry})));

            await m_DeploymentManager.DeployAsync(m_ConfigFiles);
            
            Assert.Contains(remoteConfigEntry, sentConfig);
            
            foreach (var entry in m_ConfigFiles.SelectMany(file => file.Entries))
            {
                Assert.Contains(entry, sentConfig);
            }
        }
        
        [TestCase(true,true)]
        [TestCase(false,true)]
        [TestCase(true,false)]
        [TestCase(false,false)]
        [Test]
        public async Task DeployWillReturnCorrectResultAsync(bool reconcile, bool dryRun)
        {
            var remoteConfigEntry = new RemoteConfigEntry()
            {
                Key = "dummy_key",
                Value = "dummy_value"
            };
            
            m_MockClient
                .Setup(c => c.UpdateAsync(It.IsAny<IReadOnlyList<RemoteConfigEntry>>()));

            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(Task.FromResult(new GetConfigsResult(true, new List<RemoteConfigEntry>{remoteConfigEntry})));
            var result = await m_DeploymentManager.DeployAsync(m_ConfigFiles, reconcile, dryRun);
            
            Assert.True(reconcile ?
                result.Deleted.Count > 0 :
                result.Deleted.Count == 0);

            Assert.True(dryRun ?
                result.Deployed.Count == 0 :
                result.Deployed.Count > 0);
        }
    }
}
#endif
