using System;
using System.Linq;
using System.Windows;

namespace UsbFileBlocker
{
    public partial class MainWindow : Window
    {
        private FileMonitor fileMonitor;
        private UsbDriveManager usbDriveManager;

        public MainWindow()
        {
            InitializeComponent();

            usbDriveManager = new UsbDriveManager();
            usbDriveManager.UsbDriveConnected += UsbDriveManager_UsbDriveConnected;
            usbDriveManager.UsbDriveDisconnected += UsbDriveManager_UsbDriveDisconnected;

            fileMonitor = new FileMonitor();
            fileMonitor.FileBlocked += FileMonitor_FileBlocked;

            usbDriveManager.InitializeUsbDrives();
            usbDriveManager.StartMonitoring();

            if (usbDriveManager.UsbDrives.Count > 0)
            {
                fileMonitor.InitializeFileSystemWatcher(usbDriveManager.UsbDrives);
            }
        }

        private void UsbDriveManager_UsbDriveConnected(string usbDrive)
        {
            Dispatcher.Invoke(() =>
            {
                fileMonitor.InitializeFileSystemWatcher([usbDrive]);
                MessageBox.Show($"Флешка подключена! Устройство: {usbDrive}", "Устройство подключено", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void UsbDriveManager_UsbDriveDisconnected(string usbDrive)
        {
            Dispatcher.Invoke(() =>
            {
                fileMonitor.StopMonitoring();
                MessageBox.Show($"Флешка отключена! Устройство: {usbDrive}", "Устройство отключено", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void FileMonitor_FileBlocked(string fileName, string usbDrive)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Файл заблокирован: {fileName} на диске {usbDrive}", "заблокирован", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            string fileTypeInput = FileTypeTextBox.Text;
            if (!string.IsNullOrEmpty(fileTypeInput))
            {
                fileMonitor.SetBlockedFileTypes(fileTypeInput);
            }

            string content = FileContentTextBox.Text.Trim();
            fileMonitor.SetBlockedContent(content);

            MessageBox.Show("Настройки сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
