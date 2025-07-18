// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.IO;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Specs.Forks;
using Nethermind.Evm.State;
using Nethermind.State;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test;

[Parallelizable(ParallelScope.All)]
public class GenesisLoaderTests
{
    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Can_load_genesis_with_emtpy_accounts_and_storage()
    {
        AssertBlockHash("0x61b2253366eab37849d21ac066b96c9de133b8c58a9a38652deae1dd7ec22e7b", "Specs/empty_accounts_and_storages.json");
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Can_load_genesis_with_emtpy_accounts_and_code()
    {
        AssertBlockHash("0xfa3da895e1c2a4d2673f60dd885b867d60fb6d823abaf1e5276a899d7e2feca5", "Specs/empty_accounts_and_codes.json");
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Can_load_genesis_with_precompile_that_has_zero_balance()
    {
        AssertBlockHash("0x62839401df8970ec70785f62e9e9d559b256a9a10b343baf6c064747b094de09", "Specs/hive_zero_balance_test.json");
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Can_load_withdrawals_with_empty_root()
    {
        Block block = GetGenesisBlock("Specs/shanghai_from_genesis.json");
        Assert.That(block.Hash!.ToString(), Is.EqualTo("0x1326aad1114b1f1c6a345b69ba4ba6f8ab6ce027d988aacd275ab596a047a547"));
    }

    private void AssertBlockHash(string expectedHash, string chainspecFilePath)
    {
        Block block = GetGenesisBlock(chainspecFilePath);
        Assert.That(block.Hash!.ToString(), Is.EqualTo(expectedHash));
    }

    private Block GetGenesisBlock(string chainspecPath)
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, chainspecPath);
        ChainSpec chainSpec = LoadChainSpec(path);
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState stateProvider = worldStateManager.GlobalWorldState;
        ISpecProvider specProvider = Substitute.For<ISpecProvider>();
        specProvider.GetSpec(Arg.Any<ForkActivation>()).Returns(Berlin.Instance);
        ITransactionProcessor transactionProcessor = Substitute.For<ITransactionProcessor>();
        GenesisLoader genesisLoader = new(chainSpec, specProvider, worldStateManager.GlobalStateReader, stateProvider, transactionProcessor, LimboLogs.Instance);
        return genesisLoader.Load();
    }


    private static ChainSpec LoadChainSpec(string path)
    {
        var loader = new ChainSpecFileLoader(new EthereumJsonSerializer(), LimboTraceLogger.Instance);
        var chainSpec = loader.LoadEmbeddedOrFromFile(path);
        return chainSpec;
    }
}
