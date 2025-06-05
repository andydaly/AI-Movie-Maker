using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AI_Actions
{
    public class AIVideoGenerator
    {
        private string endpointUrl = String.Empty;
        private string apiversionUrl = String.Empty;
        private string ApiKey = String.Empty;
        public AIVideoGenerator(string endpoint, string apiKey, string apiVersion)
        {
            endpointUrl = $"{endpoint}/openai/v1/video/generations/";
            apiversionUrl = $"?api-version={apiVersion}";
            ApiKey = apiKey;
        }


        public async Task<string> GenerateVideoAsync(string prompt, string filename, int duration = 5, int width = 1280, int height = 720)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Prompt cannot be null or empty.", nameof(prompt));
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("api-key", ApiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var requestBody = new
            {
                prompt = prompt,
                n_seconds = duration,
                width = width,
                height = height,
                model = "sora"
            };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var createUrl = $"{endpointUrl}jobs{apiversionUrl}";
            var response = await client.PostAsync(createUrl, content);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            string jobId = doc.RootElement.GetProperty("id").GetString();
            string statusUrl = $"{endpointUrl}jobs/{jobId}{apiversionUrl}";
            string status = String.Empty;
            JsonElement statusResponse = default;
            do
            {
                await Task.Delay(5000);
                var statusResult = await client.GetStringAsync(statusUrl);
                statusResponse = JsonDocument.Parse(statusResult).RootElement;
                status = statusResponse.GetProperty("status").GetString();
            }
            while (status != "succeeded" && status != "failed" && status != "cancelled");


            if (status == "succeeded")
            {
                if (statusResponse.TryGetProperty("generations", out var generations) && generations.GetArrayLength() > 0)
                {
                    string generationId = generations[0].GetProperty("id").GetString();
                    string videoUrl = $"{endpointUrl}/{generationId}/content/video{apiversionUrl}";
                    var videoResponse = await client.GetAsync(videoUrl);
                    if (videoResponse.IsSuccessStatusCode)
                    {
                        var videoBytes = await videoResponse.Content.ReadAsByteArrayAsync();
                        string fileNameWithExtension = $"{filename}.mp4";
                        string filePath = Path.Combine(Path.GetTempPath(), fileNameWithExtension);
                        await File.WriteAllBytesAsync(filePath, videoBytes);
                        return filePath;
                    }

                    return String.Empty;

                }
                else
                {
                    throw new Exception("No generations found in job result.");
                }
            }
            else
            {
                throw new Exception($"Job didn't succeed. Status: {status}");
            }
        }
    }
}
