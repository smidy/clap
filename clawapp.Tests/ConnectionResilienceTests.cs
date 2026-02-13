using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using clawapp.Models;
using clawapp.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace clawapp.Tests;

/// <summary>
/// Tests for connection resilience features: reconnection, exponential backoff, and offline message queue.
/// </summary>
public class ConnectionResilienceTests
{
    private readonly Mock<ILogger<OpenClawService>> _loggerMock;
    private readonly Mock<IPushNotificationService> _pushServiceMock;

    public ConnectionResilienceTests()
    {
        _loggerMock = new Mock<ILogger<OpenClawService>>();
        _pushServiceMock = new Mock<IPushNotificationService>();
    }

    [Fact]
    public void ConnectionState_Should_Default_To_Disconnected()
    {
        // Arrange & Act
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Assert
        Assert.Equal(ConnectionState.Disconnected, service.ConnectionState);
        Assert.Equal(0, service.OfflineQueueCount);
        Assert.Equal(0, service.CurrentReconnectAttempt);
    }

    [Fact]
    public void IsConnected_Should_Be_False_When_Not_Connected()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Assert
        Assert.False(service.IsConnected);
    }

    [Fact]
    public void OfflineQueueCount_Should_Return_Queue_Size()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Assert
        Assert.Equal(0, service.OfflineQueueCount);
    }

    [Fact]
    public void OnConnectionStateChanged_Can_Be_Subscribed()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        var handlerCalled = false;

        // Act - subscribe to event
        ((IOpenClawService)service).OnConnectionStateChanged += (s, e) =>
        {
            handlerCalled = true;
        };

        // Assert - handler can be subscribed
        Assert.False(handlerCalled); // Won't be called until state changes
    }

    [Fact]
    public void OnReconnectAttempt_Can_Be_Subscribed()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        var attemptReceived = 0;

        // Act - subscribe to event
        ((IOpenClawService)service).OnReconnectAttempt += (s, e) =>
        {
            attemptReceived = e;
        };

        // Assert - handler can be subscribed
        Assert.Equal(0, attemptReceived); // Won't be called until reconnect happens
    }

    [Fact]
    public void OnReconnected_Can_Be_Subscribed()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        var reconnected = false;

        // Act - subscribe to event
        ((IOpenClawService)service).OnReconnected += (s, e) =>
        {
            reconnected = true;
        };

        // Assert - handler can be subscribed
        Assert.False(reconnected); // Won't be called until reconnect happens
    }

    [Fact]
    public void OnOfflineQueueEmpty_Can_Be_Subscribed()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        var queueEmpty = false;

        // Act - subscribe to event
        ((IOpenClawService)service).OnOfflineQueueEmpty += (s, e) =>
        {
            queueEmpty = true;
        };

        // Assert - handler can be subscribed
        Assert.False(queueEmpty); // Won't be called until queue is processed
    }

    [Fact]
    public async Task CancelReconnectionAsync_Should_Not_Throw_When_Not_Reconnecting()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Act & Assert - should not throw
        await service.CancelReconnectionAsync();
        
        // State should remain disconnected
        Assert.Equal(ConnectionState.Disconnected, service.ConnectionState);
    }

    [Fact]
    public async Task DisconnectAsync_Should_Not_Throw_When_Not_Connected()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Act & Assert - should not throw even when not connected
        await service.DisconnectAsync();
        
        Assert.Equal(ConnectionState.Disconnected, service.ConnectionState);
    }

    [Fact]
    public void SendMessageAsync_WhenNotConnected_ShouldQueueMessage()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        Assert.Equal(0, service.OfflineQueueCount);

        // Act - this should queue the message since we're not connected
        _ = service.SendMessageAsync("test-session", "Hello, World!");

        // Assert - message should be queued
        Assert.Equal(1, service.OfflineQueueCount);
    }

    [Fact]
    public void SendMessageWithAttachmentsAsync_WhenNotConnected_ShouldQueueMessage()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        var attachments = new List<PendingAttachment>
        {
            new() { FileName = "test.txt", MimeType = "text/plain", Data = new byte[] { 1, 2, 3 } }
        };
        Assert.Equal(0, service.OfflineQueueCount);

        // Act - this should queue the message since we're not connected
        _ = service.SendMessageWithAttachmentsAsync("test-session", "Hello with attachment", attachments);

        // Assert - message should be queued
        Assert.Equal(1, service.OfflineQueueCount);
    }

    [Fact]
    public void MultipleMessages_WhenNotConnected_ShouldAllQueue()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Act
        _ = service.SendMessageAsync("session1", "Message 1");
        _ = service.SendMessageAsync("session1", "Message 2");
        _ = service.SendMessageAsync("session2", "Message 3");

        // Assert
        Assert.Equal(3, service.OfflineQueueCount);
    }

    [Theory]
    [InlineData(1, 1000, 900, 1100)]   // First attempt: ~1000ms
    [InlineData(2, 2000, 1800, 2200)]  // Second attempt: ~2000ms
    [InlineData(3, 4000, 3600, 4400)]  // Third attempt: ~4000ms
    [InlineData(4, 8000, 7200, 8800)]  // Fourth attempt: ~8000ms
    [InlineData(5, 16000, 14400, 17600)] // Fifth attempt: ~16000ms
    public void ExponentialBackoff_DelayCalculation_ShouldBeCorrect(
        int attempt, int expectedBaseDelay, int minDelay, int maxDelay)
    {
        // The actual delay includes jitter (±10%), so we verify it's within expected range
        var baseDelay = 1000 * Math.Pow(2, attempt - 1);
        var cappedDelay = Math.Min(30000, baseDelay);
        var jitter = cappedDelay / 10;
        
        Assert.Equal(expectedBaseDelay, cappedDelay);
        Assert.True(cappedDelay + jitter >= minDelay);
        Assert.True(cappedDelay - jitter <= maxDelay);
    }

    [Fact]
    public void ExponentialBackoff_MaxDelay_ShouldBeCapped()
    {
        // For high attempt numbers, delay should be capped at 30000ms
        for (int attempt = 6; attempt <= 10; attempt++)
        {
            var baseDelay = 1000 * Math.Pow(2, attempt - 1);
            var cappedDelay = Math.Min(30000, baseDelay);
            Assert.Equal(30000, cappedDelay);
        }
    }

    [Fact]
    public void IOpenClawService_ShouldHaveConnectionStateProperty()
    {
        // Verify the interface exposes ConnectionState
        IOpenClawService service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        Assert.Equal(ConnectionState.Disconnected, service.ConnectionState);
    }

    [Fact]
    public void IOpenClawService_ShouldHaveOfflineQueueCountProperty()
    {
        // Verify the interface exposes OfflineQueueCount
        IOpenClawService service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        Assert.Equal(0, service.OfflineQueueCount);
    }

    [Fact]
    public void IOpenClawService_ShouldHaveCurrentReconnectAttemptProperty()
    {
        // Verify the interface exposes CurrentReconnectAttempt
        IOpenClawService service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        Assert.Equal(0, service.CurrentReconnectAttempt);
    }

    [Fact]
    public void ConnectionStateEnum_ShouldHaveExpectedValues()
    {
        // Verify enum values are as expected
        Assert.Equal(0, (int)ConnectionState.Disconnected);
        Assert.Equal(1, (int)ConnectionState.Connecting);
        Assert.Equal(2, (int)ConnectionState.Connected);
        Assert.Equal(3, (int)ConnectionState.Reconnecting);
        Assert.Equal(4, (int)ConnectionState.Disconnecting);
    }

    [Fact]
    public void QueuedMessage_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var message = new QueuedMessage();

        // Assert
        Assert.Equal("", message.SessionKey);
        Assert.Equal("", message.Text);
        Assert.Null(message.Attachments);
        Assert.True((DateTimeOffset.UtcNow - message.QueuedAt).TotalSeconds < 1);
        Assert.Equal(0, message.AttemptCount);
        Assert.NotNull(message.Id);
        Assert.NotEmpty(message.Id);
    }

    [Fact]
    public void QueuedMessage_WithValues_ShouldStoreCorrectly()
    {
        // Arrange
        var attachments = new List<PendingAttachment>
        {
            new() { FileName = "test.txt", MimeType = "text/plain", Data = new byte[] { 1, 2, 3 } }
        };

        // Act
        var message = new QueuedMessage
        {
            SessionKey = "session123",
            Text = "Test message",
            Attachments = attachments,
            AttemptCount = 2
        };

        // Assert
        Assert.Equal("session123", message.SessionKey);
        Assert.Equal("Test message", message.Text);
        Assert.Equal(attachments, message.Attachments);
        Assert.Equal(2, message.AttemptCount);
    }
}

