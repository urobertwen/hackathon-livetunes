#if NUGET_MOQ_AVAILABLE
using System;
using System.Collections.Generic;
using Moq;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.ErrorHandling;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Formatting;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring
{
    static class MockFormatValidatorExtensions
    {
        public static void SetupFormatValidator(this Mock<IFormatValidator> mockFormatValidator, Action<ICollection<RemoteConfigDeploymentException>> callback)
        {
            mockFormatValidator.Setup(mv => mv.Validate(
                    It.IsAny<IRemoteConfigFile>(),
                    It.IsAny<RemoteConfigFileContent>(),
                    It.IsAny<ICollection<RemoteConfigDeploymentException>>()))
                .Returns(false)
                .Callback<IRemoteConfigFile, RemoteConfigFileContent, ICollection<RemoteConfigDeploymentException>>((_, _, exceptions) =>
                {
                    callback(exceptions);
                });
        }
    }
}
#endif
