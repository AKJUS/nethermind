// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using Autofac.Features.AttributeFilters;
using Nethermind.Abi;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Data;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
using Nethermind.Consensus.AuRa.Config;
using Nethermind.Consensus.AuRa.Contracts;
using Nethermind.Consensus.AuRa.Contracts.DataStore;
using Nethermind.Consensus.AuRa.Transactions;
using Nethermind.Consensus.AuRa.Validators;
using Nethermind.Consensus.Comparers;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Transactions;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.JsonRpc.Modules.Eth.GasPrice;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.TxPool;

namespace Nethermind.Consensus.AuRa.InitializationSteps;

// Note: Not a step!! Its a factory of some kind!
// Note: Stateful!! Can't use a singleton!
public class StartBlockProducerAuRa(
    ISpecProvider specProvider,
    ChainSpec chainSpec,
    IBlocksConfig blocksConfig,
    IAuraConfig auraConfig,
    IBlockProcessingQueue blockProcessingQueue,
    IBlockTree blockTree,
    ISealer sealer,
    ITimestamper timestamper,
    IReportingValidator reportingValidator,
    IReceiptStorage receiptStorage,
    IValidatorStore validatorStore,
    IAuRaBlockFinalizationManager auRaBlockFinalizationManager,
    ISigner engineSigner,
    IGasPriceOracle gasPriceOracle,
    ReportingContractBasedValidator.Cache reportingContractValidatorCache,
    IDisposableStack disposeStack,
    IAbiEncoder abiEncoder,
    IReadOnlyTxProcessingEnvFactory readOnlyTxProcessingEnvFactory,
    TxAuRaFilterBuilders apiTxAuRaFilterBuilders,
    AuraStatefulComponents auraStatefulComponents,
    ITxPool txPool,
    IStateReader apiStateReader,
    ITransactionComparerProvider transactionComparerProvider,
    [KeyFilter(IProtectedPrivateKey.NodeKey)] IProtectedPrivateKey protectedPrivateKey,
    ICryptoRandom cryptoRandom,
    IBlockValidator blockValidator,
    IRewardCalculatorSource rewardCalculatorSource,
    IAuRaStepCalculator stepCalculator,
    AuRaGasLimitOverrideFactory gasLimitOverrideFactory,
    IWorldStateManager worldStateManager,
    ILifetimeScope lifetimeScope,
    ILogManager logManager)
{
    private readonly AuRaChainSpecEngineParameters _parameters = chainSpec.EngineChainSpecParametersProvider
        .GetChainSpecParameters<AuRaChainSpecEngineParameters>();

    private BlockProducerEnv? _blockProducerContext;

    private IAuRaValidator? _validator;
    private DictionaryContractDataStore<TxPriorityContract.Destination>? _minGasPricesContractDataStore;
    private TxPriorityContract? _txPriorityContract;
    private TxPriorityContract.LocalDataSource? _localDataSource;

    public IBlockProductionTrigger CreateTrigger()
    {
        BuildBlocksOnAuRaSteps onAuRaSteps = new(stepCalculator, logManager);
        BuildBlocksOnlyWhenNotProcessing onlyWhenNotProcessing = new(
            onAuRaSteps,
            blockProcessingQueue,
            blockTree,
            logManager,
            !auraConfig.AllowAuRaPrivateChains);

        disposeStack.Push((IAsyncDisposable)onlyWhenNotProcessing);

        return onlyWhenNotProcessing;
    }

    public IBlockProducer BuildProducer()
    {
        ILogger logger = logManager.GetClassLogger();
        if (logger.IsInfo) logger.Info("Starting AuRa block producer & sealer");

        BlockProducerEnv producerEnv = GetProducerChain();

        IGasLimitCalculator gasLimitCalculator = CreateGasLimitCalculator();

        IBlockProducer blockProducer = new AuRaBlockProducer(
            producerEnv.TxSource,
            producerEnv.ChainProcessor,
            producerEnv.ReadOnlyStateProvider,
            sealer,
            blockTree,
            timestamper,
            stepCalculator,
            reportingValidator,
            auraConfig,
            gasLimitCalculator,
            specProvider,
            logManager,
            blocksConfig);

        return blockProducer;
    }

    private BlockProcessor CreateBlockProcessor(ITransactionProcessor txProcessor, IWorldState worldState)
    {
        ITxFilter auRaTxFilter = apiTxAuRaFilterBuilders.CreateAuRaTxFilter(
            new LocalTxFilter(engineSigner));

        _validator = new AuRaValidatorFactory(abiEncoder,
                worldState,
                txProcessor,
                blockTree,
                readOnlyTxProcessingEnvFactory.Create(),
                receiptStorage,
                validatorStore,
                auRaBlockFinalizationManager,
                NullTxSender.Instance,
                NullTxPool.Instance,
                blocksConfig,
                logManager,
                engineSigner,
                specProvider,
                gasPriceOracle,
                reportingContractValidatorCache,
                _parameters.PosdaoTransition,
                true)
            .CreateValidatorProcessor(_parameters.Validators, blockTree.Head?.Header);

        if (_validator is IDisposable disposableValidator)
        {
            disposeStack.Push(disposableValidator);
        }

        IDictionary<long, IDictionary<Address, byte[]>> rewriteBytecode = _parameters.RewriteBytecode;
        ContractRewriter? contractRewriter = rewriteBytecode?.Count > 0 ? new ContractRewriter(rewriteBytecode) : null;

        var transactionExecutor = new BlockProcessor.BlockProductionTransactionsExecutor(
            new BuildUpTransactionProcessorAdapter(txProcessor),
            worldState,
            new BlockProcessor.BlockProductionTransactionPicker(specProvider, blocksConfig.BlockProductionMaxTxKilobytes),
            logManager);

        return new AuRaBlockProcessor(
            specProvider,
            blockValidator,
            rewardCalculatorSource.Get(txProcessor),
            transactionExecutor,
            worldState,
            receiptStorage,
            new BeaconBlockRootHandler(txProcessor, worldState),
            logManager,
            blockTree,
            NullWithdrawalProcessor.Instance,
            new ExecutionRequestsProcessor(txProcessor),
            _validator,
            auRaTxFilter,
            CreateGasLimitCalculator() as AuRaContractGasLimitOverride,
            contractRewriter);
    }

    internal TxPoolTxSource CreateTxPoolTxSource()
    {
        _txPriorityContract = apiTxAuRaFilterBuilders.CreateTxPrioritySources();
        _localDataSource = auraStatefulComponents.TxPriorityContractLocalDataSource;

        if (_txPriorityContract is not null || _localDataSource is not null)
        {
            _minGasPricesContractDataStore = apiTxAuRaFilterBuilders.CreateMinGasPricesDataStore(_txPriorityContract, _localDataSource)!;
            disposeStack.Push(_minGasPricesContractDataStore);

            ContractDataStore<Address> whitelistContractDataStore = new ContractDataStoreWithLocalData<Address>(
                new HashSetContractDataStoreCollection<Address>(),
                _txPriorityContract?.SendersWhitelist,
                blockTree,
                receiptStorage,
                logManager,
                _localDataSource?.GetWhitelistLocalDataSource() ?? new EmptyLocalDataSource<IEnumerable<Address>>());

            DictionaryContractDataStore<TxPriorityContract.Destination> prioritiesContractDataStore =
                new DictionaryContractDataStore<TxPriorityContract.Destination>(
                    new TxPriorityContract.DestinationSortedListContractDataStoreCollection(),
                    _txPriorityContract?.Priorities,
                    blockTree,
                    receiptStorage,
                    logManager,
                    _localDataSource?.GetPrioritiesLocalDataSource());

            disposeStack.Push(whitelistContractDataStore);
            disposeStack.Push(prioritiesContractDataStore);

            ITxFilter auraTxFilter =
                apiTxAuRaFilterBuilders.CreateAuRaTxFilterForProducer(_minGasPricesContractDataStore);
            ITxFilterPipeline txFilterPipeline = new TxFilterPipelineBuilder(logManager)
                .WithCustomTxFilter(auraTxFilter)
                .WithBaseFeeFilter()
                .WithNullTxFilter()
                .Build;


            return new TxPriorityTxSource(
                txPool,
                logManager,
                txFilterPipeline,
                whitelistContractDataStore,
                prioritiesContractDataStore,
                specProvider,
                transactionComparerProvider,
                blocksConfig);
        }
        else
        {
            return CreateStandardTxPoolTxSource();
        }
    }

    private TxPoolTxSource CreateStandardTxPoolTxSource()
    {
        ITxFilter txSourceFilter = apiTxAuRaFilterBuilders.CreateAuRaTxFilterForProducer(_minGasPricesContractDataStore);
        ITxFilterPipeline txFilterPipeline = new TxFilterPipelineBuilder(logManager)
            .WithCustomTxFilter(txSourceFilter)
            .WithBaseFeeFilter()
            .WithHeadTxFilter()
            .Build;

        return new TxPoolTxSource(txPool, specProvider, transactionComparerProvider, logManager, txFilterPipeline, blocksConfig);
    }


    // TODO: Use BlockProducerEnvFactory
    private BlockProducerEnv GetProducerChain()
    {
        BlockProducerEnv Create()
        {
            ReadOnlyBlockTree readOnlyBlockTree = blockTree.AsReadOnly();

            IWorldState worldState = worldStateManager.CreateResettableWorldState();
            ILifetimeScope innerLifetime = lifetimeScope.BeginLifetimeScope((builder) => builder
                .AddSingleton<IWorldState>(worldState)
                .AddSingleton<BlockchainProcessor.Options>(BlockchainProcessor.Options.NoReceipts)
                .AddSingleton<IBlockProcessor, ITransactionProcessor, IWorldState>(CreateBlockProcessor)
                .AddDecorator<IBlockchainProcessor, OneTimeChainProcessor>());
            lifetimeScope.Disposer.AddInstanceForAsyncDisposal(innerLifetime);

            IBlockchainProcessor chainProcessor = innerLifetime.Resolve<IBlockchainProcessor>();

            return new BlockProducerEnv(readOnlyBlockTree, chainProcessor, worldState, CreateTxSourceForProducer());
        }

        return _blockProducerContext ??= Create();
    }

    private ITxSource CreateStandardTxSourceForProducer() => CreateTxPoolTxSource();

    private ITxSource CreateTxSourceForProducer()
    {
        bool CheckAddPosdaoTransactions(IList<ITxSource> list, long auRaPosdaoTransition)
        {
            if (auRaPosdaoTransition != AuRaChainSpecEngineParameters.TransitionDisabled && _validator is ITxSource validatorSource)
            {
                list.Insert(0, validatorSource);
                return true;
            }

            return false;
        }

        bool CheckAddRandomnessTransactions(IList<ITxSource> list, IDictionary<long, Address>? randomnessContractAddress, ISigner signer)
        {
            IList<IRandomContract> GetRandomContracts(
                IDictionary<long, Address> randomnessContractAddressPerBlock,
                IAbiEncoder abiEncoder,
                IReadOnlyTxProcessorSource txProcessorSource,
                ISigner signerLocal) =>
                randomnessContractAddressPerBlock
                    .Select(kvp => new RandomContract(
                        abiEncoder,
                        kvp.Value,
                        txProcessorSource,
                        kvp.Key,
                        signerLocal))
                    .ToArray<IRandomContract>();

            if (randomnessContractAddress?.Any() == true)
            {
                RandomContractTxSource randomContractTxSource = new RandomContractTxSource(
                    GetRandomContracts(randomnessContractAddress, abiEncoder,
                        readOnlyTxProcessingEnvFactory.Create(),
                        signer),
                    new EciesCipher(cryptoRandom),
                    signer,
                    protectedPrivateKey,
                    cryptoRandom,
                    logManager);

                list.Insert(0, randomContractTxSource);
                return true;
            }

            return false;
        }

        IList<ITxSource> txSources = new List<ITxSource> { CreateStandardTxSourceForProducer() };
        bool needSigner = false;

        needSigner |= CheckAddPosdaoTransactions(txSources, _parameters.PosdaoTransition);
        needSigner |= CheckAddRandomnessTransactions(txSources, _parameters.RandomnessContractAddress, engineSigner);

        ITxSource txSource = txSources.Count > 1 ? new CompositeTxSource(txSources.ToArray()) : txSources[0];

        if (needSigner)
        {
            TxSealer transactionSealer = new TxSealer(engineSigner, timestamper);
            txSource = new GeneratedTxSource(txSource, transactionSealer, apiStateReader, logManager);
        }

        ITxFilter? txPermissionFilter = apiTxAuRaFilterBuilders.CreateTxPermissionFilter();
        if (txPermissionFilter is not null)
        {
            // we now only need to filter generated transactions here, as regular ones are filtered on TxPoolTxSource filter based on CreateTxSourceFilter method
            txSource = new FilteredTxSource<GeneratedTransaction>(txSource, txPermissionFilter, logManager, specProvider, blocksConfig);
        }

        return txSource;
    }

    private IGasLimitCalculator CreateGasLimitCalculator()
    {
        AuRaContractGasLimitOverride? auRaContractGasLimitOverride = gasLimitOverrideFactory.GetGasLimitCalculator();
        if (auRaContractGasLimitOverride is not null) return auRaContractGasLimitOverride;
        return new TargetAdjustedGasLimitCalculator(specProvider, blocksConfig);
    }
}
