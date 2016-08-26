﻿using Newtonsoft.Json;
using SteamAuth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public static class Program
{
    const string defaultSteamGuardPath = "~/maFiles";

    public static string SteamGuardPath { get; set; } = defaultSteamGuardPath;
    public static Manifest Manifest { get; set; }
    public static SteamGuardAccount[] SteamGuardAccounts { get; set; }
    public static bool Verbose { get; set; } = false;

    /// <summary>
    ///   The main entry point for the application
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        string action = "";
        string user = "";

	    // Parse cli arguments
	    for (int i = 0; i < args.Length; i++)
	    {
		    if (args[i].StartsWith("-"))
		    {
			    if (args[i] == "-v" || args[i] == "--verbose")
			    {
				    Verbose = true;
			    }
			    else if (args[i] == "-m" || args[i] == "--mafiles-path")
			    {
				    i++;
				    if (i < args.Length)
				    	SteamGuardPath = args[i];
				    else
				    {
					    Console.WriteLine($"Expected path after {args[i-1]}");
					    return;
				    }
			    }
			    else if (args[i] == "--help" || args[i] == "-h")
			    {
				    ShowHelp();
				    return;
			    }
		    }
		    else // Parse as action or username
		    {
			    if (string.IsNullOrEmpty(action))
			    {
				    if (args[i] == "add")
				    {
					    action = "setup";
				    }
				    else if (args[i] == "encrypt")
				    {
					    action = "encrypt";
				    }
				    else if (args[i] == "decrypt")
				    {
					    action = "decrypt";
				    }
				    else if (args[i] == "remove")
				    {
					    action = "remove";
				    }
				    else if (args[i] == "2fa" || args[i] == "code" || args[i] == "generate-code")
				    {
					    action = "generate-code";
				    }
				    else if (string.IsNullOrEmpty(user))
					    user = args[i];
			    }
			    else if (string.IsNullOrEmpty(user))
				    user = args[i];
		    }
	    }

	    if (string.IsNullOrEmpty(action))
		    action = "generate-code";

        // Do some configuring
        SteamGuardPath = SteamGuardPath.Replace("~", Environment.GetEnvironmentVariable("HOME"));
        if (!Directory.Exists(SteamGuardPath))
        {
            if (SteamGuardPath == defaultSteamGuardPath.Replace("~", Environment.GetEnvironmentVariable("HOME")))
            {
                if (Verbose) Console.WriteLine("warn: {0} does not exist, creating...", SteamGuardPath);
                Directory.CreateDirectory(SteamGuardPath);
            }
            else
            {
                Console.WriteLine("error: {0} does not exist.", SteamGuardPath);
                return;
            }
        }

	    if (Verbose) Console.WriteLine($"Action: {action}");
	    if (Verbose) Console.WriteLine($"User: {user}");
	    if (Verbose) Console.WriteLine($"maFiles path: {SteamGuardPath}");

        // Perform desired action
        switch (action)
        {
            case "generate-code":
                GenerateCode(user);
                break;
            case "encrypt": // Can also be used to change passkey
                Console.WriteLine(Encrypt());
                break;
            case "decrypt":
                Console.WriteLine(Decrypt());
                break;
            case "setup":
                throw new NotSupportedException();
                break;
            default:
                Console.WriteLine("error: Unknown action: {0}", action);
                return;
        }
    }

	static void ShowHelp()
	{
		var descPadding = 26;
		var descWidth = Console.BufferWidth - descPadding;
		if (descWidth < 20)
			descWidth = 20;
		else if (descWidth > 56)
			descWidth = 56;

		var flags = new Dictionary<string, string>
		{
			{ "-h, --help", "Display this help message." },
			{ "-v, --verbose", "Display some extra information when the program is running." },
			{ "-m, --mafiles", "Specify which folder your maFiles are in. Ex: ~/maFiles" },
		};
		var actions = new Dictionary<string, string>
		{
			{ "generate-code", "Generate a Steam Guard code for the specified user (if any) and exit. (default)" },
			{ "encrypt", "Encrypt your maFiles or change your encryption passkey." },
			{ "decrypt", "Remove encryption from your maFiles." },
			{ "code", "Same as generate-code" },
			{ "2fa", "Same as generate-code" },
		};

		Console.WriteLine($"steamguard-cli - v{Assembly.GetExecutingAssembly().GetName().Version}");
		Console.WriteLine("usage: steamguard (action) (steam username) -v -h");
		Console.WriteLine();
		foreach (var flag in flags)
		{
			// word wrap the descriptions, if needed
			var desc = flag.Value;
			if (desc.Length > descWidth)
			{
				var sb = new StringBuilder();
				for (int i = 0; i < desc.Length; i += descWidth)
				{
					if (i > 0)
						sb.Append("".PadLeft((flag.Key.StartsWith("--") ? 5 : 2) + descPadding));
					sb.AppendLine(desc.Substring(i, i + descWidth > desc.Length ? desc.Length - i : descWidth).Trim());
				}
				desc = sb.ToString().TrimEnd('\n');
			}
			Console.WriteLine($"{(flag.Key.StartsWith("--") ? "     " : "  " )}{flag.Key.PadRight(descPadding)}{desc}");
		}
		Console.WriteLine();
		Console.WriteLine("Actions:");
		foreach (var action in actions)
		{
			// word wrap the descriptions, if needed
			var desc = action.Value;
			if (desc.Length > descWidth)
			{
				var sb = new StringBuilder();
				for (int i = 0; i < desc.Length; i += descWidth)
				{
					if (i > 0)
						sb.Append("".PadLeft(descPadding + 2));
					sb.AppendLine(desc.Substring(i, i + descWidth > desc.Length ? desc.Length - i : descWidth).Trim());
				}
				desc = sb.ToString().TrimEnd('\n');
			}
			Console.WriteLine($"  {action.Key.PadRight(descPadding)}{desc}");
		}
	}

    static void GenerateCode(string user = "")
    {
        if (Verbose) Console.WriteLine("Aligning time...");
        TimeAligner.AlignTime();
        if (Verbose) Console.WriteLine("Opening manifest...");
        Manifest = Manifest.GetManifest(true);
        if (Verbose) Console.WriteLine("Reading accounts from manifest...");
        if (Manifest.Encrypted)
        {
            string passkey = Manifest.PromptForPassKey();
            SteamGuardAccounts = Manifest.GetAllAccounts(passkey);
        }
        else
        {
            SteamGuardAccounts = Manifest.GetAllAccounts();
        }
        if (SteamGuardAccounts.Length == 0)
        {
            Console.WriteLine("error: No accounts read.");
            return;
        }
        if (Verbose) Console.WriteLine("Selecting account...");
        string code = "";
        for (int i = 0; i < SteamGuardAccounts.Length; i++)
        {
            SteamGuardAccount account = SteamGuardAccounts[i];
            if (user != "")
            {
                if (account.AccountName.ToLower() == user.ToLower())
                {
                    if (Verbose) Console.WriteLine("Generating Code...");
                    code = account.GenerateSteamGuardCode();
                    break;
                }
            }
            else
            {
                if (Verbose) Console.WriteLine("Generating Code for {0}...", account.AccountName);
                code = account.GenerateSteamGuardCode();
                break;
            }
        }
        if (code != "")
            Console.WriteLine(code);
        else
            Console.WriteLine("error: No Steam accounts found in {0}", SteamGuardAccounts);
    }

    static bool Encrypt()
    {
        if (Verbose) Console.WriteLine("Opening manifest...");
        Manifest = Manifest.GetManifest(true);
        if (Verbose) Console.WriteLine("Reading accounts from manifest...");
        if (Manifest.Encrypted)
        {
            string passkey = Manifest.PromptForPassKey();
            SteamGuardAccounts = Manifest.GetAllAccounts(passkey);
        }
        else
        {
            SteamGuardAccounts = Manifest.GetAllAccounts();
        }

        string newPassKey = Manifest.PromptSetupPassKey();

        for (int i = 0; i < SteamGuardAccounts.Length; i++)
        {
            var account = SteamGuardAccounts[i];
			var salt = Manifest.GetRandomSalt();
			var iv = Manifest.GetInitializationVector();
            bool success = Manifest.SaveAccount(account, true, newPassKey, salt, iv);
            if (Verbose) Console.WriteLine("Encrypted {0}: {1}", account.AccountName, success);
			if (!success) return false;
        }
		return true;
    }

    static bool Decrypt()
    {
        if (Verbose) Console.WriteLine("Opening manifest...");
        Manifest = Manifest.GetManifest(true);
        if (Verbose) Console.WriteLine("Reading accounts from manifest...");
        if (Manifest.Encrypted)
        {
            string passkey = Manifest.PromptForPassKey();
            SteamGuardAccounts = Manifest.GetAllAccounts(passkey);
        }
        else
        {
            if (Verbose) Console.WriteLine("Decryption not required.");
            return true;
        }

        for (int i = 0; i < SteamGuardAccounts.Length; i++)
        {
            var account = SteamGuardAccounts[i];
            bool success = Manifest.SaveAccount(account, false);
            if (Verbose) Console.WriteLine("Decrypted {0}: {1}", account.AccountName, success);
			if (!success) return false;
        }
		return true;
    }
}
