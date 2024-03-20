#if NUGET_MOQ_AVAILABLE
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.IO;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring
{
    static class MockFileSystemExtensions
    {
        public static void SetupFileSystemText(this Mock<IFileSystem> mockFileSystem, string data)
        {
            mockFileSystem
                .Setup(fr => fr.ReadAllText(It.IsAny<string>(), CancellationToken.None))
                .Returns(Task.FromResult(data));
        }
    }
}
#endif
