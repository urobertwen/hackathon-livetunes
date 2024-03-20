#if NUGET_MOQ_AVAILABLE
using System.Collections;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Services.RemoteConfig.Authoring.Editor.Networking;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Deployment;
using Unity.Services.RemoteConfig.Tests.Editor.Authoring.Shared;
using UnityEngine.TestTools;


namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Networking
{
    class RemoteConfigClientTests
    {
        RemoteConfigClient m_RemoteConfigClient;
        Mock<IWebApiClient> m_MockWebApi;

        [SetUp]
        public void SetUp()
        {
            var deploymentInfo = new Mock<IDeploymentInfo>();
            deploymentInfo.SetupGet(d => d.EnvironmentId).Returns("env");
            deploymentInfo.SetupGet(d => d.CloudProjectId).Returns("proj");
            
            m_MockWebApi = new Mock<IWebApiClient>();
            m_RemoteConfigClient = new RemoteConfigClient(m_MockWebApi.Object, deploymentInfo.Object);
        }

        [UnityTest]
        public IEnumerator ReturnsNotFoundOnNoResponse() => AsyncTest.AsCoroutine(ReturnsNotFoundOnNoResponseAsync);

        async Task ReturnsNotFoundOnNoResponseAsync()
        {
            m_MockWebApi
                .Setup(m => m.Fetch(It.IsAny<string>(), It.IsAny<string>()))
                .Returns( Task.FromResult( new FetchResult(null)));

            var get = await m_RemoteConfigClient.GetAsync();
            Assert.IsFalse(get.ConfigsExists);
        }
        
        [UnityTest]
        public IEnumerator ReturnsFoundOnClientExists() => AsyncTest.AsCoroutine(ReturnsFoundOnClientExistsAsync);

        async Task ReturnsFoundOnClientExistsAsync()
        {
            string configStr =
            @"{
                'projectId':'9d167bf0-55b9-45c3-ae87-659becf0b121',
                'environmentId':'976379af-013e-4f9e-8a17-391562a1c801',
                'type':'settings',
                'value':[
                    {'key':'key','type':'bool','value':true}
                ],
                'id':'01880824-1a49-475a-9411-bbe0f42d2861',
                'version':'82ecd14c-27df-41be-8f53-a6c1528a44be',
                'createdAt':'2022-10-26T17:57:32Z',
                'updatedAt':'2023-04-20T18:14:22Z'
            }";
            
            var configJObject = JsonConvert.DeserializeObject<JObject>(configStr);

            var fetchResult = new FetchResult(configJObject);
            
            m_MockWebApi
                .Setup(m => m.Fetch(It.IsAny<string>(), It.IsAny<string>()))
                .Returns( Task.FromResult( fetchResult));

            var get = await m_RemoteConfigClient.GetAsync();
            Assert.IsTrue(get.ConfigsExists);
        }
        
        [UnityTest]
        public IEnumerator ReturnsDoubleIfDoubleAsync() => AsyncTest.AsCoroutine(ReturnsDoubleIfDouble);

        async Task ReturnsDoubleIfDouble()
        {
            string configStr =
                @"{
                'projectId':'x',
                'environmentId':'x',
                'type':'settings',
                'value':[
                    {'key':'key','type':'float','value':1}
                ],
                'id':'x',
                'version':'x',
                'createdAt':'2022-10-26T17:57:32Z',
                'updatedAt':'2023-04-20T18:14:22Z'
            }";
            
            var configJObject = JsonConvert.DeserializeObject<JObject>(configStr);

            var fetchResult = new FetchResult(configJObject);
            
            m_MockWebApi
                .Setup(m => m.Fetch(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(fetchResult));

            var get = await m_RemoteConfigClient.GetAsync();
            Assert.IsTrue(get.ConfigsExists);
            Assert.AreEqual(typeof(double), get.Configs[0].Value.GetType());
        }
        
        [UnityTest]
        public IEnumerator ReturnsIntIfIntAsync() => AsyncTest.AsCoroutine(ReturnsIntIfInt);

        async Task ReturnsIntIfInt()
        {
            string configStr =
                @"{
                'projectId':'x',
                'environmentId':'x',
                'type':'settings',
                'value':[
                    {'key':'key','type':'int','value':1}
                ],
                'id':'x',
                'version':'x',
                'createdAt':'2022-10-26T17:57:32Z',
                'updatedAt':'2023-04-20T18:14:22Z'
            }";
            
            var configJObject = JsonConvert.DeserializeObject<JObject>(configStr);

            var fetchResult = new FetchResult(configJObject);
            
            m_MockWebApi
                .Setup(m => m.Fetch(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(fetchResult));

            var get = await m_RemoteConfigClient.GetAsync();
            Assert.IsTrue(get.ConfigsExists);
            Assert.AreEqual(typeof(System.Int32), get.Configs[0].Value.GetType());
        }
    }
}
#endif
