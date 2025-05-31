using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Net;
using System.Collections.Generic;

namespace ClearGlass.Services
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private const string GITHUB_API_URL = "https://api.github.com/repos/daniel1017/ClearGlass/releases/latest";
        private const string GITHUB_RELEASE_URL = "https://github.com/daniel1017/ClearGlass/releases/latest";

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ClearGlass");
        }

        public async Task<(bool hasUpdate, string latestVersion, string downloadUrl)> CheckForUpdates()
        {
            try
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                var releaseInfo = JsonSerializer.Deserialize<GitHubRelease>(response);

                if (releaseInfo == null || string.IsNullOrEmpty(releaseInfo.TagName))
                    return (false, null, null);

                // Remove 'v' prefix if present and parse version
                var latestVersionStr = releaseInfo.TagName.TrimStart('v');
                if (Version.TryParse(latestVersionStr, out Version latestVersion))
                {
                    if (latestVersion > currentVersion)
                    {
                        // Find the setup installer asset
                        var setupAsset = releaseInfo.Assets?.Find(a => a.Name.EndsWith("Setup.exe"));
                        return (true, latestVersionStr, setupAsset?.BrowserDownloadUrl);
                    }
                }

                return (false, null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                return (false, null, null);
            }
        }

        public async Task DownloadAndInstallUpdate(string downloadUrl)
        {
            if (string.IsNullOrEmpty(downloadUrl))
                throw new ArgumentException("Download URL cannot be null or empty");

            try
            {
                // Create temp directory if it doesn't exist
                var tempDir = Path.Combine(Path.GetTempPath(), "ClearGlass");
                Directory.CreateDirectory(tempDir);

                // Download the installer
                var installerPath = Path.Combine(tempDir, "ClearGlassSetup.exe");
                using (var response = await _httpClient.GetAsync(downloadUrl))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fileStream = File.Create(installerPath))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                }

                // Launch the installer
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true,
                    Verb = "runas" // Run as administrator
                });

                // Exit the current application
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download and install update: {ex.Message}");
            }
        }

        private class GitHubRelease
        {
            public string TagName { get; set; }
            public List<GitHubAsset> Assets { get; set; }
        }

        private class GitHubAsset
        {
            public string Name { get; set; }
            public string BrowserDownloadUrl { get; set; }
        }
    }
} 