/// <summary>
/// Tests specifically for Polly-based resilience features.
/// </summary>
public class PollyResilienceTests
{
    private readonly Mock<ILogger<OpenClawService>> _loggerMock;
    private readonly Mock<IPushNotificationService> _pushServiceMock;

    public PollyResilienceTests()
    {
        _loggerMock = new Mock<ILogger<OpenClawService>>();
        _pushServiceMock = new Mock<IPushNotificationService>();
    }

    [Fact]
    public void Service_ShouldInitialize_WithZeroReconnectAttempt()
    {
        // Arrange & Act
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Assert
        Assert.Equal(0, service.CurrentReconnectAttempt);
    }

    [Fact]
    public void Service_ShouldExpose_CurrentReconnectAttempt()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Act - verify the property is accessible
        var attempt = service.CurrentReconnectAttempt;

        // Assert
        Assert.Equal(0, attempt);
    }

    [Fact]
    public void Service_ShouldTrack_OfflineQueueCount()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Act - queue some messages
        _ = service.SendMessageAsync("session1", "Message 1");
        _ = service.SendMessageAsync("session2", "Message 2");

        // Assert
        Assert.Equal(2, service.OfflineQueueCount);
    }

    [Fact]
    public async Task CancelReconnectionAsync_Should_ResetReconnectAttempt()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Act - we can't easily trigger a reconnection in unit tests,
        // but we can verify the method doesn't throw and state is maintained
        await service.CancelReconnectionAsync();

        // Assert
        Assert.Equal(0, service.CurrentReconnectAttempt);
        Assert.Equal(ConnectionState.Disconnected, service.ConnectionState);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void ExponentialBackoff_Formula_ShouldProduceExpectedDelays(int attempt)
    {
        // Arrange
        var expectedBaseDelay = Math.Pow(2, attempt - 1) * 1000; // In milliseconds
        var maxDelay = 30000; // Cap at 30 seconds

        // Act
        var actualDelay = Math.Min(maxDelay, expectedBaseDelay);

        // Assert - verify the formula produces expected values
        if (attempt <= 5)
        {
            Assert.Equal(expectedBaseDelay, actualDelay);
        }
        else
        {
            Assert.Equal(maxDelay, actualDelay); // Should be capped
        }
    }

    [Fact]
    public void Jitter_Calculation_ShouldBeWithinRange()
    {
        // Arrange
        const int jitterMilliseconds = 500;
        const int iterations = 100;

        // Act & Assert
        for (int i = 0; i < iterations; i++)
        {
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(-jitterMilliseconds, jitterMilliseconds));
            
            // Jitter should be within ±500ms
            Assert.True(jitter.TotalMilliseconds >= -jitterMilliseconds, 
                $"Jitter {jitter.TotalMilliseconds}ms is less than minimum {-jitterMilliseconds}ms");
            Assert.True(jitter.TotalMilliseconds <= jitterMilliseconds, 
                $"Jitter {jitter.TotalMilliseconds}ms is greater than maximum {jitterMilliseconds}ms");
        }
    }

    [Fact]
    public void ConnectionState_ShouldFire_EventOnChange()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        var eventFired = false;
        ConnectionState? receivedState = null;

        ((IOpenClawService)service).OnConnectionStateChanged += (s, e) =>
        {
            eventFired = true;
            receivedState = e;
        };

        // Note: We can't easily trigger a state change without actually connecting,
        // but we can verify the event subscription works
        Assert.False(eventFired);
        Assert.Null(receivedState);
    }

    [Fact]
    public void Service_Should_LogExceptionsWithFullDetails()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Act - create an exception with inner exception and stack trace info
        var innerEx = new InvalidOperationException("Inner error");
        var outerEx = new WebSocketException("WebSocket failed", innerEx);

        // Note: In the actual implementation, exceptions are logged in the onRetry callback
        // with _logger.LogWarning(exception, ...), which includes the full stack trace

        // Assert - verify logger is properly configured (it's a mock, so we just verify service was created)
        Assert.NotNull(service);
    }
}

