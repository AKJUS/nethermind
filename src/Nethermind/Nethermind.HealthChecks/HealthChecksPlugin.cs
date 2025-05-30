// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.IO.Abstractions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.JsonRpc.Modules;
using Nethermind.Logging;
using Nethermind.JsonRpc;
using Nethermind.Monitoring.Config;
using Nethermind.Core.Extensions;
using Nethermind.Merge.Plugin;

namespace Nethermind.HealthChecks
{
    public class HealthChecksPlugin : INethermindPlugin, INethermindServicesPlugin
    {
        private INethermindApi _api;
        private IHealthChecksConfig _healthChecksConfig;
        private INodeHealthService _nodeHealthService;
        private ILogger _logger;
        private IJsonRpcConfig _jsonRpcConfig;
        private IInitConfig _initConfig;
        private IMergeConfig _mergeConfig;

        private ClHealthRequestsTracker _engineRequestsTracker;
        private FreeDiskSpaceChecker _freeDiskSpaceChecker;

        public async ValueTask DisposeAsync()
        {
            if (_engineRequestsTracker is not null)
            {
                await _engineRequestsTracker.DisposeAsync();
            }
            if (_freeDiskSpaceChecker is not null)
            {
                await FreeDiskSpaceChecker.DisposeAsync();
            }
        }

        public string Name => "HealthChecks";

        public string Description => "Endpoints that takes care of node`s health";

        public string Author => "Nethermind";

        public bool MustInitialize => true;
        public bool Enabled => true; // Always enabled

        public FreeDiskSpaceChecker FreeDiskSpaceChecker => LazyInitializer.EnsureInitialized(ref _freeDiskSpaceChecker,
            () => new FreeDiskSpaceChecker(
                _healthChecksConfig,
                _api.FileSystem.GetDriveInfos(_initConfig.BaseDbPath),
                _api.TimerFactory,
                _api.ProcessExit,
                _logger));

        public Task Init(INethermindApi api)
        {
            _api = api;
            _healthChecksConfig = _api.Config<IHealthChecksConfig>();
            _jsonRpcConfig = _api.Config<IJsonRpcConfig>();
            _initConfig = _api.Config<IInitConfig>();
            _mergeConfig = _api.Config<IMergeConfig>();
            _logger = api.LogManager.GetClassLogger();

            _engineRequestsTracker = _mergeConfig.Enabled ? new(_api.Timestamper, _healthChecksConfig.MaxIntervalClRequestTime, _logger) : null;
            _api.EngineRequestsTracker = _engineRequestsTracker;

            //will throw an exception and close app or block until enough disk space is available (LowStorageCheckAwaitOnStartup)
            EnsureEnoughFreeSpace();

            return Task.CompletedTask;
        }

        public void AddServices(IServiceCollection service)
        {
            service.AddHealthChecks()
                .AddTypeActivatedCheck<NodeHealthCheck>(
                    "node-health",
                    args: new object[] { _nodeHealthService, _api, _api.LogManager });
            if (_healthChecksConfig.UIEnabled)
            {
                if (!_healthChecksConfig.Enabled)
                {
                    if (_logger.IsWarn) _logger.Warn("To use HealthChecksUI please enable HealthChecks. (--HealthChecks.Enabled=true)");
                    return;
                }

                service.AddHealthChecksUI(setup =>
                {
                    setup.AddHealthCheckEndpoint("health", BuildEndpointForUi());
                    setup.SetEvaluationTimeInSeconds(_healthChecksConfig.PollingInterval);
                    setup.SetHeaderText("Nethermind Node Health");
                    if (_healthChecksConfig.WebhooksEnabled)
                    {
                        setup.AddWebhookNotification("webhook",
                            uri: _healthChecksConfig.WebhooksUri,
                            payload: _healthChecksConfig.WebhooksPayload,
                            restorePayload: _healthChecksConfig.WebhooksRestorePayload,
                            customDescriptionFunc: (livenessName, report) =>
                            {
                                string description = report.Entries["node-health"].Description;

                                IMetricsConfig metricsConfig = _api.Config<IMetricsConfig>();

                                string hostname = Dns.GetHostName();

                                HealthChecksWebhookInfo info = new(description, _api.IpResolver, metricsConfig, hostname);
                                return info.GetFullInfo();
                            }
                        );
                    }
                })
                .AddInMemoryStorage();
            }
        }

        public Task InitRpcModules()
        {
            IDriveInfo[] drives = [];

            if (_healthChecksConfig.LowStorageSpaceWarningThreshold > 0 || _healthChecksConfig.LowStorageSpaceShutdownThreshold > 0)
            {
                try
                {
                    drives = _api.FileSystem.GetDriveInfos(_initConfig.BaseDbPath);
                    FreeDiskSpaceChecker.StartAsync(default);
                }
                catch (Exception ex)
                {
                    if (_logger.IsError) _logger.Error("Failed to initialize available disk space check module", ex);
                }
            }

            _nodeHealthService = new NodeHealthService(_api.SyncServer,
                _api.MainProcessingContext!.BlockchainProcessor, _api.BlockProducerRunner!, _healthChecksConfig, _api.HealthHintService!,
                _api.EthSyncingInfo!, _engineRequestsTracker, _api.SpecProvider!.TerminalTotalDifficulty, drives, _initConfig.IsMining);

            if (_mergeConfig.Enabled)
            {
                _ = _engineRequestsTracker.StartAsync(); // Fire and forget
                _api.DisposeStack.Push(_engineRequestsTracker);
            }

            if (_healthChecksConfig.Enabled)
            {
                HealthRpcModule healthRpcModule = new(_nodeHealthService);
                _api.RpcModuleProvider!.Register(new SingletonModulePool<IHealthRpcModule>(healthRpcModule, true));
                if (_logger.IsInfo) _logger.Info("Health RPC Module has been enabled");
            }

            return Task.CompletedTask;
        }

        private string BuildEndpointForUi()
        {
            string host = _jsonRpcConfig.Host.Replace("0.0.0.0", "localhost");
            host = host.Replace("[::]", "localhost");
            return new UriBuilder("http", host, _jsonRpcConfig.Port, _healthChecksConfig.Slug).ToString();
        }

        private void EnsureEnoughFreeSpace()
        {
            if (_healthChecksConfig.LowStorageSpaceShutdownThreshold > 0)
            {
                FreeDiskSpaceChecker.EnsureEnoughFreeSpaceOnStart(_api.TimerFactory);
            }
        }
    }
}
