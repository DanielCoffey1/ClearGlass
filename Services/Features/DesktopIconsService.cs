using System;
using System.Diagnostics;
using ClearGlass.Services.Exceptions;
using ClearGlass.Services.Native;
using ClearGlass.Services.Registry;
using Microsoft.Win32;

namespace ClearGlass.Services.Features
{
    /// <summary>
    /// Handles Windows desktop icons functionality
    /// </summary>
    internal class DesktopIconsService
    {
        private IntPtr GetDesktopListViewHandle()
        {
            return WindowsApi.FindWindowEx(
                WindowsApi.FindWindowEx(
                    WindowsApi.FindWindow("Progman", null),
                    IntPtr.Zero,
                    "SHELLDLL_DefView",
                    null),
                IntPtr.Zero,
                "SysListView32",
                "FolderView");
        }

        private bool GetDesktopIconsRegistryState()
        {
            return RegistryHelper.GetValue<int>(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideIcons", 0) == 0;
        }

        private void SetDesktopIconsRegistryState(bool show)
        {
            RegistryHelper.SetValue(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "HideIcons",
                show ? 0 : 1,
                RegistryValueKind.DWord);
        }

        private void SetDesktopIconsVisibility(IntPtr handle, bool show)
        {
            if (handle != IntPtr.Zero)
            {
                if (show && !WindowsApi.IsWindowVisible(handle))
                {
                    WindowsApi.ShowWindow(handle, WindowsApi.SW_SHOW);
                }
                else if (!show && WindowsApi.IsWindowVisible(handle))
                {
                    WindowsApi.ShowWindow(handle, WindowsApi.SW_HIDE);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether desktop icons are visible
        /// </summary>
        public bool AreDesktopIconsVisible
        {
            get
            {
                try
                {
                    // First check registry for persisted state
                    bool registryState = GetDesktopIconsRegistryState();

                    // Fallback to checking window state
                    var toggleHandle = GetDesktopListViewHandle();
                    return toggleHandle != IntPtr.Zero ? WindowsApi.IsWindowVisible(toggleHandle) : registryState;
                }
                catch (Exception ex)
                {
                    throw new ThemeServiceException(
                        $"Error getting desktop icons state: {ex.Message}",
                        ThemeServiceOperation.WindowsApi,
                        ex);
                }
            }
            set
            {
                try
                {
                    SetDesktopIconsRegistryState(value);
                    var toggleHandle = GetDesktopListViewHandle();
                    SetDesktopIconsVisibility(toggleHandle, value);
                }
                catch (Exception ex)
                {
                    throw new ThemeServiceException(
                        $"Error toggling desktop icons: {ex.Message}",
                        ThemeServiceOperation.WindowsApi,
                        ex);
                }
            }
        }
    }
} 