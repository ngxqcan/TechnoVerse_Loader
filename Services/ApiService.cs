using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TechnoVerseLoader.Services
{
    public class ProductInfo
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }

    public class SubscriptionInfo
    {
        [JsonPropertyName("pricingLabel")]
        public string PricingLabel { get; set; } = "License";

        [JsonPropertyName("expiresAt")]
        public string? ExpiresAt { get; set; }

        [JsonPropertyName("isLifetime")]
        public bool IsLifetime { get; set; }
    }

    public class PayloadInfo
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = "";

        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("fileSizeFormatted")]
        public string FileSizeFormatted { get; set; } = "";

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = "";

        [JsonPropertyName("updatedAt")]
        public string UpdatedAt { get; set; } = "";

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = "";

        [JsonPropertyName("password")]
        public string? Password { get; set; }
    }

    public class AuthResponse
    {
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("product")]
        public ProductInfo? Product { get; set; }

        [JsonPropertyName("subscription")]
        public SubscriptionInfo? Subscription { get; set; }

        [JsonPropertyName("payload")]
        public PayloadInfo? Payload { get; set; }

        [JsonPropertyName("boundHwid")]
        public string? BoundHwid { get; set; }
    }

    public class DiscordUserProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; } = "";
    }

    public class PurchasedProductItem
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "active"; // active, unactivated, hwid_mismatch

        [JsonPropertyName("canActivate")]
        public bool CanActivate { get; set; }

        [JsonPropertyName("boundHwid")]
        public string? BoundHwid { get; set; }

        [JsonPropertyName("product")]
        public ProductInfo? Product { get; set; }

        [JsonPropertyName("subscription")]
        public SubscriptionInfo? Subscription { get; set; }

        [JsonPropertyName("payload")]
        public PayloadInfo? Payload { get; set; }
    }

    public class DiscordAuthResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("user")]
        public DiscordUserProfile? User { get; set; }

        [JsonPropertyName("products")]
        public System.Collections.Generic.List<PurchasedProductItem> Products { get; set; } = new();
    }

    public class ApiService
    {
        private static readonly HttpClient HttpClient = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            Proxy = null
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        static ApiService()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TechnoVerse-Loader/1.0");
        }

        public async Task<AuthResponse> AuthenticateAsync(string serverUrl, string key, string hwid)
        {
            serverUrl = NormalizeUrl(serverUrl);
            string endpoint = $"{serverUrl}/api/loader/auth";

            var payload = new
            {
                key = key.Trim(),
                hwid = hwid.Trim()
            };

            string jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await HttpClient.PostAsync(endpoint, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                var authRes = JsonSerializer.Deserialize<AuthResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (authRes != null)
                {
                    return authRes;
                }

                return new AuthResponse
                {
                    Valid = false,
                    Error = $"Invalid response from server (HTTP {(int)response.StatusCode})"
                };
            }
            catch (HttpRequestException ex)
            {
                return new AuthResponse
                {
                    Valid = false,
                    Error = $"Cannot connect to server ({serverUrl}). Detail: {ex.Message}"
                };
            }
            catch (TaskCanceledException)
            {
                return new AuthResponse
                {
                    Valid = false,
                    Error = "Server connection timeout. Please try again."
                };
            }
            catch (Exception ex)
            {
                return new AuthResponse
                {
                    Valid = false,
                    Error = $"Exception: {ex.Message}"
                };
            }
        }

        public async Task<DiscordAuthResponse> AuthenticateWithDiscordAsync(string serverUrl, string discordId, string hwid)
        {
            serverUrl = NormalizeUrl(serverUrl);
            string endpoint = $"{serverUrl}/api/loader/discord/auth";

            var payload = new
            {
                discordId = discordId.Trim(),
                hwid = hwid.Trim()
            };

            string jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await HttpClient.PostAsync(endpoint, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                var authRes = JsonSerializer.Deserialize<DiscordAuthResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (authRes != null)
                {
                    return authRes;
                }

                return new DiscordAuthResponse
                {
                    Success = false,
                    Error = $"Invalid response from server (HTTP {(int)response.StatusCode})"
                };
            }
            catch (Exception ex)
            {
                return new DiscordAuthResponse
                {
                    Success = false,
                    Error = $"Connection error to server ({serverUrl}): {ex.Message}"
                };
            }
        }

        public async Task<(bool success, string message)> ActivateKeyAsync(string serverUrl, string key, string discordId, string hwid)
        {
            serverUrl = NormalizeUrl(serverUrl);
            string endpoint = $"{serverUrl}/api/loader/activate-key";

            var payload = new
            {
                key = key.Trim(),
                discordId = discordId.Trim(),
                hwid = hwid.Trim()
            };

            string jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await HttpClient.PostAsync(endpoint, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseBody);
                bool success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
                string message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

                return (success, message);
            }
            catch (Exception ex)
            {
                return (false, "Activation error: " + ex.Message);
            }
        }

        public async Task<bool> ConsumeKeyAsync(string serverUrl, string key, string discordId, string hwid)
        {
            serverUrl = NormalizeUrl(serverUrl);
            string endpoint = $"{serverUrl}/api/loader/consume-key";

            var payload = new
            {
                key = key.Trim(),
                discordId = discordId.Trim(),
                hwid = hwid.Trim()
            };

            try
            {
                string jsonContent = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await HttpClient.PostAsync(endpoint, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool success, long pingMs, string message)> PingServerAsync(string serverUrl)
        {
            serverUrl = NormalizeUrl(serverUrl);
            string endpoint = $"{serverUrl}/api/loader/check-updates";

            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var res = await HttpClient.GetAsync(endpoint, cts.Token);
                sw.Stop();

                if (res.IsSuccessStatusCode)
                {
                    return (true, sw.ElapsedMilliseconds, $"Connected ({sw.ElapsedMilliseconds}ms)");
                }
                else
                {
                    return (false, sw.ElapsedMilliseconds, $"Server responded with HTTP {(int)res.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (false, 0, $"Cannot connect: {ex.Message}");
            }
        }

        private static string NormalizeUrl(string url)
        {
            return "https://technoverse-backend-production.up.railway.app";
        }
    }
}
