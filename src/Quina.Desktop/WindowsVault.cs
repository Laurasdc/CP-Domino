using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Quina.Core;
using Quina.Infrastructure;

namespace Quina.Desktop;

/// <summary>DPAPI CurrentUser: segredos cifrados fora do repositório, sem fallback em texto puro.</summary>
public sealed class WindowsVault
{
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuinaCP6", "credentials.bin");
    private static readonly byte[] entropy = Encoding.UTF8.GetBytes("Quina.CP6.v1");
    private readonly string scope;
    public WindowsVault(string scope = "account") => this.scope = scope;
    public WindowsVault ForPlayer(Guid player) => new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DominoApiClient.DefaultBaseUrl + "/" + player))));

    private T Access<T>(Func<Dictionary<string, string>, T> operation, bool write)
    {
        using var mutex = new Mutex(false, @"Local\QuinaCP6Vault");
        bool locked;
        try { locked = mutex.WaitOne(TimeSpan.FromSeconds(4)); }
        catch (AbandonedMutexException) { locked = true; }
        if (!locked) throw new IOException("O cofre está em uso. Tente novamente.");
        try
        {
            Dictionary<string, string> values = [];
            if (File.Exists(path))
            {
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), entropy, DataProtectionScope.CurrentUser);
                try { values = JsonSerializer.Deserialize<Dictionary<string, string>>(plain) ?? []; }
                finally { CryptographicOperations.ZeroMemory(plain); }
            }
            var result = operation(values);
            if (write)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var plain = JsonSerializer.SerializeToUtf8Bytes(values);
                byte[] encrypted;
                try { encrypted = ProtectedData.Protect(plain, entropy, DataProtectionScope.CurrentUser); }
                finally { CryptographicOperations.ZeroMemory(plain); }
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.WriteAllBytes(temporary, encrypted); File.Move(temporary, path, true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            return result;
        }
        finally { mutex.ReleaseMutex(); }
    }
    public T? Get<T>(string name) => Access(values => values.TryGetValue(scope + ":" + name, out var raw) ? JsonSerializer.Deserialize<T>(raw, DominoApiClient.Json) : default, false);
    public void Set<T>(string name, T value) => Access(values => { values[scope + ":" + name] = JsonSerializer.Serialize(value, DominoApiClient.Json); return true; }, true);
    public void Remove(string name) => Access(values => values.Remove(scope + ":" + name), true);
    public string ActivationKey() => Access(values =>
    {
        string name = scope + ":activation";
        if (values.TryGetValue(name, out var raw)) return JsonSerializer.Deserialize<string>(raw)!;
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        values[name] = JsonSerializer.Serialize(key);
        return key;
    }, true);
}

public sealed class VaultIntentStore(WindowsVault vault) : IIntentStore
{
    public PendingIntent? Load() => vault.Get<PendingIntent>("pending");
    public void Save(PendingIntent? intent) => vault.Set("pending", intent);
}
