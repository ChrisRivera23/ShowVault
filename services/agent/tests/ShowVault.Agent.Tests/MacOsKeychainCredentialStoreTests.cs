using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;
using ShowVault.Agent.Identity;
using Xunit;

namespace ShowVault.Agent.Tests;

public sealed class MacOsKeychainCredentialStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "showvault-keychain-tests", Guid.NewGuid().ToString("N"));
    private readonly string _password = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

    [Fact]
    public async Task Dedicated_keychain_survives_lock_and_reopen()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        Directory.CreateDirectory(_root);
        var keychainPath = Path.Combine(_root, "venue-agent.keychain-db");
        var passwordPath = Path.Combine(_root, "keychain-password");
        await File.WriteAllTextAsync(passwordPath, _password);
        RunSecurity("create-keychain", "-p", _password, keychainPath);

        var store = CreateStore(keychainPath, passwordPath);
        var identity = new StoredAgentIdentity(Guid.NewGuid(), Guid.NewGuid(), "sva_durable_secret");
        await store.SaveAsync(identity, CancellationToken.None);
        RunSecurity("lock-keychain", keychainPath);

        Assert.Equal(identity, await store.LoadAsync(CancellationToken.None));

        await store.DeleteAsync(CancellationToken.None);
        Assert.Null(await store.LoadAsync(CancellationToken.None));
    }

    public void Dispose()
    {
        if (!OperatingSystem.IsMacOS() || !Directory.Exists(_root))
        {
            return;
        }

        var keychainPath = Path.Combine(_root, "venue-agent.keychain-db");
        if (File.Exists(keychainPath))
        {
            RunSecurity("delete-keychain", keychainPath);
        }

        Directory.Delete(_root, true);
    }

    [SupportedOSPlatform("macos")]
    private static MacOsKeychainCredentialStore CreateStore(string keychainPath, string passwordPath) =>
        new(Options.Create(new AgentOptions
        {
            ControlPlaneUri = new Uri("https://control.test"),
            Name = "Test Agent",
            MacOsKeychainPath = keychainPath,
            MacOsKeychainPasswordFile = passwordPath
        }));

    private static void RunSecurity(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/security",
            UseShellExecute = false,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start macOS security tool.");

        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