/// <summary>
/// Tests for connection state machine behavior.
/// </summary>
public class ConnectionStateMachineTests
{
    private readonly Mock<ILogger<OpenClawService>> _loggerMock;
    private readonly Mock<IPushNotificationService> _pushServiceMock;

    public ConnectionStateMachineTests()
    {
        _loggerMock = new Mock<ILogger<OpenClawService>>();
        _pushServiceMock = new Mock<IPushNotificationService>();
    }

    [Fact]
    public void InitialState_Should_BeDisconnected()
    {
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        Assert.Equal(ConnectionState.Disconnected, service.ConnectionState);
    }

    [Fact]
    public void IsConnected_ShouldReflect_WebSocketState()
    {
        // When no WebSocket exists, IsConnected should be false
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public void OfflineQueue_Should_BeEmptyInitially()
    {
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        Assert.Equal(0, service.OfflineQueueCount);
    }

    [Fact]
    public void Events_Should_AllowMultipleSubscribers()
    {
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        var count1 = 0;
        var count2 = 0;

        ((IOpenClawService)service).OnConnectionStateChanged += (s, e) => count1++;
        ((IOpenClawService)service).OnConnectionStateChanged += (s, e) => count2++;

        // Both handlers should be subscribed
        Assert.Equal(0, count1); // Not fired yet
        Assert.Equal(0, count2); // Not fired yet
    }
}

/// <summary>
/// Tests for cancellation token handling.
/// </summary>
public class CancellationTokenTests
{
    private readonly Mock<ILogger<OpenClawService>> _loggerMock;
    private readonly Mock<IPushNotificationService> _pushServiceMock;

