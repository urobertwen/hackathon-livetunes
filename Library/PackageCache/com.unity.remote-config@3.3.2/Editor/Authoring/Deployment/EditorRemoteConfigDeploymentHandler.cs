using System.Collections.Generic;
using Unity.Services.DeploymentApi.Editor;
using Unity.Services.RemoteConfig.Authoring.Editor.Analytics;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Formatting;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.IO;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Json;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Model;
using Unity.Services.RemoteConfig.Editor.Authoring.Core.Networking;
using StatusSeverityLevel = Unity.Services.DeploymentApi.Editor.SeverityLevel;
using StateSeverityLevel = Unity.Services.DeploymentApi.Editor.SeverityLevel; 
using RcStatusSeverityLevel = Unity.Services.RemoteConfig.Editor.Authoring.Core.Networking.StatusSeverityLevel;

namespace Unity.Services.RemoteConfig.Authoring.Editor.Deployment
{
    class EditorRemoteConfigDeploymentHandler : RemoteConfigDeploymentHandler
    {
        static readonly Dictionary<RcStatusSeverityLevel, StatusSeverityLevel> s_SeverityLevelConverter = new Dictionary<RcStatusSeverityLevel, StatusSeverityLevel>()
        {
            {RcStatusSeverityLevel.None, StatusSeverityLevel.None},
            {RcStatusSeverityLevel.Info, StatusSeverityLevel.Info},
            {RcStatusSeverityLevel.Success, StatusSeverityLevel.Success},
            {RcStatusSeverityLevel.Warning, StatusSeverityLevel.Warning},
            {RcStatusSeverityLevel.Error, StatusSeverityLevel.Error}
        };

        IConfigAnalytics m_ConfigAnalytics;

        public EditorRemoteConfigDeploymentHandler(IRemoteConfigClient remoteConfigClient,
            IRemoteConfigParser remoteConfigParser,
            IRemoteConfigValidator remoteConfigValidator,
            IFormatValidator formatValidator,
            IConfigMerger configMerger,
            IJsonConverter jsonConverter,
            IFileSystem fileSystem,
            IConfigAnalytics configAnalytics)
            : base(remoteConfigClient,
                remoteConfigParser,
                remoteConfigValidator,
                formatValidator,
                configMerger,
                jsonConverter,
                fileSystem)
        {
            m_ConfigAnalytics = configAnalytics;
        }

        protected override void UpdateStatus(
            IRemoteConfigFile remoteConfigFile,
            string status,
            string detail,
            RcStatusSeverityLevel severityLevel)
        {
            if (remoteConfigFile is DeploymentItem item)
            {
                switch (severityLevel)
                {
                    case RcStatusSeverityLevel.Success: 
                        item.Status = DeploymentStatus.UpToDate;
                        break;
                    case RcStatusSeverityLevel.Error:
                        item.Status = DeploymentStatus.FailedToDeploy;
                        break;
                    case RcStatusSeverityLevel.None:
                    case RcStatusSeverityLevel.Info:
                    case RcStatusSeverityLevel.Warning:
                    default:
                        item.Status = new DeploymentStatus(status, detail, s_SeverityLevelConverter[severityLevel]);
                        break;
                        
                };
                
                item.SetStatusDetail(detail);
            }
        }

        protected override void UpdateProgress(IRemoteConfigFile remoteConfigFile, float progress)
        {
            if (remoteConfigFile is DeploymentItem item)
            {
                item.Progress = progress;
            }
        }

        protected override void OnFilesDeployed(int totalFilesRequested, IReadOnlyList<IRemoteConfigFile> filesDeployed)
        {
            m_ConfigAnalytics.SendDeployedEvent(totalFilesRequested, filesDeployed);
        }
    }
}
