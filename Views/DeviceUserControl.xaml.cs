using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Input;
using HITAPEX.Views.DeviceParameters;

namespace HITAPEX.Views;

public partial class DeviceUserControl : UserControl
{
    private BaseParameterControl? _baseControl;
    private SteeringWheelParameterControl? _steeringWheelControl;
    private PedalParameterControl? _pedalControl;

    private UserControl? _currentControl;
    private int _currentIndex = 0;
    private bool _isCheckingUnsaved;
    private EventHandler? _fadeOutCompleted;

    public BaseParameterControl? BaseControl => _baseControl;
    public PedalParameterControl? PedalControl => _pedalControl;
    public SteeringWheelParameterControl? SteeringWheelControl => _steeringWheelControl;

    public DeviceUserControl()
    {
        InitializeComponent();
        InitializeControls();
        SetupKeyboardShortcuts();
    }

    private void InitializeControls()
    {
        _baseControl = new BaseParameterControl();
        _steeringWheelControl = new SteeringWheelParameterControl();
        _pedalControl = new PedalParameterControl();
    }

    private void SetupKeyboardShortcuts()
    {
        KeyDown += DeviceUserControl_KeyDown;
    }

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

    private void NavigatePrevious()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            UpdateNavigationSelection(_currentIndex);
        }
    }

    private void NavigateNext()
    {
        if (_currentIndex < 2)
        {
            _currentIndex++;
            UpdateNavigationSelection(_currentIndex);
        }
    }

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
                    var savedIdx = _currentIndex;
                    var savedBtn = GetCurrentNavButton();
                    _pedalControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        },
                        onCancelled: () =>
                        {
                            // 取消子导航：恢复原 RadioButton 选中状态
                            _isCheckingUnsaved = false;
                            _currentIndex = savedIdx;
                            RadioButton? btn = savedBtn;
                            if (btn != null)
                                btn.IsChecked = true;
                        });
                }
                else if (_currentControl == _steeringWheelControl && _steeringWheelControl is { HasUnsavedChanges: true })
                {
                    _isCheckingUnsaved = true;
                    var savedIdx = _currentIndex;
                    var savedBtn = GetCurrentNavButton();
                    _steeringWheelControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsaved = false;
                            _currentIndex = savedIdx;
                            RadioButton? btn = savedBtn;
                            if (btn != null)
                                btn.IsChecked = true;
                        });
                }
                else if (_currentControl == _baseControl && _baseControl is { HasUnsavedChanges: true })
                {
                    _isCheckingUnsaved = true;
                    var savedIdx = _currentIndex;
                    var savedBtn = GetCurrentNavButton();
                    _baseControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsaved = false;
                            _currentIndex = savedIdx;
                            RadioButton? btn = savedBtn;
                            if (btn != null)
                                btn.IsChecked = true;
                        });
                }
                else
                {
                    ShowControl(targetControl, true);
                }
            }
        }
    }

    private RadioButton? GetCurrentNavButton() => _currentIndex switch
    {
        0 => BaseNavButton,
        1 => SteeringWheelNavButton,
        2 => PedalNavButton,
        _ => null
    };

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
