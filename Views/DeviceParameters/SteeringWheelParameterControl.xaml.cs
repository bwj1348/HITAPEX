using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HITAPEX.Models.Usb;

namespace HITAPEX.Views.DeviceParameters;

public partial class SteeringWheelParameterControl : UserControl
{
    private UsbDeviceInfo? _connectedWheelDevice;

    public SteeringWheelParameterControl()
    {
        InitializeComponent();
        Loaded += SteeringWheelParameterControl_Loaded;
    }

    private async void SteeringWheelParameterControl_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshDeviceInfoAsync();
    }

    public async Task RefreshDeviceInfoAsync()
    {
        try
        {
            var connectedDevices = App.UsbManager?.ConnectedDevices
                ?? System.Collections.ObjectModel.ReadOnlyCollection<UsbDeviceInfo>.Empty;

            _connectedWheelDevice = connectedDevices.FirstOrDefault(d =>
            {
                var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                return descriptor != null && descriptor.DeviceType == DeviceType.Wheel
                       && descriptor.IsNormalMode(d.Vid, d.Pid);
            });

            if (_connectedWheelDevice != null)
            {
                var descriptor = DeviceRegistry.FindByVidPid(_connectedWheelDevice.Vid, _connectedWheelDevice.Pid);
                var modelName = descriptor?.ModelName ?? "面盘";
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
                        _connectedWheelDevice, DeviceType.Wheel);
                    FirmwareVersionText.Text = deviceInfo?.VersionString ?? "未知";
                }
                else
                {
                    FirmwareVersionText.Text = "未知";
                }
            }
            else
            {
                DeviceModelName.Text = "面盘";
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
            Debug.WriteLine($"[SteeringWheelControl] 刷新设备信息异常: {ex.Message}");
        }
    }
}
