using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Services.RemoteConfig.Authoring.Editor.Formatting;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.ErrorHandling;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Formatting;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;
using Unity.Services.RemoteConfig.Tests.Editor.Authoring.Configs;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Formatting
{
    public class FormatValidatorTests
    {
        FormatValidator m_FormatValidator;
        RemoteConfigParser m_Parser;
        
        [SetUp]
        public void SetUp()
        {
            m_Parser = new RemoteConfigParser(new ConfigTypeDeriver());
            m_FormatValidator = new FormatValidator(new IllegalEntryDetector(), new ConfigTypeDeriver());
        }

        [Test]
        public void ValidFormatWithTypesProvided_ValidationSucceeded()
        {
            var (remoteConfigFile, remoteConfigFileContent) = TestFileGetter.GetTestFileAndContent(AssetFilePaths.Valid1Key, m_Parser);
            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_FormatValidator.Validate(remoteConfigFile, remoteConfigFileContent, deploymentExceptions);
            
            Assert.IsTrue(deploymentExceptions.Count == 0);
        }
        
        [Test]
        public void ValidFormatWithoutTypesProvided_ValidationSucceeded()
        {
            var (remoteConfigFile, remoteConfigFileContent) = TestFileGetter.GetTestFileAndContent(AssetFilePaths.ValidNoTypes, m_Parser);
            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_FormatValidator.Validate(remoteConfigFile, remoteConfigFileContent, deploymentExceptions);
            
            Assert.IsTrue(deploymentExceptions.Count == 0);
        }

        [Test]
        public void MismatchingTypeKeyProvided_ValidationFailed()
        {
            var (remoteConfigFile, remoteConfigFileContent) = TestFileGetter.GetTestFileAndContent(AssetFilePaths.InvalidMismatchInTypes, m_Parser);
            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_FormatValidator.Validate(remoteConfigFile, remoteConfigFileContent, deploymentExceptions);
            
            Assert.AreEqual(1, deploymentExceptions.Count);
            Assert.IsTrue(deploymentExceptions.Any(e => e is MissingEntryForTypeException));
        }

        [Test]
        public void NoEntriesProvided_ValidationFailed()
        {
            var (remoteConfigFile, remoteConfigFileContent) = TestFileGetter.GetTestFileAndContent(AssetFilePaths.InvalidNoEntries, m_Parser);
            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_FormatValidator.Validate(remoteConfigFile, remoteConfigFileContent, deploymentExceptions);
            
            Assert.IsTrue(deploymentExceptions.Count == 1);
            Assert.IsTrue(deploymentExceptions[0] is NoEntriesException);
        }

        [Test]
        public void InvalidTypeProvided_ValidationFailed()
        {
            var (remoteConfigFile, remoteConfigFileContent) = TestFileGetter.GetTestFileAndContent(AssetFilePaths.InvalidTypeSpecified, m_Parser);
            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_FormatValidator.Validate(remoteConfigFile, remoteConfigFileContent, deploymentExceptions);
            
            Assert.IsTrue(deploymentExceptions.Count == 1);
            Assert.IsTrue(deploymentExceptions[0] is InvalidTypeException);
        }

        [Test]
        public void MismatchingTypesProvided_ValidationFailed()
        {
            var (remoteConfigFile, remoteConfigFileContent) = TestFileGetter.GetTestFileAndContent(AssetFilePaths.InvalidTypesNotMatching, m_Parser);
            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_FormatValidator.Validate(remoteConfigFile, remoteConfigFileContent, deploymentExceptions);
            
            Assert.IsTrue(deploymentExceptions.Count == 1);
            Assert.IsTrue(deploymentExceptions[0] is TypeMismatchException);
        }

        [Test]
        public void InvalidKeyPresent_ValidationFailed()
        {
            var (remoteConfigFile, remoteConfigFileContent) = TestFileGetter.GetTestFileAndContent(AssetFilePaths.InvalidKeyPresent, m_Parser);
            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_FormatValidator.Validate(remoteConfigFile, remoteConfigFileContent, deploymentExceptions);
            
            Assert.IsTrue(deploymentExceptions.Count == 1);
            Assert.IsTrue(deploymentExceptions[0] is FileParseException);
        }
    }
}
