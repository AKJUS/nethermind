// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.JsonRpc.Modules
{
    public interface IRpcModuleFactory<out T> where T : IRpcModule
    {
        T Create();
    }
}
