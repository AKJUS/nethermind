// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Core;

public static class Eip7918Constants
{
    // floor cost in execution gas 2^13 = 8_192
    public const int BlobBaseCost = 1 << 13;
}
