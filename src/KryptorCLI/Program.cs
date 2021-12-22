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
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;

namespace KryptorCLI;

[HelpOption("-h|--help", ShowInHelpText = false)]
[Command(ExtendedHelpText = @"  -h|--help       show help information

Examples:
  --encrypt -p [file]
  --encrypt -x [file]
  --encrypt [-y recipient's public key] [file]
  --decrypt [-y sender's public key] [file]
  --sign [-c comment] [file]
  --verify [-y public key] [-t signature] [file]

Stuck? Read the tutorial at <https://www.kryptor.co.uk/tutorial>.

Please report bugs at <https://github.com/samuel-lucas6/Kryptor/issues>.")]
public class Program
{
    [Option("-e|--encrypt", "encrypt files/folders", CommandOptionType.NoValue)]
    public bool Encrypt { get; }

    [Option("-d|--decrypt", "decrypt files/folders", CommandOptionType.NoValue)]
    public bool Decrypt { get; }

    [Option("-p|--password", "use a password", CommandOptionType.NoValue)]
    public bool Password { get; }

    [Option("-k|--keyfile", "specify a keyfile", CommandOptionType.SingleValue)]
    public string Keyfile { get; }

    [Option("-x|--private", "specify your private key (blank for default)", CommandOptionType.SingleOrNoValue)]
    public (bool hasValue, string value) PrivateKey { get; }

    [Option("-y|--public", "specify a public key", CommandOptionType.SingleValue)]
    public string PublicKey { get; }

    [Option("-f|--obfuscate", "obfuscate file names", CommandOptionType.NoValue)]
    public bool ObfuscateFileNames { get; }

    [Option("-o|--overwrite", "overwrite input files", CommandOptionType.NoValue)]
    public bool Overwrite { get; }

    [Option("-g|--generate", "generate a new key pair", CommandOptionType.NoValue)]
    public bool GenerateKeys { get; }

    [Option("-r|--recover", "recover your public key from your private key", CommandOptionType.NoValue)]
    public bool RecoverPublicKey { get; }

    [Option("-s|--sign", "create a signature", CommandOptionType.NoValue)]
    public bool Sign { get; }

    [Option("-c|--comment", "add a comment to a signature", CommandOptionType.SingleValue)]
    public string Comment { get; }

    [Option("-l|--prehash", "sign large files by prehashing", CommandOptionType.NoValue)]
    public bool Prehash { get; }

    [Option("-v|--verify", "verify a signature", CommandOptionType.NoValue)]
    public bool Verify { get; }

    [Option("-t|--signature", "specify a signature file", CommandOptionType.SingleValue)]
    public string Signature { get; }

    [Option("-u|--update", "check for updates", CommandOptionType.NoValue)]
    public bool CheckForUpdates { get; }

    [Option("-a|--about", "view the program version and license", CommandOptionType.NoValue)]
    public bool About { get; }

    [Argument(0, Name = "file", Description = "specify a file path")]
    public string[] FilePaths { get; }

    public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

    private void OnExecute()
    {
        ExtractVisualCRuntime();
        Globals.Overwrite = Overwrite;
        Globals.ObfuscateFileNames = ObfuscateFileNames;
        Globals.TotalCount = FilePaths.Length;
        Console.WriteLine();
        if (Encrypt)
        {
            CommandLine.Encrypt(Password, Keyfile, GetEncryptionPrivateKey(PrivateKey.value), PublicKey, FilePaths);
        }
        else if (Decrypt)
        {
            CommandLine.Decrypt(Password, Keyfile, GetEncryptionPrivateKey(PrivateKey.value), PublicKey, FilePaths);
        }
        else if (GenerateKeys)
        {
            CommandLine.GenerateNewKeyPair(FilePaths == null ? Constants.DefaultKeyDirectory : FilePaths[0]);
        }
        else if (RecoverPublicKey)
        {
            CommandLine.RecoverPublicKey(PrivateKey.value);
        }
        else if (Sign)
        {
            CommandLine.Sign(GetSigningPrivateKey(PrivateKey.value), Comment, Prehash, Signature, FilePaths);
        }
        else if (Verify)
        {
            CommandLine.Verify(PublicKey, Signature, FilePaths);
        }
        else if (CheckForUpdates)
        {
            CommandLine.CheckForUpdates();
        }
        else if (About)
        {
            CommandLine.DisplayAbout();
        }
        else
        {
            DisplayMessage.Error("Unknown command. Specify --help for a list of options and examples.");
        }
    }
    
    private static void ExtractVisualCRuntime()
    {
        try
        {
            string executableDirectory = Path.GetDirectoryName(Environment.ProcessPath);
            string vcruntimeFilePath = Path.Combine(executableDirectory, "vcruntime140.dll");
            if (!OperatingSystem.IsWindows() || File.Exists(vcruntimeFilePath)) { return; }
            if (Environment.Is64BitOperatingSystem)
            {
                File.WriteAllBytes(vcruntimeFilePath, Properties.Resources.vcruntime140x64);
                return;
            }
            File.WriteAllBytes(vcruntimeFilePath, Properties.Resources.vcruntime140x86);
        }
        catch (Exception ex) when (ExceptionFilters.FileAccess(ex))
        {
            DisplayMessage.Exception(ex.GetType().Name, "Unable to extract the vcruntime140.dll file, which is required for libsodium to function on Windows.");
        }
    }

    private static string GetEncryptionPrivateKey(string privateKey)
    {
        return string.IsNullOrEmpty(privateKey) ? Constants.DefaultEncryptionPrivateKeyPath : privateKey;
    }

    private static string GetSigningPrivateKey(string privateKey)
    {
        return string.IsNullOrEmpty(privateKey) ? Constants.DefaultSigningPrivateKeyPath : privateKey;
    }

    public static string GetVersion()
    {
        string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        return assemblyVersion[..^2];
    }
}