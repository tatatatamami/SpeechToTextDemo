using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SpeachToTextDemo.Services;

namespace SpeachToTextDemo.TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== SpeechBatchClient テストコンソール ===");
            Console.WriteLine();

            try
            {
                // Configuration setup (実際のAPIキーとエンドポイントは使用しません)
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SPEECH__Endpoint"] = "https://test-speech.cognitiveservices.azure.com",
                        ["SPEECH__Key"] = "test-key-placeholder",
                        ["TRANSCRIPT__DestinationContainerUrl"] = "https://teststorage.blob.core.windows.net/transcripts"
                    })
                    .Build();

                using var httpClient = new HttpClient();
                var speechClient = new SpeechBatchClient(httpClient, configuration);

                // Test input validation
                Console.WriteLine("1. 入力検証テスト");
                Console.WriteLine("   - 空のURLリストでテスト中...");
                
                try
                {
                    await speechClient.CreateTranscriptionAsync(new List<Uri>(), "Test Job");
                    Console.WriteLine("   ✗ 期待された例外が発生しませんでした");
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"   ✓ 正常に例外をキャッチしました: {ex.Message}");
                }

                Console.WriteLine("   - null URLリストでテスト中...");
                try
                {
                    await speechClient.CreateTranscriptionAsync(null!, "Test Job");
                    Console.WriteLine("   ✗ 期待された例外が発生しませんでした");
                }
                catch (ArgumentNullException ex)
                {
                    Console.WriteLine($"   ✓ 正常に例外をキャッチしました: {ex.ParamName}");
                }

                Console.WriteLine("   - 空の表示名でテスト中...");
                try
                {
                    var testUrls = new List<Uri> { new Uri("https://test.com/audio.wav") };
                    await speechClient.CreateTranscriptionAsync(testUrls, "");
                    Console.WriteLine("   ✗ 期待された例外が発生しませんでした");
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"   ✓ 正常に例外をキャッチしました: {ex.Message}");
                }

                Console.WriteLine("   - 相対URIでテスト中...");
                try
                {
                    var testUrls = new List<Uri> { new Uri("/relative/path.wav", UriKind.Relative) };
                    await speechClient.CreateTranscriptionAsync(testUrls, "Test Job");
                    Console.WriteLine("   ✗ 期待された例外が発生しませんでした");
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"   ✓ 正常に例外をキャッチしました: {ex.Message}");
                }

                Console.WriteLine();
                Console.WriteLine("2. 設定値の確認");
                Console.WriteLine($"   - Speech Endpoint: {configuration["SPEECH__Endpoint"]}");
                Console.WriteLine($"   - Destination Container: {configuration["TRANSCRIPT__DestinationContainerUrl"]}");

                Console.WriteLine();
                Console.WriteLine("3. 列挙型の確認");
                Console.WriteLine("   - PunctuationMode values:");
                foreach (var value in Enum.GetValues<PunctuationMode>())
                {
                    Console.WriteLine($"     • {value} ({(int)value})");
                }

                Console.WriteLine("   - ProfanityFilterMode values:");
                foreach (var value in Enum.GetValues<ProfanityFilterMode>())
                {
                    Console.WriteLine($"     • {value} ({(int)value})");
                }

                Console.WriteLine();
                Console.WriteLine("4. 実際のAPI呼び出し（テスト用エンドポイントなので失敗します）");
                try
                {
                    var contentUrls = new List<Uri>
                    {
                        new Uri("https://example.blob.core.windows.net/audio/sample1.wav"),
                        new Uri("https://example.blob.core.windows.net/audio/sample2.wav")
                    };

                    Console.WriteLine("   - API呼び出しを試行中...");
                    var result = await speechClient.CreateTranscriptionAsync(
                        contentUrls,
                        "Test Transcription Job",
                        "ja-JP",
                        true,
                        PunctuationMode.DictatedAndAutomatic,
                        ProfanityFilterMode.Masked);

                    Console.WriteLine($"   ✓ ジョブID: {result.JobId}");
                    Console.WriteLine($"   ✓ ジョブURL: {result.JobUrl}");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"   ✓ 期待されたHTTP例外: {ex.Message}");
                    Console.WriteLine("     （テスト用エンドポイントなので正常な動作です）");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ! 予期しない例外: {ex.GetType().Name}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"致命的エラー: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("=== テスト完了 ===");
            Console.WriteLine("Enterキーを押して終了してください...");
            Console.ReadLine();
        }
    }
}
