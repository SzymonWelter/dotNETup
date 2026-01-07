using DotNetUp.Core.Models;
using FluentAssertions;

namespace DotNetUp.Tests.Models;

public class InstallationResultTests
{
    [Fact]
    public void SuccessResult_CreatesSuccessfulResult()
    {
        // Arrange
        var message = "Operation completed successfully";

        // Act
        var result = InstallationStepResult.SuccessResult(message);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be(message);
        result.Exception.Should().BeNull();
        result.Data.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void FailureResult_CreatesFailedResult()
    {
        // Arrange
        var message = "Operation failed";

        // Act
        var result = InstallationStepResult.FailureResult(message);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(message);
        result.Exception.Should().BeNull();
        result.Data.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SuccessResult_WithData_InitializesDataDictionary()
    {
        // Arrange
        var message = "Success with data";
        var data = new Dictionary<string, object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = 42
        };

        // Act
        var result = InstallationStepResult.SuccessResult(message, data);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be(message);
        result.Data.Should().HaveCount(2);
        result.Data["Key1"].Should().Be("Value1");
        result.Data["Key2"].Should().Be(42);
    }

    [Fact]
    public void FailureResult_WithException_StoresException()
    {
        // Arrange
        var message = "Operation failed with exception";
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = InstallationStepResult.FailureResult(message, exception);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(message);
        result.Exception.Should().Be(exception);
        result.Exception!.Message.Should().Be("Test exception");
    }

    [Fact]
    public void SuccessResult_WithNullData_CreatesEmptyDictionary()
    {
        // Arrange
        var message = "Success";

        // Act
        var result = InstallationStepResult.SuccessResult(message, null);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void FailureResult_WithNullData_CreatesEmptyDictionary()
    {
        // Arrange
        var message = "Failure";

        // Act
        var result = InstallationStepResult.FailureResult(message, data: null);

        // Assert
        result.Success.Should().BeFalse();
        result.Data.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void FailureResult_WithExceptionAndData_StoresBoth()
    {
        // Arrange
        var message = "Complex failure";
        var exception = new ArgumentException("Invalid argument");
        var data = new Dictionary<string, object> { ["ErrorCode"] = 500 };

        // Act
        var result = InstallationStepResult.FailureResult(message, exception, data);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(message);
        result.Exception.Should().Be(exception);
        result.Data.Should().HaveCount(1);
        result.Data["ErrorCode"].Should().Be(500);
    }

    [Fact]
    public void SuccessResult_WithEmptyMessage_IsAllowed()
    {
        // Arrange & Act
        var result = InstallationStepResult.SuccessResult(string.Empty);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().BeEmpty();
    }

    [Fact]
    public void FailureResult_WithEmptyMessage_IsAllowed()
    {
        // Arrange & Act
        var result = InstallationStepResult.FailureResult(string.Empty);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().BeEmpty();
    }

    [Fact]
    public void Result_Data_IsMutable()
    {
        // Arrange
        var result = InstallationStepResult.SuccessResult("Test");

        // Act
        result.Data["NewKey"] = "NewValue";

        // Assert
        result.Data.Should().HaveCount(1);
        result.Data["NewKey"].Should().Be("NewValue");
    }
}
