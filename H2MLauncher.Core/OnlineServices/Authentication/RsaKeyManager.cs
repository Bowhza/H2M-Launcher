using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;

namespace H2MLauncher.Core.OnlineServices.Authentication;

public sealed class RsaKeyManager
{
    private RSA? _rsa;
    private readonly string? _keyFilePath;
    private readonly ILogger<RsaKeyManager> _logger;

    // Optional: Add some random "entropy" to make the encryption more robust
    // This entropy should be constant for your application and does NOT need to be secret.
    // It helps differentiate your protected data from others.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("%d#85&E0~55Mgguc");

    public bool IsKeyLoaded => _rsa != null;
    public bool IsPersisted => _keyFilePath != null;

    public RsaKeyManager(string? keyFilePath, ILogger<RsaKeyManager> logger)
    {
        _keyFilePath = keyFilePath;
        _logger = logger;
    }

    /// <summary>
    /// Loads the RSA key from the file system. If not found or decryption fails, generates a new one and saves it.
    /// </summary>
    /// <returns>True if key was loaded or generated successfully, false otherwise.</returns>
    public bool LoadOrCreateKey()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("DPAPI is only available on Windows. This key manager will not work.");
            return false;
        }

        if (_keyFilePath is null)
        {
            return GenerateAndSaveNewKey();
        }

        if (!File.Exists(_keyFilePath))
        {
            _logger.LogInformation("Key file not found at {keyFilePath}. Generating a new RSA key...", _keyFilePath);
            return GenerateAndSaveNewKey();
        }

        try
        {
            var encryptedBytes = File.ReadAllBytes(_keyFilePath);
            byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);

            // Convert decrypted bytes back to PEM string
            string pemContent = Encoding.UTF8.GetString(decryptedBytes);

            // Try to create RSA from PEM
            _rsa = RSA.Create();
            _rsa.ImportFromPem(pemContent);

            _logger.LogInformation("RSA key loaded from {keyFilePath}.", _keyFilePath);

            return true;
        }
        catch (CryptographicException ex)
        {
            // This typically means the data is corrupted, or moved to another user/machine.
            _logger.LogError(ex, "Error decrypting RSA key using DPAPI. Generating a new key.");
            return GenerateAndSaveNewKey();
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Error importing PEM. File might be corrupted. Generating a new key.");
            return GenerateAndSaveNewKey();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while loading RSA key. Generating a new key.");
            return GenerateAndSaveNewKey();
        }
    }

    /// <summary>
    /// Generates a new RSA key pair and saves it to the file system (encrypted with DPAPI).
    /// </summary>
    /// <returns>True if key was generated and saved successfully, false otherwise.</returns>
    private bool GenerateAndSaveNewKey()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("DPAPI is only available on Windows. Cannot generate/save key securely.");
            return false;
        }

        try
        {
            // Generate a new RSA key pair
            _rsa = RSA.Create(2048);

            // Export private key as UNENCRYPTED PKCS#8 PEM string
            // We then encrypt this string's bytes with DPAPI.
            string unencryptedPem = _rsa.ExportRSAPrivateKeyPem();
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(unencryptedPem);

            // Encrypt with DPAPI
            byte[] encryptedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.CurrentUser);

            if (_keyFilePath is not null)
            {
                File.WriteAllBytes(_keyFilePath, encryptedBytes);
                _logger.LogInformation("New RSA key generated and saved to {keyFilePath}.", _keyFilePath);
            }
            else
            {
                _logger.LogInformation("New RSA key generated.");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating or saving RSA key");
            return false;
        }
    }

    /// <summary>
    /// Signs a challenge string using the loaded RSA private key.
    /// </summary>
    /// <param name="challenge">The challenge string to sign.</param>
    /// <returns>The Base64-encoded signature.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no RSA key is loaded or if not on Windows.</exception>
    public string SignChallenge(string challenge)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("RSA operations (signing) require the key manager to be initialized on Windows.");
        }
        if (_rsa == null)
        {
            throw new InvalidOperationException("RSA key is not loaded. Call LoadOrCreateKey() first.");
        }

        var dataToSign = Encoding.UTF8.GetBytes(challenge);
        var signature = _rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// Exports the public key of the loaded RSA key in SPKI (SubjectPublicKeyInfo) format.
    /// </summary>
    /// <returns>The Base64-encoded public key.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no RSA key is loaded or if not on Windows.</exception>
    public string GetPublicKeySpkiBase64()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("RSA operations (public key export) require the key manager to be initialized on Windows.");
        }
        if (_rsa == null)
        {
            throw new InvalidOperationException("RSA key is not loaded. Call LoadOrCreateKey() first.");
        }

        var publicKeyBytes = _rsa.ExportSubjectPublicKeyInfo();
        return Convert.ToBase64String(publicKeyBytes);
    }
}