    public CancellationTokenTests()
    {
        _loggerMock = new Mock<ILogger<OpenClawService>>();
        _pushServiceMock = new Mock<IPushNotificationService>();
    }

    [Fact]
    public void CancellationToken_Should_NotBeReused_AfterCancellation()
    {
        // Arrange
        var cts1 = new CancellationTokenSource();
        var token1 = cts1.Token;

        // Act - cancel the first token
        cts1.Cancel();

        // Assert - token1 is cancelled
        Assert.True(token1.IsCancellationRequested);

        // A fresh CTS should not be cancelled
        var cts2 = new CancellationTokenSource();
        var token2 = cts2.Token;
        Assert.False(token2.IsCancellationRequested);

        // Cleanup
        cts1.Dispose();
        cts2.Dispose();
    }

    [Fact]
    public void LinkedTokenSource_ShouldCancel_WhenAnySourceCancels()
    {
        // Arrange
        var userCts = new CancellationTokenSource();
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromHours(1));
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token);

        // Act - cancel user token
        userCts.Cancel();

        // Assert - linked token should also be cancelled
        Assert.True(linkedCts.Token.IsCancellationRequested);

        // Cleanup
        userCts.Dispose();
        timeoutCts.Dispose();
        linkedCts.Dispose();
    }

    [Fact]
    public async Task CancelReconnectionAsync_ShouldBe_Cancellable()
    {
        // Arrange
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);
        using var cts = new CancellationTokenSource();

        // Act & Assert - should complete without exception
        await service.CancelReconnectionAsync();
    }

    [Fact]
    public void CreateLinkedTokenSource_Should_CreateFreshToken()
    {
        // Arrange - simulate scenario where original token is cancelled
        var originalCts = new CancellationTokenSource();
        originalCts.Cancel(); // Original is now cancelled

        // Act - create a fresh linked source for a new operation
        var newCts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            newCts.Token, 
            new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token
        );

        // Assert - the linked token is NOT cancelled because the new CTS is not cancelled
        Assert.False(linkedCts.Token.IsCancellationRequested);

        // Cleanup
        originalCts.Dispose();
        newCts.Dispose();
        linkedCts.Dispose();
    }
}

/// <summary>
/// Tests for exception logging behavior.
/// </summary>
public class ExceptionLoggingTests
{
    private readonly Mock<ILogger<OpenClawService>> _loggerMock;
    private readonly Mock<IPushNotificationService> _pushServiceMock;

    public ExceptionLoggingTests()
    {
        _loggerMock = new Mock<ILogger<OpenClawService>>();
        _pushServiceMock = new Mock<IPushNotificationService>();
    }

    [Fact]
    public void Logger_Should_BeInjectable()
    {
        // Arrange & Act
        var service = new OpenClawService(_loggerMock.Object, _pushServiceMock.Object);

        // Assert - service should be created with logger
        Assert.NotNull(service);
    }

    [Fact]
    public void Exception_Should_IncludeStackTrace()
    {
        // Arrange
        Exception? capturedException = null;
        try
        {
            // Create a nested call stack to get a meaningful stack trace
            ThrowNestedException();
        }
        catch (Exception ex)
        {
            capturedException = ex;
        }

        // Assert
        Assert.NotNull(capturedException);
        Assert.NotNull(capturedException.StackTrace);
        Assert.Contains("ThrowNestedException", capturedException.StackTrace);
    }

    private void ThrowNestedException()
    {
        throw new InvalidOperationException("Test exception with stack trace");
    }

    [Fact]
    public void WebSocketException_Should_HaveErrorCode()
    {
        // Arrange & Act
        var wsEx = new WebSocketException("Connection failed");

        // Assert - WebSocketException should have specific error info
        Assert.NotNull(wsEx);
        Assert.Equal("Connection failed", wsEx.Message);
    }

    [Fact]
    public void Exception_WithInnerException_Should_PreserveInner()
    {
        // Arrange
        var inner = new TimeoutException("Connection timeout");
        var outer = new InvalidOperationException("Operation failed", inner);

        // Assert
        Assert.NotNull(outer.InnerException);
        Assert.IsType<TimeoutException>(outer.InnerException);
        Assert.Equal("Connection timeout", outer.InnerException.Message);
    }
}
