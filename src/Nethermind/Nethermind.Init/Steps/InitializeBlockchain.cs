// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Nethermind.Api;
using Nethermind.Api.Steps;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Spec;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Comparers;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Processing.CensorshipDetector;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Scheduler;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Attributes;
using Nethermind.Core.ServiceStopper;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.State;
using Nethermind.TxPool;
using Nethermind.Wallet;

namespace Nethermind.Init.Steps
{
    [RunnerStepDependencies(
        typeof(InitializePlugins),
        typeof(InitializeBlockTree),
        typeof(SetupKeyStore),
        typeof(InitializePrecompiles)
    )]
    public class InitializeBlockchain(INethermindApi api) : IStep
    {
        private readonly INethermindApi _api = api;
        private readonly IServiceStopper _serviceStopper = api.Context.Resolve<IServiceStopper>();

        public async Task Execute(CancellationToken _)
        {
            await InitBlockchain();
        }

        [Todo(Improve.Refactor, "Use chain spec for all chain configuration")]
        protected virtual Task InitBlockchain()
        {
            (IApiWithStores getApi, IApiWithBlockchain setApi) = _api.ForBlockchain;
            setApi.TransactionComparerProvider = new TransactionComparerProvider(getApi.SpecProvider!, getApi.BlockTree!.AsReadOnly());

            IInitConfig initConfig = getApi.Config<IInitConfig>();
            IBlocksConfig blocksConfig = getApi.Config<IBlocksConfig>();
            IReceiptConfig receiptConfig = getApi.Config<IReceiptConfig>();

            ThisNodeInfo.AddInfo("Gaslimit     :", $"{blocksConfig.TargetBlockGasLimit:N0}");
            ThisNodeInfo.AddInfo("ExtraData    :", Utf8.IsValid(blocksConfig.GetExtraDataBytes()) ?
                blocksConfig.ExtraData :
                "- binary data -");

            IStateReader stateReader = setApi.StateReader!;
            IWorldState mainWorldState = _api.WorldStateManager!.GlobalWorldState;
            PreBlockCaches? preBlockCaches = (mainWorldState as IPreBlockCaches)?.Caches;
            EthereumCodeInfoRepository codeInfoRepository = new(preBlockCaches?.PrecompileCache);
            IChainHeadInfoProvider chainHeadInfoProvider =
                new ChainHeadInfoProvider(
                    new ChainHeadSpecProvider(getApi.SpecProvider!, getApi.BlockTree!),
                    getApi.BlockTree!, stateReader, codeInfoRepository);

            _api.TxGossipPolicy.Policies.Add(new SpecDrivenTxGossipPolicy(chainHeadInfoProvider));

            ITxPool txPool = _api.TxPool = CreateTxPool(chainHeadInfoProvider);

            _api.BlockPreprocessor.AddFirst(
                new RecoverSignatures(getApi.EthereumEcdsa, getApi.SpecProvider, getApi.LogManager));

            WarmupEvm();
            VirtualMachine virtualMachine = CreateVirtualMachine(mainWorldState);
            ITransactionProcessor transactionProcessor = CreateTransactionProcessor(codeInfoRepository, virtualMachine, mainWorldState);

            if (_api.SealValidator is null) throw new StepDependencyException(nameof(_api.SealValidator));

            // TODO: can take the tx sender from plugin here maybe
            ITxSigner txSigner = new WalletTxSigner(getApi.Wallet, getApi.SpecProvider!.ChainId);
            TxSealer nonceReservingTxSealer =
                new(txSigner, getApi.Timestamper);
            INonceManager nonceManager = new NonceManager(chainHeadInfoProvider.ReadOnlyStateProvider);
            setApi.NonceManager = nonceManager;
            setApi.TxSender = new TxPoolSender(txPool, nonceReservingTxSealer, nonceManager, getApi.EthereumEcdsa!);

            BlockCachePreWarmer? preWarmer = blocksConfig.PreWarmStateOnBlockProcessing
                ? new(
                    _api.ReadOnlyTxProcessingEnvFactory,
                    mainWorldState,
                    blocksConfig,
                    _api.LogManager,
                    preBlockCaches)
                : null;

            IBlockProcessor mainBlockProcessor = CreateBlockProcessor(preWarmer, transactionProcessor, mainWorldState);
            IBranchProcessor mainBranchProcessor = new BranchProcessor(
                mainBlockProcessor,
                _api.SpecProvider!,
                mainWorldState,
                new BeaconBlockRootHandler(transactionProcessor, mainWorldState),
                _api.LogManager!,
                preWarmer
            );

            BlockchainProcessor blockchainProcessor = new(
                getApi.BlockTree!,
                mainBranchProcessor,
                _api.BlockPreprocessor,
                stateReader,
                getApi.LogManager,
                new BlockchainProcessor.Options
                {
                    StoreReceiptsByDefault = receiptConfig.StoreReceipts,
                    DumpOptions = initConfig.AutoDump
                })
            {
                IsMainProcessor = true
            };

            getApi.DisposeStack.Push(blockchainProcessor);

            var mainProcessingContext = setApi.MainProcessingContext = new MainProcessingContext(
                transactionProcessor,
                mainBranchProcessor,
                mainBlockProcessor,
                blockchainProcessor,
                mainWorldState);
            _serviceStopper.AddStoppable(mainProcessingContext);
            setApi.BlockProcessingQueue = blockchainProcessor;
            setApi.BlockProductionPolicy = CreateBlockProductionPolicy();

            BackgroundTaskScheduler backgroundTaskScheduler = new BackgroundTaskScheduler(
                mainBranchProcessor,
                chainHeadInfoProvider,
                initConfig.BackgroundTaskConcurrency,
                initConfig.BackgroundTaskMaxNumber,
                _api.LogManager);
            setApi.BackgroundTaskScheduler = backgroundTaskScheduler;
            _api.DisposeStack.Push(backgroundTaskScheduler);

            ICensorshipDetectorConfig censorshipDetectorConfig = _api.Config<ICensorshipDetectorConfig>();
            if (censorshipDetectorConfig.Enabled)
            {
                CensorshipDetector censorshipDetector = new(
                    _api.BlockTree!,
                    txPool,
                    CreateTxPoolTxComparer(),
                    mainBranchProcessor,
                    _api.LogManager,
                    censorshipDetectorConfig
                );
                setApi.CensorshipDetector = censorshipDetector;
                _api.DisposeStack.Push(censorshipDetector);
            }

            return Task.CompletedTask;
        }

