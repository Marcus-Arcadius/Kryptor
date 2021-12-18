﻿/*
    Kryptor: A simple, modern, and secure encryption tool.
    Copyright (C) 2020-2022 Samuel Lucas

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program. If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Text;
using Sodium;

namespace KryptorCLI;

public static class Blake2b
{
    private static readonly byte[] Personalisation = Encoding.UTF8.GetBytes(Constants.BLAKE2Personal);

    public static byte[] Hash(byte[] message)
    {
        return GenericHash.Hash(message, key: null, Constants.BLAKE2Length);
    }

    public static byte[] Hash(FileStream fileStream)
    {
        using var blake2 = new GenericHash.GenericHashAlgorithm(key: (byte[])null, Constants.BLAKE2Length);
        return blake2.ComputeHash(fileStream);
    }

    public static byte[] KeyedHash(byte[] message, byte[] key)
    {
        return GenericHash.Hash(message, key, Constants.BLAKE2Length);
    }

    public static byte[] KeyDerivation(byte[] inputKeyingMaterial, byte[] salt, int outputLength)
    {
        return GenericHash.HashSaltPersonal(message: Array.Empty<byte>(), inputKeyingMaterial, salt, Personalisation, outputLength);
    }
}