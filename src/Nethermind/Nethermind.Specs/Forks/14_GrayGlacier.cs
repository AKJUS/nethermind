// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;
using Nethermind.Core.Specs;

namespace Nethermind.Specs.Forks
{
    public class GrayGlacier : ArrowGlacier
    {
        private static IReleaseSpec _instance;

        protected GrayGlacier()
        {
            Name = "Gray Glacier";
            DifficultyBombDelay = 11400000L;
        }

        public new static IReleaseSpec Instance => LazyInitializer.EnsureInitialized(ref _instance, static () => new GrayGlacier());


    }
}
