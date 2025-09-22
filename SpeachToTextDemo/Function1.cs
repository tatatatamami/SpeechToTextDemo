using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SpeachToTextDemo.Services;
using System;
using System.Collections.Generic;

namespace SpeachToTextDemo;

public class Function1
{
    private readonly ILogger<Function1> _logger;
    private readonly SpeechBatchClient _speechBatchClient;

    public Function1(ILogger<Function1> logger, SpeechBatchClient speechBatchClient)
    {
        _logger = logger;
        _speechBatchClient = speechBatchClient;
    }

    [Function(nameof(Function1))]
    public async Task Run([BlobTrigger("audio-in/{name}", Source = BlobTriggerSource.EventGrid, Connection = "")] Stream stream, string name)
    {
        using var blobStreamReader = new StreamReader(stream);
        var content = await blobStreamReader.ReadToEndAsync();
        _logger.LogInformation("C# Blob Trigger (using Event Grid) processed blob\n Name: {name} \n Data: {content}", name, content);

        // Example usage of SpeechBatchClient (commented out to avoid actual API calls during build)
        // try
        // {
        //     var contentUrls = new List<Uri>
        //     {
        //         new Uri($"https://example.blob.core.windows.net/audio-in/{name}?sas-token")
        //     };
        //     
        //     var jobInfo = await _speechBatchClient.CreateTranscriptionAsync(
        //         contentUrls,
        //         $"Transcription for {name}");
        //     
        //     _logger.LogInformation("Created transcription job: {JobId} at {JobUrl}", jobInfo.JobId, jobInfo.JobUrl);
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "Failed to create transcription job for {name}", name);
        // }
    }
}
