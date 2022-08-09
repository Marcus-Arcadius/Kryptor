﻿/*
    Kryptor: A simple, modern, and secure encryption and signing tool.
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
using Geralt;
using Sodium;

namespace Kryptor;

public static class SymmetricKeyValidation
{
    public static byte[] GetEncryptionSymmetricKey(string symmetricKey)
    {
        try
        {
            if (string.IsNullOrEmpty(symmetricKey)) { return null; }
            if (Arrays.Compare(symmetricKey.ToCharArray(), new[] { ' ' }))
            {
                var key = SodiumCore.GetRandomBytes(Constants.EncryptionKeyLength);
                DisplayMessage.SymmetricKey(Utilities.BinaryToBase64(Arrays.Concat(Constants.SymmetricKeyHeader, key)));
                return key;
            }
            if (Arrays.Compare(new[] {symmetricKey[^1]}, Constants.Base64Padding)) { return KeyString(symmetricKey); }
            if (File.Exists(symmetricKey)) { return ReadKeyfile(symmetricKey); }
            if (Directory.Exists(symmetricKey)) { symmetricKey = Path.Combine(symmetricKey, SecureRandom.GetString(Constants.RandomFileNameLength)); }
            if (!symmetricKey.EndsWith(Constants.KeyfileExtension)) { symmetricKey += Constants.KeyfileExtension; }
            if (File.Exists(symmetricKey)) { return ReadKeyfile(symmetricKey); }
            var keyfileBytes = SodiumCore.GetRandomBytes(Constants.KeyfileLength);
            File.WriteAllBytes(symmetricKey, keyfileBytes);
            File.SetAttributes(symmetricKey, FileAttributes.ReadOnly);
            DisplayMessage.Keyfile(symmetricKey);
            return keyfileBytes;
        }
        catch (Exception ex) when (ExceptionFilters.FileAccess(ex))
        {
            DisplayMessage.FilePathException(symmetricKey, ex.GetType().Name, "Unable to randomly generate a keyfile.");
            return null;
        }
    }
    
    public static byte[] GetDecryptionSymmetricKey(string symmetricKey)
    {
        if (string.IsNullOrEmpty(symmetricKey)) { return null; }
        return Arrays.Compare(new[] {symmetricKey[^1]}, Constants.Base64Padding) ? KeyString(symmetricKey) : ReadKeyfile(symmetricKey);
    }

    private static byte[] KeyString(string encodedSymmetricKey)
    {
        try
        {
            if (encodedSymmetricKey.Length != Constants.SymmetricKeyLength) { throw new ArgumentException(ErrorMessages.InvalidSymmetricKey); }
            byte[] symmetricKey = Utilities.Base64ToBinary(encodedSymmetricKey, ignoredChars: null);
            byte[] keyHeader = Arrays.Slice(symmetricKey, sourceIndex: 0, Constants.SymmetricKeyHeader.Length);
            bool validKey = Utilities.Compare(keyHeader, Constants.SymmetricKeyHeader);
            if (!validKey) { throw new NotSupportedException("This isn't a symmetric key."); }
            return Arrays.SliceFromEnd(symmetricKey, Constants.SymmetricKeyHeader.Length);
        }
        catch (Exception ex)
        {
            DisplayMessage.KeyStringException(encodedSymmetricKey, ex.GetType().Name, ErrorMessages.InvalidSymmetricKey);
            return null;
        }
    }

    private static byte[] ReadKeyfile(string keyfilePath)
    {
        try
        {
            using var keyfile = new FileStream(keyfilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constants.FileStreamBufferSize, FileOptions.SequentialScan);
            using var blake2b = new GenericHash.GenericHashAlgorithm(key: (byte[])null, Constants.HashLength);
            return blake2b.ComputeHash(keyfile);
        }
        catch (Exception ex) when (ExceptionFilters.FileAccess(ex))
        {
            DisplayMessage.FilePathException(keyfilePath, ex.GetType().Name, "Unable to read the keyfile.");
            return null;
        }
    }
}