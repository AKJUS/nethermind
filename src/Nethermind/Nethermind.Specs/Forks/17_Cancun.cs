// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;
using Nethermind.Core;
using Nethermind.Core.Specs;

namespace Nethermind.Specs.Forks;

public class Cancun : Shanghai
{
    private static IReleaseSpec _instance;

    protected Cancun()
    {
        Name = "Cancun";
        IsEip1153Enabled = true;
        IsEip4788Enabled = true;
        IsEip4844Enabled = true;
        IsEip5656Enabled = true;
        IsEip6780Enabled = true;
        Eip4788ContractAddress = Eip4788Constants.BeaconRootsAddress;
        MaxBlobCount = 6;
        TargetBlobCount = 3;
        BlobBaseFeeUpdateFraction = 3338477;
    }

    public new static IReleaseSpec Instance => LazyInitializer.EnsureInitialized(ref _instance, static () => new Cancun());
}
