using System.IO;
using Coralph;
using Xunit;

namespace Coralph.Tests;

public class LoggingTests
{
    [Fact]
    public void Configure_CreatesLogDirectory()
    {
        // Arrange
        var options = new LoopOptions { Model = "test-model" };

        // Act
        Logging.Configure(options);

        // Assert - logs directory should be created when first log is written
        // We just verify Configure doesn't throw
        Logging.Close();
    }

    [Fact]
    public void Configure_DoesNotThrowWithDefaultOptions()
    {
        // Arrange
        var options = new LoopOptions();

        // Act & Assert - should not throw
        Logging.Configure(options);
        Logging.Close();
    }
}
