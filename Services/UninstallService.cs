using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Windows;
using System.Security.Cryptography;

namespace ClearGlass.Services
{
    public class UninstallService
    {
        private readonly WingetService _wingetService;
        private readonly Dictionary<string, HashSet<string>> _initialFileSnapshot;
        private readonly Dictionary<string, DateTime> _initialFileTimestamps;

        public UninstallService(WingetService wingetService)
        {
            _wingetService = wingetService;
            _initialFileSnapshot = new Dictionary<string, HashSet<string>>();
            _initialFileTimestamps = new Dictionary<string, DateTime>();
        }

        public async Task UninstallAppThoroughly(string packageId, string appName, IProgress<string> progress, bool createRestorePoint = true, bool autoRemoveLeftovers = false)
        {
            if (packageId != null && packageId.Contains("Steam App "))
            {
                progress.Report("Steam games must be uninstalled through Steam. Skipping...");
                return;
            }

            try
            {
                // Step 1: Take initial snapshot of relevant directories
                progress.Report("Taking initial system snapshot...");
                await TakeSystemSnapshot(appName);

                // Step 2: Create a restore point if requested
                if (createRestorePoint)
                {
                    progress.Report("Creating system restore point...");
                    await CreateSystemRestorePoint($"Before uninstalling {appName}");
                }

                // Step 3: Use winget to uninstall
                progress.Report($"Uninstalling {appName} using winget...");
                await _wingetService.UninstallApp(packageId);

                // Step 4: Wait a moment for file operations to complete
                progress.Report("Waiting for uninstaller to complete...");
                await Task.Delay(2000); // Wait 2 seconds

                // Step 5: Scan for leftover files
                progress.Report("Performing deep scan for leftover files...");
                var leftoverFiles = await ScanForLeftoverFiles(appName, packageId);

                // Step 6: Scan for leftover registry entries
                progress.Report("Scanning for leftover registry entries...");
                var leftoverRegistry = ScanForLeftoverRegistry(appName, packageId);

                if (leftoverFiles.Count > 0 || leftoverRegistry.Count > 0)
                {
                    bool shouldRemove = autoRemoveLeftovers || ShowLeftoversDialog(leftoverFiles, leftoverRegistry);
                    if (shouldRemove)
                    {
                        progress.Report("Removing leftover files...");
                        await RemoveLeftoverFiles(leftoverFiles);

                        progress.Report("Removing leftover registry entries...");
                        RemoveLeftoverRegistry(leftoverRegistry);
                    }
                }

                progress.Report("Uninstallation completed successfully.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error during thorough uninstallation: {ex.Message}", ex);
            }
        }

        private async Task TakeSystemSnapshot(string appName)
        {
            var relevantExtensions = new[] { ".exe", ".dll", ".sys", ".ini", ".config", ".dat", ".db", ".xml", ".log" };
            var commonPaths = GetCommonPaths();

            foreach (var path in commonPaths)
            {
                if (!Directory.Exists(path)) continue;

                var files = await Task.Run(() =>
                {
                    try
                    {
                        return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(f => relevantExtensions.Contains(Path.GetExtension(f).ToLower()))
                            .ToList();
                    }
                    catch (Exception)
                    {
                        return new List<string>();
                    }
                });

                var normalizedPath = path.ToLower();
                _initialFileSnapshot[normalizedPath] = new HashSet<string>(files.Select(f => GetFileHash(f)));
                foreach (var file in files)
                {
                    try
                    {
                        _initialFileTimestamps[file] = File.GetLastWriteTime(file);
                    }
                    catch { }
                }
            }
        }

        private string GetFileHash(string filePath)
        {
            try
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(filePath);
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
            catch
            {
                return string.Empty;
            }
        }

        private string[] GetCommonPaths()
        {
            var paths = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents")
            };

            // Add Windows directory
            var winDir = Environment.GetEnvironmentVariable("WINDIR");
            if (!string.IsNullOrEmpty(winDir))
            {
                paths.Add(winDir);
                paths.Add(Path.Combine(winDir, "Temp"));
            }

            return paths.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToArray();
        }

