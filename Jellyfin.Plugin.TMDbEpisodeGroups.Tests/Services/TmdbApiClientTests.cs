using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.TMDbEpisodeGroups.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Tests.Services;

public class TmdbApiClientTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<TmdbApiClient>> _loggerMock;
    private const string TestApiKey = "test-api-key-12345";

    public TmdbApiClientTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<TmdbApiClient>>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    [Fact]
    public async Task GetEpisodeGroupsAsync_WithValidApiKey_ReturnsEpisodeGroups()
    {
        // Arrange
        var tmdbSeriesId = "61709";

        var responseJson = @"{""results"":[{""id"":""651d0a14c50ad2010bfffd7f"",""name"":""Dragon Ball Z Kai - DVD Order"",""type"":2,""description"":""DVD Order"",""episode_count"":98,""group_count"":4}]}";
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var client = new TmdbApiClient(_httpClientFactoryMock.Object, _loggerMock.Object, () => TestApiKey);

        // Act
        var result = await client.GetEpisodeGroupsAsync(tmdbSeriesId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().HaveCount(1);
        result.Results[0].Id.Should().Be("651d0a14c50ad2010bfffd7f");
        result.Results[0].Name.Should().Be("Dragon Ball Z Kai - DVD Order");
        result.Results[0].Type.Should().Be(2);
        result.Results[0].EpisodeCount.Should().Be(98);
    }

    [Fact]
    public async Task GetEpisodeGroupsAsync_WithMissingApiKey_ThrowsException()
    {
        // Arrange
        var tmdbSeriesId = "61709";
        var client = new TmdbApiClient(_httpClientFactoryMock.Object, _loggerMock.Object, () => string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetEpisodeGroupsAsync(tmdbSeriesId, CancellationToken.None));
    }

    [Fact]
    public async Task GetEpisodeGroupsAsync_SendsCorrectHttpRequest()
    {
        // Arrange
        var tmdbSeriesId = "61709";
        HttpRequestMessage? capturedRequest = null;

        var responseJson = @"{""results"":[]}";
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var client = new TmdbApiClient(_httpClientFactoryMock.Object, _loggerMock.Object, () => TestApiKey);

        // Act
        await client.GetEpisodeGroupsAsync(tmdbSeriesId, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Contain($"/tv/{tmdbSeriesId}/episode_groups");
        capturedRequest.RequestUri.ToString().Should().Contain($"api_key={TestApiKey}");
    }

    [Fact]
    public async Task GetEpisodeGroupDetailsAsync_WithValidApiKey_ReturnsDetails()
    {
        // Arrange
        var episodeGroupId = "651d0a14c50ad2010bfffd7f";

        var responseJson = @"{""id"":""651d0a14c50ad2010bfffd7f"",""name"":""DVD Order"",""type"":2,""description"":""DVD release order"",""groups"":[{""id"":""group1"",""name"":""Volume 1"",""order"":1,""episodes"":[{""id"":123,""name"":""Episode 1"",""overview"":""First episode"",""episode_number"":1,""season_number"":1,""order"":1}]}]}";
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
           Content = new StringContent(responseJson)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var client = new TmdbApiClient(_httpClientFactoryMock.Object, _loggerMock.Object, () => TestApiKey);

        // Act
        var result = await client.GetEpisodeGroupDetailsAsync(episodeGroupId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(episodeGroupId);
        result.Name.Should().Be("DVD Order");
        result.Groups.Should().HaveCount(1);
        result.Groups[0].Episodes.Should().HaveCount(1);
        result.Groups[0].Episodes[0].Name.Should().Be("Episode 1");
    }

    [Fact]
    public async Task GetEpisodeGroupDetailsAsync_WithMissingApiKey_ThrowsException()
    {
        // Arrange
        var episodeGroupId = "651d0a14c50ad2010bfffd7f";
        var client = new TmdbApiClient(_httpClientFactoryMock.Object, _loggerMock.Object, () => string.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await client.GetEpisodeGroupDetailsAsync(episodeGroupId, CancellationToken.None));
    }
}
