// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Autofac;
using Nethermind.Api;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Comparers;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Core.Container;
using Nethermind.Core.Test.Blockchain;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Evm.State;
using Nethermind.State;
using Nethermind.TxPool;

namespace Nethermind.Core.Test.Modules;

public class TestBlockProcessingModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder
            .AddSingleton<ITransactionComparerProvider, TransactionComparerProvider>()
            // NOTE: The ordering of block preprocessor is not guarenteed
            .AddComposite<IBlockPreprocessorStep, CompositeBlockPreprocessorStep>()
            .AddSingleton<CompositeBlockPreprocessorStep>()
            .AddSingleton<IBlockPreprocessorStep, RecoverSignatures>()

            // Yea, for some reason, the ICodeInfoRepository need to be the main one for ChainHeadInfoProvider to work.
            // Like, is ICodeInfoRepository suppose to be global? Why not just IStateReader.
            .AddKeyedSingleton<ICodeInfoRepository>(nameof(IWorldStateManager.GlobalWorldState), (ctx) =>
            {
                IWorldState worldState = ctx.Resolve<IWorldStateManager>().GlobalWorldState;
                PreBlockCaches? preBlockCaches = (worldState as IPreBlockCaches)?.Caches;
                return new EthereumCodeInfoRepository(preBlockCaches?.PrecompileCache);
            })

            .AddSingleton<ITxPool, TxPool.TxPool>()
            .AddSingleton<CompositeTxGossipPolicy>()
            .AddSingleton<INonceManager, IChainHeadInfoProvider>((chainHeadInfoProvider) => new NonceManager(chainHeadInfoProvider.ReadOnlyStateProvider))

            // The main block processing pipeline, anything that requires the use of the main IWorldState is wrapped
            // in a `IMainProcessingContext`.
            .AddSingleton<IMainProcessingContext, AutoMainProcessingContext>()
            // Then component that has no ambiguity is extracted back out.
            .Map<IBlockProcessingQueue, AutoMainProcessingContext>(ctx => (IBlockProcessingQueue)ctx.BlockchainProcessor)
            .Map<GenesisLoader, AutoMainProcessingContext>(ctx => ctx.GenesisLoader)
            .Bind<IMainProcessingContext, AutoMainProcessingContext>()

            // Seems to be only used by block producer.
            .AddScoped<IGasLimitCalculator, TargetAdjustedGasLimitCalculator>()
            .AddScoped<IComparer<Transaction>, ITransactionComparerProvider>(txComparer => txComparer.GetDefaultComparer())

            .AddSingleton<IBlockProductionPolicy, BlockProductionPolicy>()
            .AddSingleton<IBlockProducerFactory, AutoBlockProducerFactory<TestBlockProducer>>()
            .AddSingleton<IBlockProducer, IBlockProducerFactory>((factory) => factory.InitBlockProducer())

            // Something else entirely. Just some wrapper over things.
            .AddSingleton<IManualBlockProductionTrigger, BuildBlocksWhenRequested>()
            .Bind<IBlockProductionTrigger, IManualBlockProductionTrigger>()
            .AddSingleton<IBlockProducerRunner, StandardBlockProducerRunner>()
            .AddSingleton<ProducedBlockSuggester>()
            .ResolveOnServiceActivation<ProducedBlockSuggester, IBlockProducerRunner>()

            .AddSingleton<ISigner>(NullSigner.Instance)

            ;
    }

    public class AutoBlockProducerFactory<T>(ILifetimeScope rootLifetime, IBlockProducerEnvFactory producerEnvFactory) : IBlockProducerFactory where T : IBlockProducer
    {
        public IBlockProducer InitBlockProducer()
        {
            IBlockProducerEnv env = producerEnvFactory.Create();
            ILifetimeScope innerScope = rootLifetime.BeginLifetimeScope((builder) => builder
                // Block producer specific things is in `IBlockProducerEnvFactory`.
                // Yea, it can be added as `AddScoped` too and then mapped out, but its clearer this way.
                .AddScoped<IWorldState>(env.ReadOnlyStateProvider)
                .AddScoped<IBlockchainProcessor>(env.ChainProcessor)
                .AddScoped<ITxSource>(env.TxSource)

                .AddScoped<IBlockProducer, T>());

            return innerScope.Resolve<IBlockProducer>();
        }
    }
}
