// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Spec;
using Nethermind.Consensus.Comparers;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Core.Test.Db;
using Nethermind.Crypto;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Evm.State;
using Nethermind.State;
using Nethermind.TxPool;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test;

public class ReorgTests
{
#pragma warning disable NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
    private BlockchainProcessor _blockchainProcessor = null!;
#pragma warning restore NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
    private BlockTree _blockTree = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        ISpecProvider specProvider = MainnetSpecProvider.Instance;
        IDbProvider memDbProvider = TestMemDbProvider.Init();
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest(memDbProvider, LimboLogs.Instance);
        IWorldState stateProvider = worldStateManager.GlobalWorldState;

        IReleaseSpec finalSpec = specProvider.GetFinalSpec();

        if (finalSpec.WithdrawalsEnabled)
        {
            stateProvider.CreateAccount(Eip7002Constants.WithdrawalRequestPredeployAddress, 0, Eip7002TestConstants.Nonce);
            stateProvider.InsertCode(Eip7002Constants.WithdrawalRequestPredeployAddress, Eip7002TestConstants.CodeHash, Eip7002TestConstants.Code, specProvider.GenesisSpec);
        }

        if (finalSpec.ConsolidationRequestsEnabled)
        {
            stateProvider.CreateAccount(Eip7251Constants.ConsolidationRequestPredeployAddress, 0, Eip7251TestConstants.Nonce);
            stateProvider.InsertCode(Eip7251Constants.ConsolidationRequestPredeployAddress, Eip7251TestConstants.CodeHash, Eip7251TestConstants.Code, specProvider.GenesisSpec);
        }

        stateProvider.Commit(specProvider.GenesisSpec);
        stateProvider.CommitTree(0);

        IStateReader stateReader = worldStateManager.GlobalStateReader;
        EthereumEcdsa ecdsa = new(1);
        ITransactionComparerProvider transactionComparerProvider =
            new TransactionComparerProvider(specProvider, _blockTree);

        _blockTree = Build.A.BlockTree()
            .WithoutSettingHead
            .WithSpecProvider(specProvider)
            .TestObject;

        EthereumCodeInfoRepository codeInfoRepository = new();
        TxPool.TxPool txPool = new(
            ecdsa,
            new BlobTxStorage(),
            new ChainHeadInfoProvider(
                new ChainHeadSpecProvider(specProvider, _blockTree), _blockTree, worldStateManager.GlobalStateReader, codeInfoRepository),
            new TxPoolConfig(),
            new TxValidator(specProvider.ChainId),
            LimboLogs.Instance,
            transactionComparerProvider.GetDefaultComparer());
        BlockhashProvider blockhashProvider = new(_blockTree, specProvider, stateProvider, LimboLogs.Instance);
        VirtualMachine virtualMachine = new(
            blockhashProvider,
            specProvider,
            LimboLogs.Instance);
        TransactionProcessor transactionProcessor = new(
            specProvider,
            stateProvider,
            virtualMachine,
            codeInfoRepository,
            LimboLogs.Instance);

        BlockProcessor blockProcessor = new BlockProcessor(
            MainnetSpecProvider.Instance,
            Always.Valid,
            new RewardCalculator(specProvider),
            new BlockProcessor.BlockValidationTransactionsExecutor(new ExecuteTransactionProcessorAdapter(transactionProcessor), stateProvider),
            stateProvider,
            NullReceiptStorage.Instance,
            new BeaconBlockRootHandler(transactionProcessor, stateProvider),
            new BlockhashStore(MainnetSpecProvider.Instance, stateProvider),
            LimboLogs.Instance,
            new WithdrawalProcessor(stateProvider, LimboLogs.Instance),
            new ExecutionRequestsProcessor(transactionProcessor));
        BranchProcessor branchProcessor = new BranchProcessor(
            blockProcessor,
            MainnetSpecProvider.Instance,
            stateProvider,
            new BeaconBlockRootHandler(transactionProcessor, stateProvider),
            LimboLogs.Instance);

        _blockchainProcessor = new BlockchainProcessor(
            _blockTree,
            branchProcessor,
            new RecoverSignatures(
                ecdsa,
                specProvider,
                LimboLogs.Instance),
            stateReader,
            LimboLogs.Instance, BlockchainProcessor.Options.Default);
    }

    [OneTimeTearDown]
    public async Task TearDownAsync() => await (_blockchainProcessor?.DisposeAsync() ?? default);

    [Test, MaxTime(Timeout.MaxTestTime)]
    [Retry(3)]
    public void Test()
    {
        List<Block> events = new();

        Block block0 = Build.A.Block.Genesis.WithDifficulty(1).WithTotalDifficulty(1L).TestObject;
        Block block1 = Build.A.Block.WithParent(block0).WithDifficulty(2).WithTotalDifficulty(2L).TestObject;
        Block block2 = Build.A.Block.WithParent(block1).WithDifficulty(1).WithTotalDifficulty(3L).TestObject;
        Block block3 = Build.A.Block.WithParent(block2).WithDifficulty(3).WithTotalDifficulty(6L).TestObject;
        Block block1B = Build.A.Block.WithParent(block0).WithDifficulty(4).WithTotalDifficulty(5L).TestObject;
        Block block2B = Build.A.Block.WithParent(block1B).WithDifficulty(6).WithTotalDifficulty(11L).TestObject;

        _blockTree.BlockAddedToMain += (_, args) =>
        {
            events.Add(args.Block);
        };

        _blockchainProcessor.Start();

        _blockTree.SuggestBlock(block0);
        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);
        _blockTree.SuggestBlock(block3);
        _blockTree.SuggestBlock(block1B);
        _blockTree.SuggestBlock(block2B);

        Assert.That(() => _blockTree.Head, Is.EqualTo(block2B).After(10000, 500));

        events.Should().HaveCount(6);
        events[4].Hash.Should().Be(block1B.Hash!);
        events[5].Hash.Should().Be(block2B.Hash!);
    }
}
