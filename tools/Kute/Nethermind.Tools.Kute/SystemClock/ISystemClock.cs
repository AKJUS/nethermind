// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Tools.Kute.SystemClock;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
