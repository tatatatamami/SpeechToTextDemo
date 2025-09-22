using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace SpeachToTextDemo.Services
{
    public enum PunctuationMode
    {
        None,
        Dictated,
        DictatedAndAutomatic
    }

    public enum ProfanityFilterMode
    {
        None,
        Masked,
        Removed
    }

    public sealed record TranscriptionJobInfo(
        string JobId,
        Uri JobUrl,
        Uri FilesUrl,
        string DisplayName,
        string Locale,
        bool DiarizationEnabled,
        PunctuationMode PunctuationMode,
        ProfanityFilterMode ProfanityFilterMode,
        IReadOnlyList<Uri> ContentUrls,
        Uri DestinationContainerUrl,
        DateTimeOffset SubmittedAtUtc
    );

    public class SpeechBatchClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _speechEndpoint;
        private readonly string _speechKey;
        private readonly string _destinationContainerUrl;

        public SpeechBatchClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _speechEndpoint = _configuration["SPEECH__Endpoint"] ?? 
                throw new InvalidOperationException("SPEECH__Endpoint configuration is required");
            _speechKey = _configuration["SPEECH__Key"] ?? 
                throw new InvalidOperationException("SPEECH__Key configuration is required");
            _destinationContainerUrl = _configuration["TRANSCRIPT__DestinationContainerUrl"] ?? 
                throw new InvalidOperationException("TRANSCRIPT__DestinationContainerUrl configuration is required");
        }

        public async Task<TranscriptionJobInfo> CreateTranscriptionAsync(
            IEnumerable<Uri> contentUrls,
            string displayName,
            string locale = "ja-JP",
            bool diarizationEnabled = true,
            PunctuationMode punctuationMode = PunctuationMode.DictatedAndAutomatic,
            ProfanityFilterMode profanityFilterMode = ProfanityFilterMode.Masked,
            CancellationToken ct = default)
        {
            // Input validation
            var contentUrlsList = contentUrls?.ToList() ?? throw new ArgumentNullException(nameof(contentUrls));
            if (!contentUrlsList.Any())
                throw new ArgumentException("contentUrls cannot be empty", nameof(contentUrls));
            
            foreach (var url in contentUrlsList)
            {
                if (!url.IsAbsoluteUri)
                    throw new ArgumentException($"All content URLs must be absolute URIs: {url}", nameof(contentUrls));
            }

            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("displayName cannot be null or empty", nameof(displayName));

            // Prepare request
            var requestUrl = $"{_speechEndpoint.TrimEnd('/')}/speechtotext/v3.2/transcriptions";
            
            var requestBody = new
            {
                displayName = displayName,
                locale = locale,
                contentUrls = contentUrlsList.Select(u => u.ToString()).ToArray(),
                properties = new
                {
                    destinationContainerUrl = _destinationContainerUrl,
                    diarizationEnabled = diarizationEnabled,
                    punctuationMode = punctuationMode.ToString(),
                    profanityFilterMode = profanityFilterMode.ToString()
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };
            
            request.Headers.Add("Ocp-Apim-Subscription-Key", _speechKey);
            request.Headers.Add("Accept", "application/json");

            // Send request
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            // Extract job information from response
            var jobUrl = response.Headers.Location ?? 
                throw new InvalidOperationException("Location header not found in response");

            var jobId = jobUrl.Segments[^1]; // Last segment of the URL
            var filesUrl = new Uri(jobUrl, "./files");
            var submittedAtUtc = DateTimeOffset.UtcNow;

            return new TranscriptionJobInfo(
                JobId: jobId,
                JobUrl: jobUrl,
                FilesUrl: filesUrl,
                DisplayName: displayName,
                Locale: locale,
                DiarizationEnabled: diarizationEnabled,
                PunctuationMode: punctuationMode,
                ProfanityFilterMode: profanityFilterMode,
                ContentUrls: contentUrlsList.AsReadOnly(),
                DestinationContainerUrl: new Uri(_destinationContainerUrl),
                SubmittedAtUtc: submittedAtUtc
            );
        }
    }
}
