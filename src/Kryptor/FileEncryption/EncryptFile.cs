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
using System.Text;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Geralt;
using cAEAD;

namespace Kryptor;

public static class EncryptFile
{
    public static void Encrypt(string inputFilePath, string outputFilePath, bool isDirectory, Span<byte> unencryptedHeaders, Span<byte> nonce, Span<byte> headerKey)
    {
        Span<byte> fileKey = stackalloc byte[ChaCha20.KeySize];
        SecureRandom.Fill(fileKey);
        try
        {
            using (var inputFile = new FileStream(inputFilePath, FileHandling.GetFileStreamReadOptions(inputFilePath)))
            {
                long chunkCount = ((inputFile.Length != 0 ? inputFile.Length : 1) + Constants.FileChunkSize - 1) / Constants.FileChunkSize;
                using var outputFile = new FileStream(outputFilePath, FileHandling.GetFileStreamWriteOptions(chunkCount * Constants.CiphertextChunkLength + Constants.FileHeadersLength));
                Span<byte> encryptedHeader = EncryptFileHeader(chunkCount, inputFile.Length, isDirectory, Path.GetFileName(inputFilePath), unencryptedHeaders, fileKey, nonce, headerKey);
                outputFile.Write(unencryptedHeaders);
                outputFile.Write(encryptedHeader);
                ConstantTime.Increment(nonce);
                EncryptChunks(inputFile, outputFile, nonce, fileKey);
            }
            if (Globals.Overwrite) {
                FileHandling.OverwriteFile(inputFilePath, outputFilePath);
            }
            else if (isDirectory) {
                FileHandling.DeleteFile(inputFilePath);
            }
            FileHandling.SetReadOnly(outputFilePath);
        }
        catch (Exception ex) when (ExceptionFilters.Cryptography(ex))
        {
            CryptographicOperations.ZeroMemory(headerKey);
            CryptographicOperations.ZeroMemory(fileKey);
            CryptographicOperations.ZeroMemory(nonce);
            FileHandling.DeleteFile(outputFilePath);
            throw;
        }
    }
    
    private static Span<byte> EncryptFileHeader(long chunkCount, long fileLength, bool isDirectory, string fileName, Span<byte> unencryptedHeaders, Span<byte> fileKey, Span<byte> nonce, Span<byte> headerKey)
    {
        Span<byte> ciphertextLength = stackalloc byte[Constants.LongBytesLength];
        BinaryPrimitives.WriteInt64LittleEndian(ciphertextLength, chunkCount * Constants.CiphertextChunkLength);
        Span<byte> associatedData = stackalloc byte[ciphertextLength.Length + unencryptedHeaders.Length];
        Spans.Concat(associatedData, ciphertextLength, unencryptedHeaders);

        Span<byte> plaintextLength = stackalloc byte[Constants.LongBytesLength];
        BinaryPrimitives.WriteInt64LittleEndian(plaintextLength, fileLength);
        Span<byte> directory = BitConverter.GetBytes(isDirectory);
        Span<byte> paddedFileName = GetFileName(fileName, out Span<byte> fileNameLength);
        Span<byte> spare = stackalloc byte[Constants.LongBytesLength * 4]; spare.Clear();
        Span<byte> plaintextHeader = stackalloc byte[Constants.EncryptedHeaderLength - BLAKE2b.TagSize];
        Spans.Concat(plaintextHeader, plaintextLength, directory, fileNameLength, paddedFileName, spare, fileKey);
        
        Span<byte> ciphertextHeader = new byte[plaintextHeader.Length + BLAKE2b.TagSize];
        ChaCha20BLAKE2b.Encrypt(ciphertextHeader, plaintextHeader, nonce, headerKey, associatedData);
        CryptographicOperations.ZeroMemory(plaintextHeader);
        CryptographicOperations.ZeroMemory(headerKey);
        return ciphertextHeader;
    }

    private static Span<byte> GetFileName(string fileName, out Span<byte> fileNameLength)
    {
        Span<byte> fileNameBytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(fileName.Length)];
        int bytesEncoded = Encoding.UTF8.GetBytes(fileName, fileNameBytes);
        Span<byte> paddedFileName = new byte[Constants.FileNameHeaderLength];
        if (Globals.EncryptFileNames) {
            fileNameBytes[..bytesEncoded].CopyTo(paddedFileName);
        }
        fileNameLength = new byte[Constants.IntBytesLength];
        BinaryPrimitives.WriteInt32LittleEndian(fileNameLength, !Globals.EncryptFileNames ? 0 : bytesEncoded);
        return paddedFileName;
    }

    private static void EncryptChunks(Stream inputFile, Stream outputFile, Span<byte> nonce, Span<byte> fileKey)
    {
        Span<byte> plaintextChunk = GC.AllocateArray<byte>(Constants.FileChunkSize, pinned: true);
        Span<byte> ciphertextChunk = new byte[plaintextChunk.Length + BLAKE2b.TagSize];
        if (inputFile.Length == 0) {
            ChaCha20BLAKE2b.Encrypt(ciphertextChunk, plaintextChunk, nonce, fileKey);
            outputFile.Write(ciphertextChunk);
            return;
        }
        int bytesRead;
        while ((bytesRead = inputFile.Read(plaintextChunk)) > 0) {
            if (bytesRead < plaintextChunk.Length) {
                CryptographicOperations.ZeroMemory(plaintextChunk[bytesRead..]);
            }
            ChaCha20BLAKE2b.Encrypt(ciphertextChunk, plaintextChunk, nonce, fileKey);
            ConstantTime.Increment(nonce);
            outputFile.Write(ciphertextChunk);
        }
        CryptographicOperations.ZeroMemory(plaintextChunk[..bytesRead]);
        CryptographicOperations.ZeroMemory(fileKey);
        CryptographicOperations.ZeroMemory(nonce);
    }
}