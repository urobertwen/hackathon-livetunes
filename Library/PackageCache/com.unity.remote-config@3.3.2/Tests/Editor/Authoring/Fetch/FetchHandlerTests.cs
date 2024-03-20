#if NUGET_MOQ_AVAILABLE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Services.RemoteConfig.Authoring.Editor.Fetch;
using Unity.Services.RemoteConfig.Authoring.Editor.Formatting;
using Unity.Services.RemoteConfig.Authoring.Editor.Json;
using Unity.Services.RemoteConfig.Authoring.Editor.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.ErrorHandling;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Fetch;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Formatting;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.IO;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Networking;
using Unity.Services.RemoteConfig.Tests.Editor.Authoring.Shared;
using UnityEngine.TestTools;

namespace Unity.Services.RemoteConfig.Tests.Editor.Authoring.Fetch
{
    public class FetchHandlerTests
    {
        const string k_DefaultFileName = "fetched_keys.rc";

        string m_DefaultFilePath = Path.Combine(".", k_DefaultFileName);
        
        Mock<IRemoteConfigClient> m_MockClient;
        Mock<IFileSystem> m_MockFileSystem;
        RemoteConfigValidator m_RemoteConfigValidator;
        Dictionary<string, string> m_FileWrittenContents = new Dictionary<string, string>();
        JsonConverter m_Converter;
        RemoteConfigFetchHandler m_RemoteConfigFetchHandler;
        List<RemoteConfigEntry> m_RemoteRemoteConfigs;
        Dictionary<string, object> m_DefaultFileContents;

