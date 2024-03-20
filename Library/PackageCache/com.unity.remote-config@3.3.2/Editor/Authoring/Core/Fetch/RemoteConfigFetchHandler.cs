﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.ErrorHandling;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.IO;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Json;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Networking;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Results;

namespace Unity.Services.RemoteConfig.Editor.Authoring.Core.Fetch
{
    abstract class RemoteConfigFetchHandler : IRemoteConfigFetchHandler
    {
        const string k_FetchResultName = "fetched_keys.rc";
        readonly IRemoteConfigClient m_Client;
        readonly IFileSystem m_FileSystem;
        readonly IJsonConverter m_JsonConverter;
        readonly IRemoteConfigValidator m_RemoteConfigValidator;
        readonly IRemoteConfigParser m_RemoteConfigParser;

        public RemoteConfigFetchHandler(
            IRemoteConfigClient client,
            IFileSystem fileSystem,
            IJsonConverter jsonConverter,
            IRemoteConfigValidator remoteConfigValidator,
            IRemoteConfigParser remoteConfigParser)
        {
            m_Client = client;
            m_FileSystem = fileSystem;
            m_JsonConverter = jsonConverter;
            m_RemoteConfigValidator = remoteConfigValidator;
            m_RemoteConfigParser = remoteConfigParser;
        }
        
        public async Task<FetchResult> FetchAsync(
            string rootDirectory,
            IReadOnlyList<IRemoteConfigFile> files,
            bool dryRun = false,
            bool reconcile = false,
            CancellationToken token = default)
        {
            var serverRemoteConfigResult = await m_Client.GetAsync();

            var fetchExceptions = new List<RemoteConfigDeploymentException>();
            
            var remote = serverRemoteConfigResult.ConfigsExists 
                ? serverRemoteConfigResult.Configs 
                : Array.Empty<RemoteConfigEntry>();

            await DeserializeFiles(files);
            
            var allLocal = GetLocalRemoteConfigEntries(files);
            var validLocal = m_RemoteConfigValidator.FilterValidEntries(files, allLocal, fetchExceptions);

            if(fetchExceptions.Count > 0)
            {
                foreach (var exception in fetchExceptions)
                {
                    foreach (var entry in exception.AffectedFiles.SelectMany(file => file.Entries))
                    {
                        throw new DuplicateKeysInMultipleFilesException(entry.Key, exception.AffectedFiles);
                    }
                }
            }
            
            var toUpdate = FindEntriesToUpdate(remote, validLocal);
            var toDelete = FindEntriesToDelete(remote, validLocal);
            var toCreate = FindEntriesToCreate(remote, validLocal, reconcile);
            var toFetch = files;
            
            if (dryRun)
            {
                return new FetchResult(toCreate, toUpdate, toDelete, toFetch);
            }

            UpdateLocal(remote, toUpdate);
            DeleteLocal(files, toDelete);

            var filesToWrite = GetFilesToWrite(toUpdate, toDelete);

            await WriteOrDeleteFiles(filesToWrite);
            
            if (reconcile)
            {
                await CreateOrUpdateDefaultFile(rootDirectory, filesToWrite, toCreate);
            }

            return new FetchResult(toCreate, toUpdate, toDelete, toFetch);
        }

        async Task<IReadOnlyList<RemoteConfigFileContent>> DeserializeFiles(IReadOnlyList<IRemoteConfigFile> configFiles)
        {
            var result = await Task.WhenAll(configFiles.Select(SetContent));
        
            return result;
        }
        
        async Task<RemoteConfigFileContent> SetContent(IRemoteConfigFile configFile)
        {
            var text = await m_FileSystem.ReadAllText(configFile.Path);
            var content = m_JsonConverter.DeserializeObject<RemoteConfigFileContent>(text, true);
            configFile.Entries = content.ToRemoteConfigEntries(configFile, m_RemoteConfigParser);
        
            return content;
        }

        protected abstract IRemoteConfigFile ConstructRemoteConfigFile(
            string path);
        