        private void WarmupEvm()
        {
            IWorldState state = _api.WorldStateManager!.CreateResettableWorldState();
            state.SetBaseBlock(null);
            VirtualMachine.WarmUpEvmInstructions(state, new EthereumCodeInfoRepository());
        }

        protected virtual ITransactionProcessor CreateTransactionProcessor(ICodeInfoRepository codeInfoRepository, IVirtualMachine virtualMachine, IWorldState worldState)
        {
            if (_api.SpecProvider is null) throw new StepDependencyException(nameof(_api.SpecProvider));

            return new TransactionProcessor(
                _api.SpecProvider,
                worldState,
                virtualMachine,
                codeInfoRepository,
                _api.LogManager);
        }

        protected VirtualMachine CreateVirtualMachine(IWorldState worldState)
        {
            if (_api.BlockTree is null) throw new StepDependencyException(nameof(_api.BlockTree));
            if (_api.SpecProvider is null) throw new StepDependencyException(nameof(_api.SpecProvider));
            if (_api.WorldStateManager is null) throw new StepDependencyException(nameof(_api.WorldStateManager));

            // blockchain processing
            BlockhashProvider blockhashProvider = new(
                _api.BlockTree, _api.SpecProvider, worldState, _api.LogManager);

            VirtualMachine virtualMachine = new(
                blockhashProvider,
                _api.SpecProvider,
                _api.LogManager);

            return virtualMachine;
        }

        protected virtual IBlockProductionPolicy CreateBlockProductionPolicy() =>
            new BlockProductionPolicy(_api.Config<IMiningConfig>());

        protected virtual ITxPool CreateTxPool(IChainHeadInfoProvider chainHeadInfoProvider)
        {
            TxPool.TxPool txPool = new(_api.EthereumEcdsa!,
                _api.BlobTxStorage ?? NullBlobTxStorage.Instance,
                chainHeadInfoProvider,
                _api.Config<ITxPoolConfig>(),
                _api.TxValidator!,
                _api.LogManager,
                CreateTxPoolTxComparer(),
                _api.TxGossipPolicy,
                null,
                _api.HeadTxValidator
            );

            _api.DisposeStack.Push(txPool);
            return txPool;
        }

        protected IComparer<Transaction> CreateTxPoolTxComparer() => _api.TransactionComparerProvider!.GetDefaultComparer();

        // TODO: remove from here - move to consensus?
        protected virtual IBlockProcessor CreateBlockProcessor(
            BlockCachePreWarmer? preWarmer,
            ITransactionProcessor transactionProcessor,
            IWorldState worldState)
        {
            if (_api.RewardCalculatorSource is null) throw new StepDependencyException(nameof(_api.RewardCalculatorSource));
            if (_api.BlockTree is null) throw new StepDependencyException(nameof(_api.BlockTree));
            if (_api.WorldStateManager is null) throw new StepDependencyException(nameof(_api.WorldStateManager));
            if (_api.SpecProvider is null) throw new StepDependencyException(nameof(_api.SpecProvider));

            return new BlockProcessor(_api.SpecProvider,
                _api.BlockValidator,
                _api.RewardCalculatorSource.Get(transactionProcessor),
                new BlockProcessor.BlockValidationTransactionsExecutor(new ExecuteTransactionProcessorAdapter(transactionProcessor), worldState),
                worldState,
                _api.ReceiptStorage!,
                new BeaconBlockRootHandler(transactionProcessor, worldState),
                new BlockhashStore(_api.SpecProvider!, worldState),
                _api.LogManager,
                new WithdrawalProcessor(worldState, _api.LogManager),
                new ExecutionRequestsProcessor(transactionProcessor));
        }
    }
}
