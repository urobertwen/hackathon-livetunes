using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Services.RemoteConfig.Authoring.Editor.Formatting;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.ErrorHandling;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Configs
{
    public class RemoteConfigValidatorTests
    {
        readonly RemoteConfigValidator m_RemoteConfigValidator = new RemoteConfigValidator();
        readonly RemoteConfigParser m_Parser = new RemoteConfigParser(new ConfigTypeDeriver());

        [Test]
        public void NoDuplicatesSingleFile_ValidationSucceeds()
        {
            var file = TestFileGetter.GetTestFile(AssetFilePaths.ValidBase, m_Parser);
            var entries = file.Entries;

            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_RemoteConfigValidator.FilterValidEntries(new[]
            {
                file
            }, 
                entries, 
                deploymentExceptions);

            Assert.IsTrue(deploymentExceptions.Count == 0);
        }

        [Test]
        public void NoDuplicatesMultipleFiles_ValidationSucceeds()
        {
            var file1 = TestFileGetter.GetTestFile(AssetFilePaths.ValidBase, m_Parser);
            var entries1 = file1.Entries;

            var file2 = TestFileGetter.GetTestFile(AssetFilePaths.ValidOther, m_Parser);
            var entries2 = file2.Entries;
            
            var allEntries = entries1.Concat(entries2).ToList();

            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_RemoteConfigValidator.FilterValidEntries(new[]
                {
                    file1, file2
                },
                allEntries,
                deploymentExceptions);

            Assert.IsTrue(deploymentExceptions.Count == 0);
        }
        
        [Test]
        public void DuplicateMultipleFiles_ValidationFails()
        {
            var file1 = TestFileGetter.GetTestFile(AssetFilePaths.ValidBase, m_Parser);
            var entries1 = file1.Entries;

            var file2 = TestFileGetter.GetTestFile(AssetFilePaths.ValidCopy, m_Parser);
            var entries2 = file2.Entries;
            
            var allEntries = entries1.Concat(entries2).ToList();

            var deploymentExceptions = new List<RemoteConfigDeploymentException>();
            m_RemoteConfigValidator.FilterValidEntries(new[]
                {
                    file1, file2
                },
                allEntries, 
                deploymentExceptions);

            Assert.IsTrue(deploymentExceptions.Count == 2);
            Assert.IsTrue(deploymentExceptions[0] is DuplicateKeysInMultipleFilesException);
            Assert.IsTrue(deploymentExceptions[1] is DuplicateKeysInMultipleFilesException);
        }
    }
}
