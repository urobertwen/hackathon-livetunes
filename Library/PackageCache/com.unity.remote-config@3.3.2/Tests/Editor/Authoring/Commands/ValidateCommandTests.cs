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
using Unity.Services.RemoteConfig.Authoring.Editor.Deployment;
using Unity.Services.RemoteConfig.Authoring.Editor.Formatting;
using Unity.Services.RemoteConfig.Authoring.Editor.Json;
using Unity.Services.RemoteConfig.Authoring.Editor.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.ErrorHandling;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Formatting;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.IO;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;
using Unity.Services.RemoteConfig.Tests.Editor.Authoring.Configs;
using Unity.Services.RemoteConfig.Tests.Editor.Authoring.Shared;
using UnityEngine.TestTools;
using AssetStateSeverityLevel = Unity.Services.DeploymentApi.Editor.SeverityLevel;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Commands
{
    public class ValidateCommandTests
    {
        Mock<IRemoteConfigParser> m_MockParser;
        Mock<IRemoteConfigValidator> m_MockValidator;
        Mock<IFormatValidator> m_MockFormatValidator;
        Mock<IFileSystem> m_MockFileSystem;

        ValidateCommand m_Command;
        List<IRemoteConfigFile> m_ConfigFiles;

        [SetUp]
        public void SetUp()
        {
            m_MockParser = new Mock<IRemoteConfigParser>();
            m_MockValidator = new Mock<IRemoteConfigValidator>();
            m_MockFormatValidator = new Mock<IFormatValidator>();
            m_ConfigFiles = new List<IRemoteConfigFile>();
            m_MockFileSystem = new Mock<IFileSystem>();

            var remoteConfigParser = new RemoteConfigParser(new ConfigTypeDeriver());
            m_ConfigFiles = new List<IRemoteConfigFile>()
            {
                TestFileGetter.GetTestFile(AssetFilePaths.ValidBase, remoteConfigParser),
                TestFileGetter.GetTestFile(AssetFilePaths.ValidBase, remoteConfigParser)
            };
            
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
                        .SelectMany(file => file.Entries)
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

            m_Command = new ValidateCommand(
                m_MockValidator.Object, 
                m_MockFormatValidator.Object, 
                m_MockParser.Object, 
                m_MockFileSystem.Object, 
                new JsonConverter());
        }
        
        [UnityTest]
        public IEnumerator ValidationFailed_AssetStateUpdated() => AsyncTest.AsCoroutine(ValidationFailed_AssetStateUpdatedAsync);
        async Task ValidationFailed_AssetStateUpdatedAsync()
        {
            var configFile = (RemoteConfigFile) m_ConfigFiles.First();

            m_MockValidator
                .Setup(
                    validator => validator.FilterValidEntries(
                        It.IsAny<IReadOnlyList<IRemoteConfigFile>>(),
                        It.IsAny<IReadOnlyList<RemoteConfigEntry>>(),
                        It.IsAny<ICollection<RemoteConfigDeploymentException>>()))
                .Returns(Array.Empty<RemoteConfigEntry>())
                .Callback<
                    IReadOnlyList<IRemoteConfigFile>, 
                    IReadOnlyList<RemoteConfigEntry>, 
                    ICollection<RemoteConfigDeploymentException>>((_, _, deploymentExceptions) =>
                {
                    deploymentExceptions.Add(new NoEntriesException(configFile));
                });

            await m_Command.ExecuteAsync(m_ConfigFiles.Cast<IDeploymentItem>(), CancellationToken.None);

            Assert.AreEqual(AssetStateSeverityLevel.Error, configFile.States.FirstOrDefault().Level);
        }
        
        [UnityTest]
        public IEnumerator BothValidatorsFail_DisplaysBothErrorTypes() => AsyncTest.AsCoroutine(BothValidatorsFail_DisplaysBothErrorTypesAsync);
        async Task BothValidatorsFail_DisplaysBothErrorTypesAsync()
        {
            var configFile1 = (RemoteConfigFile) m_ConfigFiles.First();
            var configFile2 = (RemoteConfigFile) m_ConfigFiles.Skip(1).First();

            m_MockValidator
                .Setup(
                    validator => validator.FilterValidEntries(
                        It.IsAny<IReadOnlyList<IRemoteConfigFile>>(),
                        It.IsAny<IReadOnlyList<RemoteConfigEntry>>(),
                        It.IsAny<ICollection<RemoteConfigDeploymentException>>()))
                .Returns(Array.Empty<RemoteConfigEntry>())
                .Callback<IReadOnlyList<IRemoteConfigFile>, 
                    IReadOnlyList<RemoteConfigEntry>, 
                    ICollection<RemoteConfigDeploymentException>>((_, _, deploymentExceptions) =>
                {
                    deploymentExceptions.Add(new NoEntriesException(configFile1));
                });

            m_MockFormatValidator.Setup(mv => mv.Validate(
                    configFile2, 
                    It.IsAny<RemoteConfigFileContent>(), 
                    It.IsAny<ICollection<RemoteConfigDeploymentException>>()))
                .Returns(false)
                .Callback<IRemoteConfigFile, RemoteConfigFileContent, ICollection<RemoteConfigDeploymentException>>((_, _, exceptions) =>
                {
                    exceptions.Add(new FileParseException(configFile2));
                });

            await m_Command.ExecuteAsync(m_ConfigFiles.Cast<IDeploymentItem>(), CancellationToken.None);

            Assert.AreEqual(AssetStateSeverityLevel.Error, configFile1.States.FirstOrDefault().Level);
            Assert.AreEqual(AssetStateSeverityLevel.Error, configFile2.States.FirstOrDefault().Level);
        }
        
        [UnityTest]
        public IEnumerator ResetsAssetStates() => AsyncTest.AsCoroutine(ResetsAssetStatesAsync);
        async Task ResetsAssetStatesAsync()
        {
            var configFile = (RemoteConfigFile) m_ConfigFiles.First();
            configFile.States.Add(new AssetState());
            
            await m_Command.ExecuteAsync(m_ConfigFiles.Cast<IDeploymentItem>(), CancellationToken.None);

            Assert.IsEmpty(configFile.States);
        }
        
        [UnityTest]
        public IEnumerator FormatValidationFailed_AssetStateUpdated() => AsyncTest.AsCoroutine(FormatValidationFailed_AssetStateUpdatedAsync);
        async Task FormatValidationFailed_AssetStateUpdatedAsync()
        {
            var configFile = (RemoteConfigFile) m_ConfigFiles.First();
            
            m_MockFormatValidator.Setup(mv => mv.Validate(
                    It.IsAny<IRemoteConfigFile>(),
                    It.IsAny<RemoteConfigFileContent>(),
                    It.IsAny<ICollection<RemoteConfigDeploymentException>>()))
                .Returns(false)
                .Callback<IRemoteConfigFile, RemoteConfigFileContent, ICollection<RemoteConfigDeploymentException>>((_, _, exceptions) =>
                {
                    exceptions.Add(new FileParseException(configFile));
                });

            await m_Command.ExecuteAsync(m_ConfigFiles.Cast<IDeploymentItem>(), CancellationToken.None);

            Assert.AreEqual(AssetStateSeverityLevel.Error, configFile.States.FirstOrDefault().Level);
        }
        
        [UnityTest]
        public IEnumerator InvalidJsonFormat_AssetStateUpdated() => AsyncTest.AsCoroutine(InvalidJsonFormat_AssetStateUpdatedAsync);
        async Task  InvalidJsonFormat_AssetStateUpdatedAsync()
        {
            var configFile = (RemoteConfigFile) m_ConfigFiles.First();

            m_MockFileSystem.SetupFileSystemText(string.Empty);
            m_MockFormatValidator.SetupFormatValidator(exceptions => exceptions.Add(new FileParseException(configFile)));

            await m_Command.ExecuteAsync(m_ConfigFiles.Cast<IDeploymentItem>(), CancellationToken.None);

            Assert.AreEqual(AssetStateSeverityLevel.Error, configFile.States.FirstOrDefault().Level);
        }
    }
}
#endif