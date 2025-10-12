using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using SpeachToTextDemo.Services;
using Xunit;

namespace SpeachToTextDemo.Tests
{
    public class SpeechBatchClientTests
    {
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly SpeechBatchClient _speechBatchClient;

        public SpeechBatchClientTests()
        {
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
            _configurationMock = new Mock<IConfiguration>();

            // Setup configuration mock
            _configurationMock.Setup(c => c["SPEECH__Endpoint"]).Returns("https://test-speech-endpoint.cognitiveservices.azure.com");
            _configurationMock.Setup(c => c["SPEECH__Key"]).Returns("test-speech-key");
            _configurationMock.Setup(c => c["TRANSCRIPT__DestinationContainerUrl"]).Returns("https://test-storage.blob.core.windows.net/transcripts");

            _speechBatchClient = new SpeechBatchClient(_httpClient, _configurationMock.Object);
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenHttpClientIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SpeechBatchClient(null!, _configurationMock.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenConfigurationIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SpeechBatchClient(_httpClient, null!));
        }

        [Fact]
        public void Constructor_ShouldThrowInvalidOperationException_WhenSpeechEndpointIsNull()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["SPEECH__Endpoint"]).Returns((string)null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new SpeechBatchClient(_httpClient, configMock.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowInvalidOperationException_WhenSpeechKeyIsNull()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["SPEECH__Endpoint"]).Returns("https://test-endpoint.com");
            configMock.Setup(c => c["SPEECH__Key"]).Returns((string)null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new SpeechBatchClient(_httpClient, configMock.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowInvalidOperationException_WhenDestinationContainerUrlIsNull()
        {
            // Arrange
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["SPEECH__Endpoint"]).Returns("https://test-endpoint.com");
            configMock.Setup(c => c["SPEECH__Key"]).Returns("test-key");
            configMock.Setup(c => c["TRANSCRIPT__DestinationContainerUrl"]).Returns((string)null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new SpeechBatchClient(_httpClient, configMock.Object));
        }

        [Fact]
        public async Task CreateTranscriptionAsync_ShouldThrowArgumentNullException_WhenContentUrlsIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _speechBatchClient.CreateTranscriptionAsync(null!, "Test"));
        }

        [Fact]
        public async Task CreateTranscriptionAsync_ShouldThrowArgumentException_WhenContentUrlsIsEmpty()
        {
            // Arrange
            var emptyUrls = new List<Uri>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _speechBatchClient.CreateTranscriptionAsync(emptyUrls, "Test"));
        }

        [Fact]
        public async Task CreateTranscriptionAsync_ShouldThrowArgumentException_WhenDisplayNameIsEmpty()
        {
            // Arrange
            var contentUrls = new List<Uri> { new Uri("https://test.com/audio.wav") };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _speechBatchClient.CreateTranscriptionAsync(contentUrls, ""));
        }

        [Fact]
        public async Task CreateTranscriptionAsync_ShouldThrowArgumentException_WhenUrlIsNotAbsolute()
        {
            // Arrange
            var contentUrls = new List<Uri> { new Uri("/relative/path.wav", UriKind.Relative) };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _speechBatchClient.CreateTranscriptionAsync(contentUrls, "Test"));
        }

        [Fact]
        public async Task CreateTranscriptionAsync_ShouldReturnTranscriptionJobInfo_WhenSuccessful()
        {
            // Arrange
            var contentUrls = new List<Uri> 
            { 
                new Uri("https://test.blob.core.windows.net/audio/file1.wav"),
                new Uri("https://test.blob.core.windows.net/audio/file2.wav")
            };
            var displayName = "Test Transcription Job";
            var jobId = "12345678-1234-1234-1234-123456789abc";
            var jobUrl = new Uri($"https://test-speech-endpoint.cognitiveservices.azure.com/speechtotext/v3.2/transcriptions/{jobId}");

            // Setup HTTP response
            var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Headers = { Location = jobUrl }
            };

            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponseMessage);

            // Act
            var result = await _speechBatchClient.CreateTranscriptionAsync(
                contentUrls, 
                displayName, 
                "ja-JP", 
                true, 
                PunctuationMode.DictatedAndAutomatic, 
                ProfanityFilterMode.Masked);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(jobId, result.JobId);
            Assert.Equal(jobUrl, result.JobUrl);
            Assert.Equal(displayName, result.DisplayName);
            Assert.Equal("ja-JP", result.Locale);
            Assert.True(result.DiarizationEnabled);
            Assert.Equal(PunctuationMode.DictatedAndAutomatic, result.PunctuationMode);
            Assert.Equal(ProfanityFilterMode.Masked, result.ProfanityFilterMode);
            Assert.Equal(contentUrls, result.ContentUrls);
            Assert.Equal(new Uri("https://test-storage.blob.core.windows.net/transcripts"), result.DestinationContainerUrl);

            // Verify HTTP request was made correctly
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri.ToString() == "https://test-speech-endpoint.cognitiveservices.azure.com/speechtotext/v3.2/transcriptions" &&
                    req.Headers.GetValues("Ocp-Apim-Subscription-Key").Contains("test-speech-key") &&
                    req.Headers.GetValues("Accept").Contains("application/json")),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task CreateTranscriptionAsync_ShouldSendCorrectRequestBody()
        {
            // Arrange
            var contentUrls = new List<Uri> { new Uri("https://test.com/audio.wav") };
            var displayName = "Test Job";

            var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Headers = { Location = new Uri("https://test.com/job/123") }
            };

            string capturedRequestBody = null;
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) => 
                {
                    if (req.Content != null)
                    {
                        capturedRequestBody = await req.Content.ReadAsStringAsync();
                    }
                })
                .ReturnsAsync(httpResponseMessage);

            // Act
            await _speechBatchClient.CreateTranscriptionAsync(contentUrls, displayName);

            // Assert
            Assert.NotNull(capturedRequestBody);

            var requestJson = JsonDocument.Parse(capturedRequestBody);

            Assert.Equal(displayName, requestJson.RootElement.GetProperty("displayName").GetString());
            Assert.Equal("ja-JP", requestJson.RootElement.GetProperty("locale").GetString());
            
            var contentUrlsArray = requestJson.RootElement.GetProperty("contentUrls");
            Assert.Single(contentUrlsArray.EnumerateArray());
            Assert.Equal("https://test.com/audio.wav", contentUrlsArray[0].GetString());

            var properties = requestJson.RootElement.GetProperty("properties");
            Assert.Equal("https://test-storage.blob.core.windows.net/transcripts", 
                properties.GetProperty("destinationContainerUrl").GetString());
            Assert.True(properties.GetProperty("diarizationEnabled").GetBoolean());
            Assert.Equal("DictatedAndAutomatic", properties.GetProperty("punctuationMode").GetString());
            Assert.Equal("Masked", properties.GetProperty("profanityFilterMode").GetString());
        }

        [Fact]
        public async Task CreateTranscriptionAsync_ShouldThrowInvalidOperationException_WhenLocationHeaderMissing()
        {
            // Arrange
            var contentUrls = new List<Uri> { new Uri("https://test.com/audio.wav") };
            var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.Accepted); // No Location header

            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponseMessage);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _speechBatchClient.CreateTranscriptionAsync(contentUrls, "Test"));
        }
    }
}