// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Blockchain.Filters
{
    public class BlockFilter(int id, long startBlockNumber) : FilterBase(id)
    {
        public long StartBlockNumber { get; set; } = startBlockNumber;
    }
}