        private async Task CreateSystemRestorePoint(string description)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Checkpoint-Computer -Description '{description}' -RestorePointType 'APPLICATION_UNINSTALL'\"",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception("Failed to create system restore point");
                }
            }
        }

        private async Task<List<string>> ScanForLeftoverFiles(string appName, string packageId)
        {
            var leftoverFiles = new List<string>();
            var commonPaths = GetCommonPaths();
            var relevantExtensions = new[] { ".exe", ".dll", ".sys", ".ini", ".config", ".dat", ".db", ".xml", ".log" };

            // Create search patterns from app name and package ID
            var searchTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                appName,
                packageId,
                appName.Replace(" ", ""),
                Regex.Replace(appName, @"[^\w\s]", ""),
                appName.ToLower(),
                packageId.ToLower(),
                string.Join("", appName.Split(' ').Select(s => s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : "")),
                string.Join("", appName.Split(' ').Select(s => s.ToLower()))
            };

            foreach (var path in commonPaths)
            {
                if (!Directory.Exists(path)) continue;

                await Task.Run(() =>
                {
                    try
                    {
                        // Scan for matching directories
                        var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                        foreach (var dir in directories)
                        {
                            var dirName = Path.GetFileName(dir).ToLower();
                            if (searchTerms.Any(term => dirName.Contains(term.ToLower())))
                            {
                                leftoverFiles.Add(dir);
                                continue;
                            }

                            // Deep scan files in directories
                            try
                            {
                                var files = Directory.GetFiles(dir, "*.*")
                                    .Where(f => relevantExtensions.Contains(Path.GetExtension(f).ToLower()));

                                foreach (var file in files)
                                {
                                    var fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                                    
                                    // Check if file was created/modified during installation
                                    var normalizedPath = path.ToLower();
                                    var fileHash = GetFileHash(file);
                                    var fileTime = File.GetLastWriteTime(file);

                                    bool isNew = _initialFileSnapshot.ContainsKey(normalizedPath) &&
                                               !_initialFileSnapshot[normalizedPath].Contains(fileHash);

                                    bool wasModified = _initialFileTimestamps.ContainsKey(file) &&
                                                     fileTime > _initialFileTimestamps[file];

                                    if (isNew || wasModified || searchTerms.Any(term => fileName.Contains(term.ToLower())))
                                    {
                                        leftoverFiles.Add(file);
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException) { }
                            catch (IOException) { }
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                });
            }

            return leftoverFiles.Distinct().ToList();
        }

        private List<string> ScanForLeftoverRegistry(string appName, string packageId)
        {
            var leftoverKeys = new List<string>();
            var rootKeys = new[]
            {
                Microsoft.Win32.Registry.LocalMachine,
                Microsoft.Win32.Registry.CurrentUser
            };

            var searchPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE",
                @"SOFTWARE\Classes"
            };

            foreach (var rootKey in rootKeys)
            {
                foreach (var path in searchPaths)
                {
                    try
                    {
                        using var key = rootKey.OpenSubKey(path);
                        if (key == null) continue;

                        ScanRegistryKey(key, appName, packageId, leftoverKeys);
                    }
                    catch (Exception) { }
                }
            }

            return leftoverKeys;
        }

        private void ScanRegistryKey(RegistryKey key, string appName, string packageId, List<string> leftoverKeys, int depth = 0)
        {
            if (depth > 10) return; // Prevent too deep recursion

            try
            {
                foreach (var valueName in key.GetValueNames())
                {
                    var value = key.GetValue(valueName)?.ToString();
                    if (string.IsNullOrEmpty(value)) continue;

                    if (value.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                        value.Contains(packageId, StringComparison.OrdinalIgnoreCase))
                    {
                        leftoverKeys.Add(key.Name);
                        break;
                    }
                }

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    ScanRegistryKey(subKey, appName, packageId, leftoverKeys, depth + 1);
                }
            }
            catch (Exception) { }
        }

        private bool ShowLeftoversDialog(List<string> leftoverFiles, List<string> leftoverRegistry)
        {
            var message = "The following leftovers were found after uninstallation:\n\n";

            if (leftoverFiles.Any())
            {
                message += "Files and Directories:\n";
                foreach (var file in leftoverFiles.Take(5))
                {
                    message += $"- {file}\n";
                }
                if (leftoverFiles.Count > 5)
                {
                    message += $"...and {leftoverFiles.Count - 5} more\n";
                }
                message += "\n";
            }

            if (leftoverRegistry.Any())
            {
                message += "Registry Entries:\n";
                foreach (var entry in leftoverRegistry.Take(5))
                {
                    message += $"- {entry}\n";
                }
                if (leftoverRegistry.Count > 5)
                {
                    message += $"...and {leftoverRegistry.Count - 5} more\n";
                }
            }

            message += "\nWould you like to remove these leftover items?";

            var result = CustomMessageBox.Show(
                message,
                "Leftover Items Found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
        }

        private async Task RemoveLeftoverFiles(List<string> leftoverFiles)
        {
            await Task.Run(() =>
            {
                foreach (var file in leftoverFiles)
                {
                    try
                    {
                        if (Directory.Exists(file))
                        {
                            Directory.Delete(file, true);
                        }
                        else if (File.Exists(file))
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception) { }
                }
            });
        }

        private void RemoveLeftoverRegistry(List<string> leftoverRegistry)
        {
            foreach (var keyPath in leftoverRegistry)
            {
                try
                {
                    Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(keyPath, false);
                }
                catch (Exception) { }
                try
                {
                    Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
                }
                catch (Exception) { }
            }
        }

        public async Task CleanupAfterUninstall(string packageId, string appName, IProgress<string> progress)
        {
            try
            {
                // Take a small pause to ensure all uninstall operations are complete
                await Task.Delay(2000);

                progress.Report("Scanning for leftover files...");
                var leftoverFiles = await ScanForLeftoverFiles(appName, packageId);

                progress.Report("Scanning for leftover registry entries...");
                var leftoverRegistry = ScanForLeftoverRegistry(appName, packageId);

                if (leftoverFiles.Count > 0)
                {
                    progress.Report($"Found {leftoverFiles.Count} leftover files. Removing...");
                    await RemoveLeftoverFiles(leftoverFiles);
                }

                if (leftoverRegistry.Count > 0)
                {
                    progress.Report($"Found {leftoverRegistry.Count} leftover registry entries. Removing...");
                    RemoveLeftoverRegistry(leftoverRegistry);
                }

                progress.Report("Cleanup completed successfully.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error during cleanup: {ex.Message}", ex);
            }
        }
    }
} 