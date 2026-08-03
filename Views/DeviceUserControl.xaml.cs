using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Input;
using HITAPEX.Views.DeviceParameters;

namespace HITAPEX.Views;

/// <summary>
/// 设备参数页面容器，管理三个子视图（基座/面盘/踏板）的切换、淡入淡出动画和未保存变更检查。
/// 通过 RadioButton 导航栏在 BaseParameterControl、SteeringWheelParameterControl、
/// PedalParameterControl 之间切换，支持键盘快捷键（1/2/3、上/下箭头）导航。
/// </summary>
public partial class DeviceUserControl : UserControl
{
    // ═══ 子控件实例 ═══
    private BaseParameterControl? _baseControl;
    private SteeringWheelParameterControl? _steeringWheelControl;
    private PedalParameterControl? _pedalControl;

    // ═══ 导航状态 ═══
    /// <summary>当前显示的子控件</summary>
    private UserControl? _currentControl;
    /// <summary>当前选中的导航索引（0=基座, 1=面盘, 2=踏板）</summary>
    private int _currentIndex = 0;
    /// <summary>正在检查未保存变更标志，阻止导航按钮重复触发</summary>
    private bool _isCheckingUnsaved;
    /// <summary>淡出动画完成后的回调，用于衔接淡入动画</summary>
    private EventHandler? _fadeOutCompleted;

    // ═══ 公开属性：供外部获取子控件引用 ═══
    public BaseParameterControl? BaseControl => _baseControl;
    public PedalParameterControl? PedalControl => _pedalControl;
    public SteeringWheelParameterControl? SteeringWheelControl => _steeringWheelControl;

    /// <summary>
    /// 导航到指定设备子页（供外部调用，如首页 Group 图标点击跳转）。
    /// </summary>
    /// <param name="index">0=基座, 1=面盘, 2=踏板</param>
    public void NavigateToTab(int index)
    {
        UpdateNavigationSelection(Math.Clamp(index, 0, 2));
    }

    public DeviceUserControl()
    {
        InitializeComponent();
        InitializeControls();
        SetupKeyboardShortcuts();
    }

    /// <summary>创建三个设备参数子控件的实例</summary>
    private void InitializeControls()
    {
        _baseControl = new BaseParameterControl();
        _steeringWheelControl = new SteeringWheelParameterControl();
        _pedalControl = new PedalParameterControl();
    }

    /// <summary>注册键盘快捷键（1/2/3 切换子页，上/下箭头导航）</summary>
    private void SetupKeyboardShortcuts()
    {
        KeyDown += DeviceUserControl_KeyDown;
    }

    /// <summary>键盘快捷键处理：1/2/3 切换子页，上/下箭头循环导航</summary>
    private void DeviceUserControl_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.D1:
            case Key.NumPad1:
                BaseNavButton.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                SteeringWheelNavButton.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                PedalNavButton.IsChecked = true;
                e.Handled = true;
                break;
            case Key.Up:
                NavigatePrevious();
                e.Handled = true;
                break;
            case Key.Down:
                NavigateNext();
                e.Handled = true;
                break;
        }
    }

    /// <summary>切换到上一个子控件</summary>
    private void NavigatePrevious()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            UpdateNavigationSelection(_currentIndex);
        }
    }

    /// <summary>切换到下一个子控件</summary>
    private void NavigateNext()
    {
        if (_currentIndex < 2)
        {
            _currentIndex++;
            UpdateNavigationSelection(_currentIndex);
        }
    }

    /// <summary>根据索引更新导航按钮选中状态</summary>
    private void UpdateNavigationSelection(int index)
    {
        switch (index)
        {
            case 0:
                BaseNavButton.IsChecked = true;
                break;
            case 1:
                SteeringWheelNavButton.IsChecked = true;
                break;
            case 2:
                PedalNavButton.IsChecked = true;
                break;
        }
    }

    /// <summary>页面加载时恢复上次选中的子选项卡（而非总是显示基座）</summary>
    private void DeviceUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // 恢复上次选中的子选项卡，而非始终显示基座
        UserControl? controlToShow;
        if (PedalNavButton.IsChecked == true)
        {
            controlToShow = _pedalControl;
            _currentIndex = 2;
        }
        else if (SteeringWheelNavButton.IsChecked == true)
        {
            controlToShow = _steeringWheelControl;
            _currentIndex = 1;
        }
        else
        {
            controlToShow = _baseControl;
            _currentIndex = 0;
        }
        ShowControl(controlToShow, false);
    }

    /// <summary>
    /// 导航按钮选中变更处理：检查当前子控件是否有未保存变更，
    /// 有则弹出确认对话框，无则直接切换并播放淡入淡出动画。
    /// </summary>
    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isCheckingUnsaved) return;

        if (sender is RadioButton button)
        {
            UserControl? targetControl = null;

            if (button == BaseNavButton)
            {
                targetControl = _baseControl;
                _currentIndex = 0;
            }
            else if (button == SteeringWheelNavButton)
            {
                targetControl = _steeringWheelControl;
                _currentIndex = 1;
            }
            else if (button == PedalNavButton)
            {
                targetControl = _pedalControl;
                _currentIndex = 2;
            }

            if (targetControl != null && targetControl != _currentControl)
            {
                if (_currentControl == _pedalControl && _pedalControl is { HasUnsavedChanges: true })
                {
                    _isCheckingUnsaved = true;
                    _pedalControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        },
                        onCancelled: () =>
                        {
                            // 取消子导航 = 不保存修改，直接切换到目标子页面
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        });
                }
                else if (_currentControl == _steeringWheelControl && _steeringWheelControl is { HasUnsavedChanges: true })
                {
                    _isCheckingUnsaved = true;
                    _steeringWheelControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        });
                }
                else if (_currentControl == _baseControl && _baseControl is { HasUnsavedChanges: true })
                {
                    _isCheckingUnsaved = true;
                    _baseControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        });
                }
                else
                {
                    ShowControl(targetControl, true);
                }
            }
        }
    }

    /// <summary>
    /// 显示目标子控件，可选播放淡入淡出切换动画。
    /// 动画流程：先播放当前控件的淡出动画 → 替换内容 → 播放目标控件的淡入动画。
    /// </summary>
    private void ShowControl(UserControl? control, bool animate)
    {
        if (control == null) return;

        if (animate && _currentControl != null)
        {
            var fadeOut = (Storyboard)FindResource("FadeOutAnimation");
            if (_fadeOutCompleted != null)
                fadeOut.Completed -= _fadeOutCompleted;

            _fadeOutCompleted = (s, e) =>
            {
                ContentHost.Content = control;
                _currentControl = control;

                var fadeIn = (Storyboard)FindResource("FadeInAnimation");
                fadeIn.Begin(ContentHost);
            };
            fadeOut.Completed += _fadeOutCompleted;
            fadeOut.Begin(ContentHost);
        }
        else
        {
            ContentHost.Content = control;
            _currentControl = control;

            if (animate)
            {
                var fadeIn = (Storyboard)FindResource("FadeInAnimation");
                fadeIn.Begin(ContentHost);
            }
        }
    }
}
