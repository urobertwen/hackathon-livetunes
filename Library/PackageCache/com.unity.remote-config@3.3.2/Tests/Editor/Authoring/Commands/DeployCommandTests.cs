#if NUGET_MOQ_AVAILABLE
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Unity.Services.DeploymentApi.Editor;
using Unity.Services.RemoteConfig.Authoring.Editor.Deployment;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Deployment;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Results;
using UnityEngine.TestTools;
using static Unity.Services.RemoteConfig.Tests.Editor.Authoring.Shared.AsyncTest;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Commands
{
    class DeployCommandTests
    {
        [UnityTest]
        public IEnumerator OnlyOneConcurrentDeploy() => AsCoroutine(OnlyOneConcurrentDeployAsync);

        static async Task OnlyOneConcurrentDeployAsync()
        {
            var mockDeploymentHandler = new Mock<IRemoteConfigDeploymentHandler>();
            mockDeploymentHandler
                .Setup(dh => dh.DeployAsync(
                    It.IsAny<IReadOnlyList<IRemoteConfigFile>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()))
                .Returns(async () =>
                {
                    await Task.Delay(1);
                    return new DeployResult(null, null, null);
                });
            var deployCommand = new DeployCommand(mockDeploymentHandler.Object);

            
            var deploymentItems = new List<IDeploymentItem>();
            var task1 = deployCommand.ExecuteAsync(deploymentItems);
            var task2 = deployCommand.ExecuteAsync(deploymentItems);
            await Task.WhenAll(task1, task2);

            mockDeploymentHandler
                .Verify(
                    x => x.DeployAsync(
                        It.IsAny<IReadOnlyList<IRemoteConfigFile>>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()),
                    Times.Once());

            await deployCommand.ExecuteAsync(deploymentItems);
            
            mockDeploymentHandler
                .Verify(
                    x => x.DeployAsync(
                        It.IsAny<IReadOnlyList<IRemoteConfigFile>>(), 
                        It.IsAny<bool>(), 
                        It.IsAny<bool>()),
                    Times.Exactly(2));
        }

    }
}
#endif
