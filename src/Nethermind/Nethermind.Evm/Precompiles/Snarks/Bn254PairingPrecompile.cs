// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Crypto;

namespace Nethermind.Evm.Precompiles.Snarks;

/// <summary>
/// https://github.com/herumi/mcl/blob/master/api.md
/// </summary>
public class Bn254PairingPrecompile : IPrecompile<Bn254PairingPrecompile>
{
    private const int Bn256PairingMaxInputSizeGranite = 112687;
    private const int PairSize = 192;

    public static Bn254PairingPrecompile Instance = new Bn254PairingPrecompile();

    public static Address Address { get; } = Address.FromNumber(8);

    public long BaseGasCost(IReleaseSpec releaseSpec) => releaseSpec.IsEip1108Enabled ? 45000L : 100000L;

    public long DataGasCost(ReadOnlyMemory<byte> inputData, IReleaseSpec releaseSpec) => (releaseSpec.IsEip1108Enabled ? 34000L : 80000L) * (inputData.Length / PairSize);

    public (ReadOnlyMemory<byte>, bool) Run(ReadOnlyMemory<byte> inputData, IReleaseSpec releaseSpec)
    {
        Metrics.Bn254PairingPrecompile++;
        if (releaseSpec.IsOpGraniteEnabled && inputData.Length > Bn256PairingMaxInputSizeGranite)
        {
            return IPrecompile.Failure;
        }
        if (inputData.Length % PairSize > 0)
        {
            // note that it will not happen in case of null / 0 length
            return IPrecompile.Failure;
        }

        /* we modify input in place here and this is save for EVM but not
               safe in benchmarks so we need to remember to clone */
        Span<byte> output = stackalloc byte[64];
        Span<byte> inputDataSpan = inputData.ToArray().AsSpan();
        Span<byte> inputReshuffled = stackalloc byte[PairSize];
        for (int i = 0; i < inputData.Length / PairSize; i++)
        {
            inputDataSpan.Slice(i * PairSize + 0, 64).CopyTo(inputReshuffled[..64]);
            inputDataSpan.Slice(i * PairSize + 64, 32).CopyTo(inputReshuffled.Slice(96, 32));
            inputDataSpan.Slice(i * PairSize + 96, 32).CopyTo(inputReshuffled.Slice(64, 32));
            inputDataSpan.Slice(i * PairSize + 128, 32).CopyTo(inputReshuffled.Slice(160, 32));
            inputDataSpan.Slice(i * PairSize + 160, 32).CopyTo(inputReshuffled.Slice(128, 32));
            inputReshuffled.CopyTo(inputDataSpan.Slice(i * PairSize, PairSize));
        }

        return Pairings.Bn254Pairing(inputDataSpan, output) ? (output[..32].ToArray(), true) : IPrecompile.Failure;
    }
}
