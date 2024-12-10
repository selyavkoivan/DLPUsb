using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsbFileBlocker;

public class FileMonitor
{
    private List<FileSystemWatcher> usbWatchers = new List<FileSystemWatcher>();
    private List<string> blockedFileTypes = new List<string>();
    private string blockedContent;

    public event Action<string, string> FileBlocked;

    public void InitializeFileSystemWatcher(List<string> usbDrives)
    {
        foreach (var usbDrive in usbDrives)
        {
            AddFileSystemWatcher(usbDrive);
        }
    }

    private void AddFileSystemWatcher(string usbDrive)
    {
        var watcher = new FileSystemWatcher
        {
            Path = usbDrive,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            Filter = "*.*"
        };

        watcher.Created += async (sender, e) => await OnFileCreatedAsync(e, usbDrive);
        watcher.EnableRaisingEvents = true;
        usbWatchers.Add(watcher);
    }

    private void DeleteFileSystemWatcher(string usbDrive)
    {
        usbWatchers.Remove(usbWatchers.First(w => w.Path == usbDrive));
    }

    public async Task OnFileCreatedAsync(FileSystemEventArgs e, string usbDrive)
    {
        if (blockedFileTypes.Any(extension => e.FullPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                await Task.Delay(500); 
                File.Delete(e.FullPath);
                FileBlocked?.Invoke(e.Name, usbDrive);
            }
            catch (Exception ex)
            {
                FileBlocked?.Invoke($"Ошибка: {e.Name}. {ex.Message}", usbDrive);
            }
        }

        if (await FileContainsBlockedContentAsync(e.FullPath))
        {
            try
            {
                File.Delete(e.FullPath);
                FileBlocked?.Invoke($"Содержит запрещенный контент: {e.Name}", usbDrive);
            }
            catch (Exception ex)
            {
                FileBlocked?.Invoke($"Ошибка: {e.Name}. {ex.Message}", usbDrive);
            }
        }
    }

    private async Task<bool> FileContainsBlockedContentAsync(string filePath)
    {
        try
        {
            await WaitForFileReleaseAsync(filePath);

            string content = await File.ReadAllTextAsync(filePath);
            return content.Contains(blockedContent, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task WaitForFileReleaseAsync(string filePath)
    {
        const int maxAttempts = 10;
        int attempts = 0;
        bool isFileAvailable = false;

        while (attempts < maxAttempts && !isFileAvailable)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    isFileAvailable = true;
                }
            }
            catch (IOException)
            {
                attempts++;
                await Task.Delay(200); 
            }
        }

        if (!isFileAvailable)
        {
            throw new IOException("File could not be accessed within the given time.");
        }
    }

    public void SetBlockedFileTypes(string fileTypes)
    {
        blockedFileTypes = fileTypes.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(f => f.Trim())
                                     .ToList();
    }

    public void SetBlockedContent(string content)
    {
        blockedContent = content.Trim();
    }

    public void StopMonitoring()
    {
        foreach (var watcher in usbWatchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }
}