#if NUGET_MOQ_AVAILABLE
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using Unity.Services.RemoteConfig.Authoring.Editor.Analytics;
using Unity.Services.RemoteConfig.Authoring.Editor.Formatting;
using Unity.Services.RemoteConfig.Authoring.Editor.IO;
using Unity.Services.RemoteConfig.Authoring.Editor.Json;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;
using Unity.Services.RemoteConfig.Tests.Editor.Authoring.Configs;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Analytics
{
    class ConfigAnalyticsTests
    {
        Mock<IAnalyticsWrapper> m_MockAnalyticsWrapper;
        
        [SetUp]
        public void SetUp()
        {
            m_MockAnalyticsWrapper = new Mock<IAnalyticsWrapper>();
            m_MockAnalyticsWrapper
                .Setup(aw => aw.Register(It.IsAny<string>(), It.IsAny<int>()))
                .Verifiable();
            m_MockAnalyticsWrapper
                .Setup(aw => aw.Send(
                    It.IsAny<string>(), 
                    It.IsAny<object>(), 
                    It.IsAny<int>()))
                .Verifiable();
        }

        [Test]
        public void EventsRegistered()
        {
            var configAnalytics = new ConfigAnalytics(m_MockAnalyticsWrapper.Object, new JsonConverter(), new FileSystem());
            
            m_MockAnalyticsWrapper.Verify(
                aw => aw.Register(ConfigAnalytics.EventNameCreate, It.IsAny<int>()),
                Times.Once());
            m_MockAnalyticsWrapper.Verify(
                aw => aw.Register(ConfigAnalytics.EventNameDeployed, It.IsAny<int>()),
                Times.Once());
            m_MockAnalyticsWrapper.Verify(
                aw => aw.Register(ConfigAnalytics.EventNameDeployedGroup, It.IsAny<int>()),
                Times.Once());
        }
        
        [TestCase(0, 0, 0, 0)]
        [TestCase(1, 0, 0, 0)]
        [TestCase(1, 1, 1, 1)]
        [TestCase(5, 3, 3, 1)]
        [TestCase(5, 5, 5, 1)]
        public void Deployed_HasRightCount(
            int nbTotalFiles,
            int nbSuccessfulFiles,
            int expectedDeployCount,
            int expectedGroupCount)
        {
            var configAnalytics = new ConfigAnalytics(m_MockAnalyticsWrapper.Object, new JsonConverter(), new FileSystem());

            var validFiles = new List<IRemoteConfigFile>();
            for (var i = 0; i < nbSuccessfulFiles; i++)
            {
                validFiles.Add(TestFileGetter.GetTestFile(AssetFilePaths.ValidBase, new RemoteConfigParser(new ConfigTypeDeriver())));
            }
            configAnalytics.SendDeployedEvent(nbTotalFiles, validFiles);
            
            m_MockAnalyticsWrapper.Verify(
                aw => aw.Send(
                    ConfigAnalytics.EventNameDeployed, It.IsAny<object>(), It.IsAny<int>()),
                Times.Exactly(expectedDeployCount));
            m_MockAnalyticsWrapper.Verify(
                aw => aw.Send(
                    ConfigAnalytics.EventNameDeployedGroup, It.IsAny<object>(), It.IsAny<int>()),
                Times.Exactly(expectedGroupCount));
        }
    }
}
#endif
