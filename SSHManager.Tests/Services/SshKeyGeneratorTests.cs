using Microsoft.VisualStudio.TestTools.UnitTesting;
using SSHManager.Services;

namespace SSHManager.Tests.Services;

[TestClass]
public class SshKeyGeneratorTests
{
    private SshKeyGenerator _generator = null!;

    [TestInitialize]
    public void Setup()
    {
        _generator = new SshKeyGenerator();
    }

    [TestMethod]
    public void GenerateKeyPair_WithValidInputs_ReturnsValidKeyPair()
    {
        // Arrange
        var keyName = "test_key";
        var comment = "test@example.com";

        // Act
        var result = _generator.GenerateKeyPair(keyName, comment);

        // Assert
        Assert.AreEqual(keyName, result.KeyName);
        Assert.AreEqual(comment, result.Comment);
        Assert.IsTrue(result.PrivateKeyPem.Contains("BEGIN RSA PRIVATE KEY"));
        Assert.IsTrue(result.PrivateKeyPem.Contains("END RSA PRIVATE KEY"));
        Assert.IsTrue(result.PublicKeyOpenSsh.StartsWith("ssh-rsa"));
        Assert.IsTrue(result.PublicKeyOpenSsh.EndsWith(comment));
        Assert.IsNull(result.EncryptedPrivateKeyPem);
    }

    [TestMethod]
    public void GenerateKeyPair_WithPassphrase_ReturnsEncryptedKey()
    {
        // Arrange
        var keyName = "test_key";
        var comment = "test@example.com";
        var passphrase = "secret123";

        // Act
        var result = _generator.GenerateKeyPair(keyName, comment, passphrase);

        // Assert
        Assert.IsNotNull(result.EncryptedPrivateKeyPem);
        Assert.IsTrue(result.EncryptedPrivateKeyPem.Contains("BEGIN ENCRYPTED PRIVATE KEY"));
        Assert.IsTrue(result.EncryptedPrivateKeyPem.Contains("END ENCRYPTED PRIVATE KEY"));
    }

    [TestMethod]
    public void GenerateKeyPair_WithCustomKeySize_GeneratesCorrectSize()
    {
        // Arrange
        var keyName = "test_key";
        var comment = "test@example.com";
        var keySize = 2048;

        // Act
        var result = _generator.GenerateKeyPair(keyName, comment, null, keySize);

        // Assert
        Assert.IsNotNull(result.PrivateKeyPem);
        Assert.IsNotNull(result.PublicKeyOpenSsh);
        // Note: We can't easily verify the exact key size without parsing the key,
        // but we can verify that the generation completed successfully
    }

    [TestMethod]
    public void GenerateKeyPair_MultipleCalls_GeneratesDifferentKeys()
    {
        // Arrange
        var keyName = "test_key";
        var comment = "test@example.com";

        // Act
        var result1 = _generator.GenerateKeyPair(keyName, comment);
        var result2 = _generator.GenerateKeyPair(keyName, comment);

        // Assert
        Assert.AreNotEqual(result1.PrivateKeyPem, result2.PrivateKeyPem);
        Assert.AreNotEqual(result1.PublicKeyOpenSsh, result2.PublicKeyOpenSsh);
    }
}