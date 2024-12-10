using System;
using System.Collections.Generic;
using System.Management;
using System.IO;

namespace UsbFileBlocker
{
    public class UsbDriveManager
    {
        public event Action<string> UsbDriveConnected;
        public event Action<string> UsbDriveDisconnected;

        private List<string> usbDrives = new List<string>();

        public List<string> UsbDrives => usbDrives;

        public void StartMonitoring()
        {
            var connectQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_DiskDrive'");
            var connectWatcher = new ManagementEventWatcher(connectQuery);
            connectWatcher.EventArrived += Watcher_EventArrived;
            connectWatcher.Start();

            var disconnectQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_DiskDrive'");
            var disconnectWatcher = new ManagementEventWatcher(disconnectQuery);
            disconnectWatcher.EventArrived += Watcher_DisconnectEventArrived;
            disconnectWatcher.Start();
        }

        private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var targetInstance = e.NewEvent["TargetInstance"] as ManagementBaseObject;

            if (targetInstance != null)
            {
                var deviceID = targetInstance["DeviceID"].ToString();
                var mediaType = targetInstance["MediaType"]?.ToString();

                if (mediaType == "Removable Media")
                {
                    var name = GetRootDirectoryByDeviceID(deviceID);
                    usbDrives.Add(name);
                    UsbDriveConnected?.Invoke(name);
                }
            }
        }

        private void Watcher_DisconnectEventArrived(object sender, EventArrivedEventArgs e)
        {
            var targetInstance = e.NewEvent["TargetInstance"] as ManagementBaseObject;

            if (targetInstance != null)
            {
                var deviceID = targetInstance["DeviceID"].ToString();
                var mediaType = targetInstance["MediaType"]?.ToString();

                if (mediaType == "Removable Media")
                {
                    try
                    {
                        var name = GetRootDirectoryByDeviceID(deviceID);

                        if (usbDrives.Contains(name))
                        {
                            usbDrives.Remove(name);
                            UsbDriveDisconnected?.Invoke(name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при отключении устройства: {ex.Message}");
                    }
                }
            }
        }

        private string GetRootDirectoryByDeviceID(string deviceID)
        {
            try
            {
                var query = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceID}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                var disks = query.Get().Cast<ManagementBaseObject>().ToList();

                if (!disks.Any())
                {
                    return "Не найдено";
                }

                foreach (var disk in disks)
                {
                    var partition = (ManagementBaseObject)disk;
                    var logicalQuery = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

                    var logicalDisks = logicalQuery.Get().Cast<ManagementBaseObject>().ToList();


                    if (!logicalDisks.Any())
                    {
                        continue;
                    }

                    foreach (var logicalDisk in logicalDisks)
                    {
                        var logical = (ManagementBaseObject)logicalDisk;
                        var driveLetter = logical["DeviceID"].ToString();

                        try
                        {
                            var driveInfo = new DriveInfo(driveLetter);
                            if (driveInfo.IsReady)
                            {
                                return driveInfo.RootDirectory.FullName;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка при получении пути для {driveLetter}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при выполнении запроса для устройства {deviceID}: {ex.Message}");
            }

            return "Не найдено";


        }

        public void InitializeUsbDrives()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                {
                    usbDrives.Add(drive.RootDirectory.FullName);
                }
            }
        }
    }
}
