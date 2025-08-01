// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using FluentAssertions;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Test.Validators;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.AuRa;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Transactions;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Specs.Forks;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using Nethermind.TxPool;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.AuRa.Test
{
    public class AuraBlockProcessorTests
    {
        [Test]
        public void Prepared_block_contains_author_field()
        {
            BranchProcessor processor = CreateProcessor().Processor;

            BlockHeader header = Build.A.BlockHeader.WithAuthor(TestItem.AddressD).TestObject;
            Block block = Build.A.Block.WithHeader(header).TestObject;
            Block[] processedBlocks = processor.Process(
                null,
                new List<Block> { block },
                ProcessingOptions.None,
                NullBlockTracer.Instance);
            Assert.That(processedBlocks.Length, Is.EqualTo(1), "length");
            Assert.That(processedBlocks[0].Author, Is.EqualTo(block.Author), "author");
        }

        [Test]
        public void For_not_empty_block_tx_filter_should_be_called()
        {
            ITxFilter txFilter = Substitute.For<ITxFilter>();
            txFilter
                .IsAllowed(Arg.Any<Transaction>(), Arg.Any<BlockHeader>(), Arg.Any<IReleaseSpec>())
                .Returns(AcceptTxResult.Accepted);
            BranchProcessor processor = CreateProcessor(txFilter).Processor;

            BlockHeader header = Build.A.BlockHeader.WithAuthor(TestItem.AddressD).WithNumber(3).TestObject;
            Transaction tx = Nethermind.Core.Test.Builders.Build.A.Transaction.WithData(new byte[] { 0, 1 })
                .SignedAndResolved().WithChainId(105).WithGasPrice(0).WithValue(0).TestObject;
            Block block = Build.A.Block.WithHeader(header).WithTransactions(new Transaction[] { tx }).TestObject;
            _ = processor.Process(
                null,
                new List<Block> { block },
                ProcessingOptions.None,
                NullBlockTracer.Instance);
            txFilter.Received().IsAllowed(Arg.Any<Transaction>(), Arg.Any<BlockHeader>(), Arg.Any<IReleaseSpec>());
        }

        [Test]
        public void For_normal_processing_it_should_not_fail_with_gas_remaining_rules()
        {
            BranchProcessor processor = CreateProcessor().Processor;
            int gasLimit = 10000000;
            BlockHeader header = Build.A.BlockHeader.WithAuthor(TestItem.AddressD).WithNumber(3).TestObject;
            Transaction tx = Nethermind.Core.Test.Builders.Build.A.Transaction.WithData(new byte[] { 0, 1 })
                .SignedAndResolved().WithChainId(105).WithGasPrice(0).WithValue(0).WithGasLimit(gasLimit + 1).TestObject;
            Block block = Build.A.Block.WithHeader(header).WithTransactions(new Transaction[] { tx })
                .WithGasLimit(gasLimit).TestObject;
            Assert.DoesNotThrow(() => processor.Process(
                null,
                new List<Block> { block },
                ProcessingOptions.None,
                NullBlockTracer.Instance));
        }

        [Test]
        public void Should_rewrite_contracts()
        {
            static BlockHeader Process(BranchProcessor auRaBlockProcessor, BlockHeader parent)
            {
                BlockHeader header = Build.A.BlockHeader.WithAuthor(TestItem.AddressD).WithParent(parent).TestObject;
                Block block = Build.A.Block.WithHeader(header).TestObject;
                return auRaBlockProcessor.Process(
                    parent,
                    new List<Block> { block },
                    ProcessingOptions.None,
                    NullBlockTracer.Instance)[0].Header;
            }

            Dictionary<long, IDictionary<Address, byte[]>> contractOverrides = new()
            {
                {
                    2,
                    new Dictionary<Address, byte[]>()
                    {
                        {TestItem.AddressA, Bytes.FromHexString("0x123")},
                        {TestItem.AddressB, Bytes.FromHexString("0x321")},
                    }
                },
                {
                    3,
                    new Dictionary<Address, byte[]>()
                    {
                        {TestItem.AddressA, Bytes.FromHexString("0x456")},
                        {TestItem.AddressB, Bytes.FromHexString("0x654")},
                    }
                },
            };

            (BranchProcessor processor, IWorldState stateProvider) =
                CreateProcessor(contractRewriter: new ContractRewriter(contractOverrides));

            stateProvider.CreateAccount(TestItem.AddressA, UInt256.One);
            stateProvider.CreateAccount(TestItem.AddressB, UInt256.One);
            stateProvider.Commit(London.Instance);
            stateProvider.CommitTree(0);
            stateProvider.RecalculateStateRoot();

            BlockHeader currentBlock = Build.A.BlockHeader.WithNumber(0).WithStateRoot(stateProvider.StateRoot).TestObject;
            currentBlock = Process(processor, currentBlock);
            stateProvider.GetCode(TestItem.AddressA).Should().BeEquivalentTo(Array.Empty<byte>());
            stateProvider.GetCode(TestItem.AddressB).Should().BeEquivalentTo(Array.Empty<byte>());

            currentBlock = Process(processor, currentBlock);
            stateProvider.GetCode(TestItem.AddressA).Should().BeEquivalentTo(Bytes.FromHexString("0x123"));
            stateProvider.GetCode(TestItem.AddressB).Should().BeEquivalentTo(Bytes.FromHexString("0x321"));

            _ = Process(processor, currentBlock);
            stateProvider.GetCode(TestItem.AddressA).Should().BeEquivalentTo(Bytes.FromHexString("0x456"));
            stateProvider.GetCode(TestItem.AddressB).Should().BeEquivalentTo(Bytes.FromHexString("0x654"));
        }

        private (BranchProcessor Processor, IWorldState StateProvider) CreateProcessor(ITxFilter? txFilter = null, ContractRewriter? contractRewriter = null)
        {
            IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
            IWorldState stateProvider = worldStateManager.GlobalWorldState;
            ITransactionProcessor transactionProcessor = Substitute.For<ITransactionProcessor>();
            AuRaBlockProcessor processor = new AuRaBlockProcessor(
                HoleskySpecProvider.Instance,
                TestBlockValidator.AlwaysValid,
                NoBlockRewards.Instance,
                new BlockProcessor.BlockValidationTransactionsExecutor(new ExecuteTransactionProcessorAdapter(transactionProcessor), stateProvider),
                stateProvider,
                NullReceiptStorage.Instance,
                new BeaconBlockRootHandler(transactionProcessor, stateProvider),
                LimboLogs.Instance,
                Substitute.For<IBlockTree>(),
                new WithdrawalProcessor(stateProvider, LimboLogs.Instance),
                new ExecutionRequestsProcessor(transactionProcessor),
                auRaValidator: null,
                txFilter,
                contractRewriter: contractRewriter);

            BranchProcessor branchProcessor = new BranchProcessor(
                processor,
                HoleskySpecProvider.Instance,
                stateProvider,
                new BeaconBlockRootHandler(transactionProcessor, stateProvider),
                LimboLogs.Instance);

            return (branchProcessor, stateProvider);
        }
    }
}
