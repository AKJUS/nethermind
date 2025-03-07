// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Trie.Pruning;

public class KeepLastNPruningStrategy(IPruningStrategy baseStrategy, int depth) : IPruningStrategy
{
    public bool PruningEnabled => baseStrategy.PruningEnabled;

    public int MaxDepth => depth;

    public bool ShouldPrune(in long currentMemory) => baseStrategy.ShouldPrune(in currentMemory);

    public int TrackedPastKeyCount => baseStrategy.TrackedPastKeyCount;
}
