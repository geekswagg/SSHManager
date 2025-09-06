using Microsoft.Extensions.DependencyInjection;
using SSHManager.Commands;
using SSHManager.Models;
using SSHManager.Services.Interfaces;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SSHManager.Tests.Commands;

[TestClass]
public class GenerateKeysCommandTests
{
    private Mock<ISshKeyGenerator> _mockKeyGenerator = null!;
    private Mock<IFileService> _mockFileService = null!;
    private Mock<IClipboardService> _mockClipboardService = null!;
    private Mock<ISshConfigService> _mockConfigService = null!;
    private Mock<IConsoleService> _mockConsoleService = null!;
    private GenerateKeysCommand _command = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockKeyGenerator = new Mock<ISshKeyGenerator>();
        _mockFileService = new Mock<IFileService>();
        _mockClipboardService = new Mock<IClipboardService>();
        _mockConfigService = new Mock<ISshConfigService>();
        _mockConsoleService = new Mock<IConsoleService>();

        _command = new GenerateKeysCommand(
            _mockKeyGenerator.Object,
            _mockFileService.Object,
            _mockClipboardService.Object,
            _mockConfigService.Object,
            _mockConsoleService.Object);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithValidOptions_GeneratesKeyPairSuccessfully()
    {
        // Arrange
        var options = new KeyGenerationOptions
        {
            Name = "test_key",
            OutputDirectory = "/tmp",
            Comment = "test@example.com",
            CopyToClipboard = true,
            GenerateConfig = true
        };

        var expectedKeyPair = new SshKeyPair
        {
            PrivateKeyPem = "private-key-content",
            PublicKeyOpenSsh = "ssh-rsa AAAA... test@example.com",
            KeyName = "test_key",
            Comment = "test@example.com"
        };

        _mockKeyGenerator.Setup(x => x.GenerateKeyPair(options.Name, options.Comment, options.Passphrase, options.KeySize))
            .Returns(expectedKeyPair);
        _mockClipboardService.Setup(x => x.SetTextAsync(It.IsAny<string>())).ReturnsAsync(true);
        _mockConfigService.Setup(x => x.UpdateConfigFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockConsoleService.Setup(x => x.ShowProgressAsync(It.IsAny<string>(), It.IsAny<Func<IProgressContext, Task>>()))
            .Returns((string message, Func<IProgressContext, Task> operation) => operation(Mock.Of<IProgressContext>()));

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.AreEqual(expectedKeyPair, result.KeyPair);
        Assert.IsTrue(result.CopiedToClipboard);
        Assert.IsTrue(result.ConfigUpdated);
        
        _mockFileService.Verify(x => x.WriteTextFileAsync(It.IsAny<string>(), expectedKeyPair.PrivateKeyPem), Times.Once);
        _mockFileService.Verify(x => x.WriteTextFileAsync(It.IsAny<string>(), expectedKeyPair.PublicKeyOpenSsh), Times.Once);
        _mockClipboardService.Verify(x => x.SetTextAsync(It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithPassphrase_GeneratesEncryptedKey()
    {
        // Arrange
        var options = new KeyGenerationOptions
        {
            Name = "test_key",
            OutputDirectory = "/tmp",
            Comment = "test@example.com",
            Passphrase = "secret123",
            CopyToClipboard = false,
            GenerateConfig = false
        };

        var expectedKeyPair = new SshKeyPair
        {
            PrivateKeyPem = "private-key-content",
            PublicKeyOpenSsh = "ssh-rsa AAAA... test@example.com",
            EncryptedPrivateKeyPem = "encrypted-private-key-content",
            KeyName = "test_key",
            Comment = "test@example.com"
        };

        _mockKeyGenerator.Setup(x => x.GenerateKeyPair(options.Name, options.Comment, options.Passphrase, options.KeySize))
            .Returns(expectedKeyPair);
        _mockConsoleService.Setup(x => x.ShowProgressAsync(It.IsAny<string>(), It.IsAny<Func<IProgressContext, Task>>()))
            .Returns((string message, Func<IProgressContext, Task> operation) => operation(Mock.Of<IProgressContext>()));

        // Act
        var result = await _command.ExecuteAsync(options);

        // Assert
        Assert.IsNotNull(result.EncryptedKeyPath);
        _mockFileService.Verify(x => x.WriteTextFileAsync(It.IsAny<string>(), expectedKeyPair.EncryptedPrivateKeyPem), Times.Once);
    }
}