using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HITAPEX.Models.Usb;

namespace HITAPEX.Views.DeviceParameters;

public partial class BaseParameterControl : UserControl
{
    private UsbDeviceInfo? _connectedBaseDevice;

    public BaseParameterControl()
    {
        InitializeComponent();
        Loaded += BaseParameterControl_Loaded;
    }

    private async void BaseParameterControl_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshDeviceInfoAsync();
    }

    public async Task RefreshDeviceInfoAsync()
    {
        try
        {
            var connectedDevices = App.UsbManager?.ConnectedDevices
                ?? System.Collections.ObjectModel.ReadOnlyCollection<UsbDeviceInfo>.Empty;

            _connectedBaseDevice = connectedDevices.FirstOrDefault(d =>
            {
                var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                return descriptor != null && descriptor.DeviceType == DeviceType.Base
                       && descriptor.IsNormalMode(d.Vid, d.Pid);
            });

            if (_connectedBaseDevice != null)
            {
                var descriptor = DeviceRegistry.FindByVidPid(_connectedBaseDevice.Vid, _connectedBaseDevice.Pid);
                var modelName = descriptor?.ModelName ?? "基座";
                DeviceModelName.Text = modelName;
                ConnectionStatusText.Text = $"已连接({modelName})";

                var color = (Color)ColorConverter.ConvertFromString("#179548");
                var brush = new SolidColorBrush(color);
                var iconPaths = new[] { ConnStatusIcon1, ConnStatusIcon2, ConnStatusIcon3,
                                        ConnStatusIcon4, ConnStatusIcon5, ConnStatusIcon6, ConnStatusIcon7 };
                foreach (var path in iconPaths)
                {
                    if (path != null)
                        path.Stroke = brush;
                }

                if (App.ProtocolService != null && App.FirmwareUpdater != null)
                {
                    var deviceInfo = await App.FirmwareUpdater.GetDeviceInfoAsync(
                        _connectedBaseDevice, DeviceType.Base);
                    FirmwareVersionText.Text = deviceInfo?.VersionString ?? "未知";
                }
                else
                {
                    FirmwareVersionText.Text = "未知";
                }
            }
            else
            {
                DeviceModelName.Text = "基座";
                ConnectionStatusText.Text = "未连接";

                var color = (Color)ColorConverter.ConvertFromString("#C60E0E");
                var brush = new SolidColorBrush(color);
                var iconPaths = new[] { ConnStatusIcon1, ConnStatusIcon2, ConnStatusIcon3,
                                        ConnStatusIcon4, ConnStatusIcon5, ConnStatusIcon6, ConnStatusIcon7 };
                foreach (var path in iconPaths)
                {
                    if (path != null)
                        path.Stroke = brush;
                }

                FirmwareVersionText.Text = "---";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 刷新设备信息异常: {ex.Message}");
        }
    }
}