        [SetUp]
        public void SetUp()
        {
            m_RemoteConfigValidator = new RemoteConfigValidator();
            m_MockClient = new Mock<IRemoteConfigClient>();

            m_MockFileSystem = new Mock<IFileSystem>();
            m_MockFileSystem.Setup(f => f.WriteAllText(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((path, content, t) =>
                {
                    m_FileWrittenContents[path] = content;
                })
                .Returns(Task.CompletedTask);

            m_MockFileSystem.Setup(f => f.Delete(It.IsAny<string>()))
                .Returns(Task.FromResult(true));
            
            m_MockFileSystem.Setup(f => f.ReadAllText(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult("{ entries: {} }"));

            m_RemoteRemoteConfigs = new List<RemoteConfigEntry>
            {
                { "one", "a_str" },
                { "two", 123 },
                { "five", JObject.Parse("{'a':'123'}") }
            };
            
            m_DefaultFileContents = new Dictionary<string, object> {
                { "one", "old_str" },
                { "three", 789 }
            };

            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(() => Task.FromResult(new GetConfigsResult(true, m_RemoteRemoteConfigs)));

            var mockParser = new Mock<IRemoteConfigParser>();
            mockParser.Setup(m => m.ParseFile(
                    It.IsAny<RemoteConfigFileContent>(),
                    It.IsAny<IRemoteConfigFile>()))
                .Returns<RemoteConfigFileContent, IRemoteConfigFile>(
                    (rcfc, rcf) => rcf.Entries);

            m_Converter = new JsonConverter();

            m_RemoteConfigFetchHandler = new EditorRemoteConfigFetchHandler(
                m_MockClient.Object,
                m_MockFileSystem.Object,
                m_Converter,
                m_RemoteConfigValidator,
                mockParser.Object
            );
        }


        [UnityTest]
        public IEnumerator Test_FetchIntoEmptyLocal() => AsyncTest.AsCoroutine(Test_FetchIntoEmptyLocalAsync);

        async Task Test_FetchIntoEmptyLocalAsync()
        {
            var res = await m_RemoteConfigFetchHandler.FetchAsync(".", new IRemoteConfigFile[] { });

            m_MockFileSystem.Verify(
                f => f.WriteAllText(m_DefaultFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never());

            Assert.AreEqual(0, res.Created.Count);
            Assert.AreEqual(0, res.Updated.Count);
            Assert.AreEqual(0, res.Deleted.Count);
        }
        
        [UnityTest]
        public IEnumerator Test_FetchReconcileIntoEmptyLocal() =>
            AsyncTest.AsCoroutine(Test_FetchReconcileIntoEmptyLocalAsync);
        async Task Test_FetchReconcileIntoEmptyLocalAsync()
        {
            var res = await m_RemoteConfigFetchHandler.FetchAsync(
                ".", 
                new IRemoteConfigFile[] { }, 
                reconcile: true);

            m_MockFileSystem.Verify(
                f => f.WriteAllText(m_DefaultFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once());

            Assert.AreEqual(3, res.Created.Count);
            Assert.AreEqual(0, res.Updated.Count);
            Assert.AreEqual(0, res.Deleted.Count);

            var expectedObj = new RemoteConfigFileContent();
            foreach (var entry in m_RemoteRemoteConfigs)
            {
                expectedObj.UpdateEntry(entry);
            }

            var expectedStr = m_Converter.SerializeObject(expectedObj);
            
            Assert.AreEqual(expectedStr, m_FileWrittenContents[m_DefaultFilePath]);
            m_MockFileSystem.Verify(f => f.WriteAllText(m_DefaultFilePath, expectedStr, It.IsAny<CancellationToken>()), Times.Once());
        }
        
        [UnityTest]
        public IEnumerator Test_FetchIntoOneFile() =>
            AsyncTest.AsCoroutine(Test_FetchIntoOneFileAsync);
        async Task Test_FetchIntoOneFileAsync()
        {
            var fileName = "file_one.rc";
            var rcFile = CreateMockRcFile(fileName);

            var res = await m_RemoteConfigFetchHandler.FetchAsync(
                ".", 
                new IRemoteConfigFile[] { rcFile }, 
                reconcile: false);

            m_MockFileSystem.Verify(
                f => f.WriteAllText(m_DefaultFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never());

            //remote: one, two and five
            //local: one and three
            Assert.AreEqual(0, res.Created.Count, "Nothing should be created without reconcile");
            
            Assert.AreEqual(1, res.Updated.Count, "One key should be updated");
            Assert.AreEqual("one", res.Updated[0].Key, "key 'one' should be updated");
            Assert.AreEqual(fileName, res.Updated[0].File.Name);
            
            Assert.AreEqual(1, res.Deleted.Count, "One 'key' should be deleted");
            Assert.AreEqual("three", res.Deleted[0].Key, "key 'three' should be deleted");
            Assert.AreEqual(fileName, res.Deleted[0].File.Name, "key 'three' should be deleted");

            var expected = new RemoteConfigFileContent();
            expected.UpdateEntry("one", "a_str");
            var expectedStr = m_Converter.SerializeObject(expected);
            
            m_MockFileSystem.Verify(
                f => f.WriteAllText(fileName, expectedStr, It.IsAny<CancellationToken>()),
                Times.Once());
        }

        [UnityTest]
        public IEnumerator Test_FetchReconcileIntoOneFile() =>
            AsyncTest.AsCoroutine(Test_FetchReconcileIntoOneFileAsync);
        async Task Test_FetchReconcileIntoOneFileAsync()
        {
            var fileName = "file_one.rc";
            var rcFile = CreateMockRcFile(fileName);

            var res = await m_RemoteConfigFetchHandler.FetchAsync(
                ".", 
                new IRemoteConfigFile[] { rcFile }, 
                reconcile: true);

            m_MockFileSystem.Verify(
                f => f.WriteAllText(m_DefaultFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once());

            //remote: one, two and five
            //local: one and three
            Assert.AreEqual(2, res.Created.Count, "Two keys should be created ");
            var keys = res.Created.Select(e => e.Key).ToList();
            Assert.Contains( "two", keys, "key 'two' should be created");
            Assert.Contains( "five", keys, "key 'five' should be created");
            Assert.AreEqual(k_DefaultFileName, res.Created[0].File.Name);
            
            Assert.AreEqual(1, res.Updated.Count, "One key should be updated");
            Assert.AreEqual("one", res.Updated[0].Key, "key 'one' should be updated");
            Assert.AreEqual(fileName, res.Updated[0].File.Name);
            
            Assert.AreEqual(1, res.Deleted.Count, "One 'key' should be deleted");
            Assert.AreEqual("three", res.Deleted[0].Key, "key 'three' should be deleted");
            Assert.AreEqual(fileName, res.Deleted[0].File.Name, "key 'three' should be deleted");
            
            //existing file
            var expected = new RemoteConfigFileContent();
            expected.UpdateEntry("one", "a_str");
            var expectedStr = m_Converter.SerializeObject(expected);
            
            m_MockFileSystem.Verify(
                f => f.WriteAllText(fileName, expectedStr, It.IsAny<CancellationToken>()),
                Times.Once());
            
            //new file
            expected = new RemoteConfigFileContent();
            expected.UpdateEntry("two", 123L);
            expected.UpdateEntry("five", JObject.Parse("{'a':'123'}"));
            expectedStr = m_Converter.SerializeObject(expected);
        }
        
        [UnityTest]
        public IEnumerator Test_FetchIntoTwoFiles() =>
            AsyncTest.AsCoroutine(Test_FetchIntoTwoFilesAsync);
        async Task Test_FetchIntoTwoFilesAsync()
        {
            var fileName = "file_one.rc";
            var rcFile = CreateMockRcFile(fileName);
            
            var fileName2 = "file_two.rc";
            var rcFile2 = CreateMockRcFile(
                fileName2, 
                new Dictionary<string, object>
                {
                    { "five", "123"}
                },
                new Dictionary<string, ConfigType>
                {
                    { "five", ConfigType.STRING}
                });

            var res = await m_RemoteConfigFetchHandler.FetchAsync(
                ".", 
                new IRemoteConfigFile[] { rcFile, rcFile2 });

            //remote: one, two and five
            //local: one and three  | five
            Assert.AreEqual(0, res.Created.Count, "Zero keys should be created ");

            Assert.AreEqual(2, res.Updated.Count, "Two keys should be updated");
            Assert.AreEqual("one", res.Updated[0].Key, "key 'one' should be updated");
            Assert.AreEqual("five", res.Updated[1].Key, "key 'five' should be updated");
            Assert.AreEqual(fileName, res.Updated[0].File.Name);
            Assert.AreEqual(fileName2, res.Updated[1].File.Name);

            //new file
            m_MockFileSystem.Verify(
                f => f.WriteAllText(m_DefaultFilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never());
            
            //existing file
            var expected = new RemoteConfigFileContent();
            expected.UpdateEntry("one", "a_str");
            var expectedStr = m_Converter.SerializeObject(expected);
            
            m_MockFileSystem.Verify(
                f => f.WriteAllText(fileName, expectedStr, It.IsAny<CancellationToken>()),
                Times.Once());
            
            //existing file2
            var expected2 = new RemoteConfigFileContent();
            expected2.UpdateEntry("five", JObject.Parse("{'a':'123'}"));
            var expectedStr2 = m_Converter.SerializeObject(expected2);
            
            m_MockFileSystem.Verify(
                f => f.WriteAllText(fileName2, expectedStr2, It.IsAny<CancellationToken>()),
                Times.Once());
            
            //new file
            expected = new RemoteConfigFileContent();
            expected.UpdateEntry("two", 123L);
            expectedStr = m_Converter.SerializeObject(expected);
            
        }
        
        [UnityTest]
        public IEnumerator Test_FetchIntoDuplicateKeys() =>
            AsyncTest.AsCoroutine(Test_FetchIntoDuplicateKeysAsync);
        async Task Test_FetchIntoDuplicateKeysAsync()
        {
            var fileName = "file_one.rc";
            var rcFile = CreateMockRcFile(fileName);
            
            var fileName2 = "file_two.rc";
            var rcFile2 = CreateMockRcFile(fileName2);

            Assert.ThrowsAsync<DuplicateKeysInMultipleFilesException>(async () => await m_RemoteConfigFetchHandler.FetchAsync(
                ".", 
                new IRemoteConfigFile[] { rcFile, rcFile2 }));
        }

        [UnityTest]
        public IEnumerator Test_FetchReconcileTwice() =>
            AsyncTest.AsCoroutine(Test_FetchReconcileTwiceAsync);
        async Task Test_FetchReconcileTwiceAsync()
        {
            var res = await m_RemoteConfigFetchHandler.FetchAsync(
                ".", 
                new IRemoteConfigFile[] { }, 
                reconcile: true);

            var expectedObj = new RemoteConfigFileContent();
            foreach (var entry in m_RemoteRemoteConfigs)
            {
                expectedObj.UpdateEntry(entry);
            }
            
            m_RemoteRemoteConfigs.Add(new RemoteConfigEntry()
            {
                Key = "newKey",
                Value = "newVal"
            });

            var createdFile = new RemoteConfigFile()
            {
                Path = m_DefaultFilePath,
            };
            
            createdFile.Entries = expectedObj.ToRemoteConfigEntries(createdFile, new RemoteConfigParser(new ConfigTypeDeriver()));

            res = await m_RemoteConfigFetchHandler.FetchAsync(
                ".", 
                new IRemoteConfigFile[] { createdFile }, 
                reconcile: true);
            
            Assert.AreEqual(1, res.Created.Count);
            Assert.AreEqual(3, res.Updated.Count);
            Assert.AreEqual(0, res.Deleted.Count);

            foreach (var entry in m_RemoteRemoteConfigs)
            {
                expectedObj.UpdateEntry(entry);
            }

            var expectedStr = m_Converter.SerializeObject(expectedObj);
            
            Assert.AreEqual(expectedStr, m_FileWrittenContents[m_DefaultFilePath]);
        }
        
        [UnityTest]
        public IEnumerator Test_FetchReconcileEmptyEnvironment_DoesNotCreateDefault() =>
            AsyncTest.AsCoroutine(Test_FetchReconcileEmptyEnvironment_DoesNotCreateDefaultAsync);
        async Task Test_FetchReconcileEmptyEnvironment_DoesNotCreateDefaultAsync()
        {
            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(() => Task.FromResult(new GetConfigsResult(true, new RemoteConfigEntry[0])));

            var res = await m_RemoteConfigFetchHandler.FetchAsync(
                ".", 
                new IRemoteConfigFile[] { }, 
                reconcile: true);
            
            Assert.AreEqual(0, res.Created.Count);
            Assert.AreEqual(0, res.Updated.Count);
            Assert.AreEqual(0, res.Deleted.Count);
            Assert.AreEqual(0, res.Fetched.Count);
            Assert.AreEqual(0, res.Failed.Count);
            m_MockFileSystem.Verify(f => f.WriteAllText(
                It.IsAny<string>(),
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>()), Times.Never());
        }
        
        [UnityTest]
        public IEnumerator Test_FetchReconcileEmptyEnvironment_Deletes() =>
            AsyncTest.AsCoroutine(Test_FetchReconcileEmptyEnvironment_DeletesAsync);
        async Task Test_FetchReconcileEmptyEnvironment_DeletesAsync()
        {
            m_MockClient
                .Setup(c => c.GetAsync())
                .Returns(() => Task.FromResult(new GetConfigsResult(true, Array.Empty<RemoteConfigEntry>())));

            var fileName = "file_one.rc";
            var rcFile = CreateMockRcFile(fileName);
            
            var res = await m_RemoteConfigFetchHandler.FetchAsync(
                ".", 
                new IRemoteConfigFile[] { rcFile }, 
                reconcile: true);
            
            Assert.AreEqual(0, res.Created.Count, "Created count");
            Assert.AreEqual(0, res.Updated.Count, "Updated count");
            Assert.AreEqual(2, res.Deleted.Count, "Deleted count");
            Assert.AreEqual(1, res.Fetched.Count, "Fetched count");
            Assert.AreEqual(0, res.Failed.Count, "Failed count");
            m_MockFileSystem.Verify(f => f.Delete(
                fileName), Times.Once());
        }

        RemoteConfigFile CreateMockRcFile(
            string name, 
            Dictionary<string, object> entries = null, 
            Dictionary<string, ConfigType> types = null)
        {
            entries = entries ?? m_DefaultFileContents;

            types = types ?? new Dictionary<string, ConfigType>();
            
            var rcFile = new RemoteConfigFile
            {
                Name = name,
                Path = name,
                Entries = new List<RemoteConfigEntry>()
            };

            rcFile.Entries = new RemoteConfigFileContent()
            {
                entries = entries,
                types = types
            }.ToRemoteConfigEntries(rcFile, new RemoteConfigParser(new ConfigTypeDeriver()));
            
            return rcFile;
        }
    }

    static class TestExtensions
    {
        public static void Add(
            this List<RemoteConfigEntry> self,
            string key,
            object obj)
        {
            self.Add(new RemoteConfigEntry
            {
                Key = key,
                Value =  obj
            });
        }
        
        public static void UpdateEntry(
            this RemoteConfigFileContent self,
            string key,
            object obj,
            string type = null)
        {
            if (type == null)
            {
                if (obj is string)
                    type = "STRING";
                else if (obj is Int64)
                    type = "LONG";
                else if (obj is Int32)
                    type = "INT";
                else if (obj is JObject)
                    type = "JSON";
            }
            
            
            self.UpdateEntry(new RemoteConfigEntry
            {
                Key = key,
                Value =  obj
            });
        }
    }
}
#endif
