// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Autofac;
using Nethermind.Config;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Specs.Forks;

namespace Nethermind.Core.Test.Modules;

/// <summary>
/// For when you don't care if it match prod or not. You just want something that build and will override some
/// component later anyway.
/// </summary>
/// <param name="configProvider"></param>
public class TestNethermindModule(IConfigProvider configProvider, ChainSpec chainSpec) : Module
{
    public TestNethermindModule() : this(new ConfigProvider())
    {
    }

    public TestNethermindModule(params IConfig[] configs) : this(new ConfigProvider(configs))
    {
    }

    public TestNethermindModule(IConfigProvider configProvider) : this(configProvider, new ChainSpec() { Parameters = new ChainParameters() })
    {
    }

    public TestNethermindModule(ChainSpec chainSpec) : this(new ConfigProvider(), chainSpec)
    {
    }

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        LongDisposeTracker.Configure(builder);

        builder
            .AddModule(new PseudoNethermindModule(chainSpec, configProvider, LimboLogs.Instance))
            .AddModule(new TestEnvironmentModule(TestItem.PrivateKeyA, Random.Shared.Next().ToString()))
            .AddSingleton<ISpecProvider>(new TestSpecProvider(Cancun.Instance));
    }
}
