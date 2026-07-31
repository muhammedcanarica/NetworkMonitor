using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Controllers;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Controllers;

public sealed class ConfigBackupControllerTests
{
    [Fact]
    public async Task GetRunningConfiguration_ReturnsBackupResponse()
    {
        var response = new ConfigBackupResponse(
            "192.168.1.10",
            ConfigBackupVendor.CiscoIos,
            "hostname core-switch",
            DateTimeOffset.UtcNow,
            "192.168.1.10-running-config-2026-07-31.txt");
        var controller = new ConfigBackupController(
            new StubConfigBackupService((_, _) => Task.FromResult(response)),
            new StubConfigBackupStorageService());

        var action = await controller.GetRunningConfiguration(CreateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task GetRunningConfiguration_MapsValidationFailureToBadRequest()
    {
        var controller = new ConfigBackupController(
            new StubConfigBackupService((_, _) => throw new ConfigBackupValidationException("Invalid IP address.")),
            new StubConfigBackupStorageService());

        var action = await controller.GetRunningConfiguration(CreateRequest(), CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal("Invalid IP address.", Assert.IsType<ProblemDetails>(result.Value).Detail);
    }

    [Theory]
    [InlineData(ConfigBackupErrorKind.Authentication, StatusCodes.Status401Unauthorized)]
    [InlineData(ConfigBackupErrorKind.Connection, StatusCodes.Status502BadGateway)]
    [InlineData(ConfigBackupErrorKind.ConnectionTimeout, StatusCodes.Status504GatewayTimeout)]
    [InlineData(ConfigBackupErrorKind.CommandTimeout, StatusCodes.Status504GatewayTimeout)]
    public async Task GetRunningConfiguration_MapsOperationFailuresToExpectedStatus(
        ConfigBackupErrorKind kind,
        int expectedStatus)
    {
        var controller = new ConfigBackupController(
            new StubConfigBackupService((_, _) => throw new ConfigBackupOperationException(kind, "Safe error message.")),
            new StubConfigBackupStorageService());

        var action = await controller.GetRunningConfiguration(CreateRequest(), CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal("Safe error message.", Assert.IsType<ProblemDetails>(result.Value).Detail);
    }

    [Theory]
    [InlineData("validation", StatusCodes.Status400BadRequest)]
    [InlineData("limit", StatusCodes.Status413PayloadTooLarge)]
    [InlineData("not-found", StatusCodes.Status404NotFound)]
    public async Task Save_MapsStorageFailuresToExpectedStatus(string failure, int expectedStatus)
    {
        Exception exception = failure switch
        {
            "validation" => new ConfigBackupStorageValidationException("Invalid backup."),
            "limit" => new ConfigBackupSizeLimitException("Backup too large."),
            _ => new ConfigBackupNotFoundException(42)
        };
        var controller = new ConfigBackupController(
            new StubConfigBackupService((_, _) => throw new NotImplementedException()),
            new StubConfigBackupStorageService(exception));

        var action = await controller.Save(new SaveConfigBackupRequest(), CancellationToken.None);

        var result = Assert.IsAssignableFrom<ObjectResult>(action.Result);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.IsType<ProblemDetails>(result.Value);
    }

    private static ConfigBackupRequest CreateRequest() => new()
    {
        IpAddress = "192.168.1.10",
        Username = "operator",
        Password = "not-returned"
    };

    private sealed class StubConfigBackupService(
        Func<ConfigBackupRequest, CancellationToken, Task<ConfigBackupResponse>> getConfiguration) : IConfigBackupService
    {
        public Task<ConfigBackupResponse> GetRunningConfigurationAsync(
            ConfigBackupRequest request,
            CancellationToken cancellationToken)
        {
            return getConfiguration(request, cancellationToken);
        }
    }

    private sealed class StubConfigBackupStorageService(Exception? saveException = null) : IConfigBackupStorageService
    {
        public Task<SaveConfigBackupResponse> SaveAsync(
            SaveConfigBackupRequest request,
            CancellationToken cancellationToken) => saveException is null
                ? throw new NotImplementedException()
                : Task.FromException<SaveConfigBackupResponse>(saveException);

        public Task<IReadOnlyList<ConfigBackupListItemResponse>> ListAsync(
            int? deviceId,
            CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<ConfigBackupDetailResponse> GetByIdAsync(int id, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<ConfigBackupComparisonResponse> CompareAsync(
            int fromId,
            int toId,
            CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
