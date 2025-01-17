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
using System.Security.Cryptography;
using Geralt;
using kcAEAD;
using ChaCha20BLAKE2;

namespace Kryptor;

public static class PrivateKey
{
    public static Span<byte> Encrypt(Span<byte> privateKey, Span<byte> password, Span<byte> keyAlgorithm)
    {
        DisplayMessage.DerivingKeyFromPassword();
        Span<byte> salt = stackalloc byte[Argon2id.SaltSize];
        SecureRandom.Fill(salt);

        Span<byte> nonce = stackalloc byte[ChaCha20.NonceSize]; nonce.Clear();
        Span<byte> key = stackalloc byte[ChaCha20.KeySize];
        Argon2id.DeriveKey(key, password, salt, Constants.Iterations, Constants.MemorySize);
        CryptographicOperations.ZeroMemory(password);

        Span<byte> associatedData = stackalloc byte[keyAlgorithm.Length + Constants.PrivateKeyVersion2.Length];
        Spans.Concat(associatedData, keyAlgorithm, Constants.PrivateKeyVersion2);

        Span<byte> encryptedPrivateKey = stackalloc byte[kcChaCha20Poly1305.CommitmentSize + privateKey.Length + Poly1305.TagSize];
        kcChaCha20Poly1305.Encrypt(encryptedPrivateKey, privateKey, nonce, key, associatedData);
        CryptographicOperations.ZeroMemory(privateKey);
        CryptographicOperations.ZeroMemory(key);

        Span<byte> fullPrivateKey = new byte[associatedData.Length + salt.Length + encryptedPrivateKey.Length];
        Spans.Concat(fullPrivateKey, associatedData, salt, encryptedPrivateKey);
        return fullPrivateKey;
    }

    public static Span<byte> DecryptV2(Span<byte> privateKey, Span<byte> password)
    {
        try
        {
            Span<byte> associatedData = privateKey[..(Constants.Curve25519KeyHeader.Length + Constants.PrivateKeyVersion2.Length)];
            Span<byte> salt = privateKey.Slice(associatedData.Length, Argon2id.SaltSize);
            Span<byte> encryptedPrivateKey = privateKey[(associatedData.Length + salt.Length)..];
            
            Span<byte> nonce = stackalloc byte[ChaCha20.NonceSize]; nonce.Clear();
            Span<byte> key = stackalloc byte[ChaCha20.KeySize];
            Argon2id.DeriveKey(key, password, salt, Constants.Iterations, Constants.MemorySize);
            CryptographicOperations.ZeroMemory(password);

            Span<byte> decryptedPrivateKey = GC.AllocateArray<byte>(encryptedPrivateKey.Length - Poly1305.TagSize - kcChaCha20Poly1305.CommitmentSize, pinned: true);
            kcChaCha20Poly1305.Decrypt(decryptedPrivateKey, encryptedPrivateKey, nonce, key, associatedData);
            CryptographicOperations.ZeroMemory(key);
            return decryptedPrivateKey;
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Incorrect password, or the private key has been tampered with.", ex);
        }
    }

    public static Span<byte> DecryptV1(Span<byte> privateKey, Span<byte> password)
    {
        try
        {
            Span<byte> associatedData = privateKey[..(Constants.OldCurve25519KeyHeader.Length + Constants.PrivateKeyVersion1.Length)];
            Span<byte> salt = privateKey.Slice(associatedData.Length, Argon2id.SaltSize);
            Span<byte> nonce = privateKey.Slice(associatedData.Length + salt.Length, XChaCha20.NonceSize);
            Span<byte> encryptedPrivateKey = privateKey[(associatedData.Length + salt.Length + nonce.Length)..];

            Span<byte> key = stackalloc byte[ChaCha20.KeySize];
            Argon2id.DeriveKey(key, password, salt, iterations: 12, Constants.MemorySize);
            CryptographicOperations.ZeroMemory(password);

            Span<byte> decryptedPrivateKey = XChaCha20BLAKE2b.Decrypt(encryptedPrivateKey.ToArray(), nonce.ToArray(), key.ToArray(), associatedData.ToArray());
            CryptographicOperations.ZeroMemory(key);
            return decryptedPrivateKey;
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Incorrect password, or the private key has been tampered with.", ex);
        }
    }
}