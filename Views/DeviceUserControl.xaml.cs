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

    public PedalParameterControl? PedalControl => _pedalControl;

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
        ShowControl(_baseControl, false);
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
                    _pedalControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsaved = false;
                            ShowControl(targetControl, true);
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsaved = false;
                            _pedalControl.DiscardChanges();
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

    private void ShowControl(UserControl? control, bool animate)
    {
        if (control == null) return;

        if (animate && _currentControl != null)
        {
            var fadeOut = (Storyboard)FindResource("FadeOutAnimation");
            fadeOut.Completed += (s, e) =>
            {
                ContentHost.Content = control;
                _currentControl = control;

                var fadeIn = (Storyboard)FindResource("FadeInAnimation");
                fadeIn.Begin(ContentHost);
            };
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