        async Task CreateOrUpdateDefaultFile(string rootDirectory,
            IReadOnlyList<IRemoteConfigFile> filesToWrite,
            IReadOnlyList<RemoteConfigEntry> toCreate)
        {
            var defaultFile = filesToWrite.FirstOrDefault(f => f.Name == k_FetchResultName);
            if (defaultFile == null)
            {
                await CreateNewRemoteConfigFile(rootDirectory, toCreate);
            }
            else
            {
                defaultFile.UpdateOrCreateEntries(toCreate);

                await WriteOrDeleteFiles(new[] { defaultFile });
            }
        }

        static IReadOnlyList<RemoteConfigEntry> FindEntriesToUpdate(
            IReadOnlyList<RemoteConfigEntry> remote,
            IReadOnlyList<RemoteConfigEntry> local)
        {
            var toUpdate = local
                .Where(l => remote.Any(r => r.Key == l.Key))
                .ToList();

            return toUpdate;
        }

        static IReadOnlyList<RemoteConfigEntry> FindEntriesToDelete(
            IReadOnlyList<RemoteConfigEntry> remote,
            IReadOnlyList<RemoteConfigEntry> local)
        {
            var toDelete = local
                .Where(l => remote.All(r => r.Key != l.Key))
                .ToList();

            return toDelete;
        }

        static IReadOnlyList<RemoteConfigEntry> FindEntriesToCreate(
            IReadOnlyList<RemoteConfigEntry> remote,
            IReadOnlyList<RemoteConfigEntry> local,
            bool reconcile)
        {
            if (!reconcile)
                return new List<RemoteConfigEntry>();
            
            var localSet = local.Select(l => l.Key).ToHashSet();
            return remote
                .Where(k => !localSet.Contains(k.Key))
                .ToList();
        }

        static void UpdateLocal(
            IReadOnlyList<RemoteConfigEntry> remote,
            IReadOnlyList<RemoteConfigEntry> toUpdate)
        {
            foreach (var entry in toUpdate)
            {
                entry.File.UpdateEntries(remote);
            }
        }
        
        static void DeleteLocal(
            IReadOnlyList<IRemoteConfigFile> files,
            IReadOnlyList<RemoteConfigEntry> toDelete)
        {
            foreach (var file in files.Where(file=> file.Entries != null))
            {
                file.RemoveEntries(toDelete);
            }
        }

        static IReadOnlyList<IRemoteConfigFile> GetFilesToWrite(
            IReadOnlyList<RemoteConfigEntry> toUpdate,
            IReadOnlyList<RemoteConfigEntry> toDelete)
        {
            return toUpdate
                .Concat(toDelete)
                .Select(entry=> entry.File)
                .Distinct()
                .ToList();
        }

        async Task WriteOrDeleteFiles(
            IReadOnlyList<IRemoteConfigFile> files)
        {
            var tasks = new List<Task>(files.Count);

            foreach (var file in files)
            {
                if (file.Entries.Any())
                {
                    var content = new RemoteConfigFileContent(file.Entries);

                    var text = m_JsonConverter.SerializeObject(content);

                    tasks.Add(m_FileSystem.WriteAllText(file.Path, text));
                }
                else
                {
                    tasks.Add(m_FileSystem.Delete(file.Path));
                }
            }

            await Task.WhenAll(tasks);
        }

        async Task CreateNewRemoteConfigFile(
            string rootDirectory,
            IReadOnlyList<RemoteConfigEntry> toCreate)
        {
            if (toCreate.Count == 0)
            {
                return;
            }
            
            var filePath = Path.Combine(rootDirectory, k_FetchResultName);
            var file = ConstructRemoteConfigFile(filePath);

            foreach (var entry in toCreate)
            {
                entry.File = file;
            }
            
            var content = new RemoteConfigFileContent(toCreate);
            
            var serializeObject = m_JsonConverter.SerializeObject(content);
            
            await m_FileSystem.WriteAllText(filePath, serializeObject);
        }

        static List<RemoteConfigEntry> GetLocalRemoteConfigEntries(IReadOnlyList<IRemoteConfigFile> files)
        {
            return files
                .Where(file => file.Entries != null)
                .SelectMany(file => file.Entries)
                .ToList();
        }
    }
}