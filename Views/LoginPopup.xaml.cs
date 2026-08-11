using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HITAPEX.Services;

namespace HITAPEX.Views;

/// <summary>
/// 登录/注册弹窗，支持登录和注册两个面板切换。
/// </summary>
public partial class LoginPopup : UserControl
{
    public LoginPopup()
    {
        InitializeComponent();
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
        SwitchToLoginPanel();
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    // ════════════════════════════════════════════════════════════════
    // 面板切换
    // ════════════════════════════════════════════════════════════════

    private void SwitchToLoginPanel()
    {
        ResetLoginInputs();
        LoginFormPanel.Visibility = Visibility.Visible;
        RegisterFormPanel.Visibility = Visibility.Collapsed;
        ForgotPasswordFormPanel.Visibility = Visibility.Collapsed;
    }

    private void SwitchToRegisterPanel()
    {
        ResetRegisterInputs();
        LoginFormPanel.Visibility = Visibility.Collapsed;
        RegisterFormPanel.Visibility = Visibility.Visible;
        ForgotPasswordFormPanel.Visibility = Visibility.Collapsed;
    }

    private void SwitchToForgotPasswordPanel()
    {
        LoginFormPanel.Visibility = Visibility.Collapsed;
        RegisterFormPanel.Visibility = Visibility.Collapsed;
        ForgotPasswordFormPanel.Visibility = Visibility.Visible;
        ForgotPasswordStep1Panel.Visibility = Visibility.Visible;
        ForgotPasswordStep2Panel.Visibility = Visibility.Collapsed;
        ForgotPasswordStep3Panel.Visibility = Visibility.Collapsed;
    }

    private void SwitchToForgotPassword_Click(object sender, RoutedEventArgs e) => SwitchToForgotPasswordPanel();

    private void SwitchToRegister_Click(object sender, RoutedEventArgs e) => SwitchToRegisterPanel();

    private void SwitchToLogin_Click(object sender, RoutedEventArgs e) => SwitchToLoginPanel();

    private void ResetLoginInputs()
    {
        LoginEmailTextBox.Text = LocalizationService.Instance["Login.EmailPlaceholder"];
        LoginEmailTextBox.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 238, 238, 238));
        LoginPasswordBox.Password = string.Empty;
        LoginPasswordPlaceholder.Visibility = Visibility.Visible;
        AgreementCheckBox.IsChecked = false;
    }

    private void ResetRegisterInputs()
    {
        ResetRegisterTextBox(RegisterEmailTextBox, "Login.EmailPlaceholder");
        ResetRegisterTextBox(RegisterUsernameTextBox, "Login.UsernamePlaceholder");
        RegisterPasswordBox.Password = string.Empty;
        RegisterPasswordPlaceholder.Visibility = Visibility.Visible;
        RegisterConfirmPasswordBox.Password = string.Empty;
        RegisterConfirmPasswordPlaceholder.Visibility = Visibility.Visible;
        RegisterAgreementCheckBox.IsChecked = false;
    }

    // ════════════════════════════════════════════════════════════════
    // 弹窗关闭
    // ════════════════════════════════════════════════════════════════

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void Overlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Grid grid && grid.Background != null)
            Hide();
    }

    // ════════════════════════════════════════════════════════════════
    // 登录表单 — 邮箱 placeholder
    // ════════════════════════════════════════════════════════════════

    private void LoginEmailTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (LoginEmailTextBox.Text == LocalizationService.Instance["Login.EmailPlaceholder"])
        {
            LoginEmailTextBox.Text = string.Empty;
            LoginEmailTextBox.Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238));
        }
    }

    private void LoginEmailTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LoginEmailTextBox.Text))
        {
            LoginEmailTextBox.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 238, 238, 238));
            LoginEmailTextBox.Text = LocalizationService.Instance["Login.EmailPlaceholder"];
        }
    }

    private void ClearLoginInput_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name && name == "LoginEmailTextBox")
        {
            LoginEmailTextBox.Text = string.Empty;
            LoginEmailTextBox.Focus();
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 登录表单 — 密码 placeholder
    // ════════════════════════════════════════════════════════════════

    private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        LoginPasswordPlaceholder.Visibility = string.IsNullOrEmpty(LoginPasswordBox.Password)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoginPasswordBox_GotFocus(object sender, RoutedEventArgs e)
        => LoginPasswordPlaceholder.Visibility = Visibility.Collapsed;

    private void LoginPasswordBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(LoginPasswordBox.Password))
            LoginPasswordPlaceholder.Visibility = Visibility.Visible;
    }

    private void ClearLoginPassword_Click(object sender, RoutedEventArgs e)
    {
        LoginPasswordBox.Password = string.Empty;
        LoginPasswordBox.Focus();
        RemoveOverlayTextBox(LoginPasswordBox, "overlay_LoginPassword");
        LoginPasswordBox.Visibility = Visibility.Visible;
        ResetEyeIcon(LoginPasswordEyeBtn);
        LoginPasswordPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void ToggleLoginPasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        TogglePasswordVisibilityInner(LoginPasswordEyeBtn, LoginPasswordBox, LoginPasswordPlaceholder, "overlay_LoginPassword");
    }

    // ════════════════════════════════════════════════════════════════
    // 注册表单 — TextBox placeholder（通用）
    // ════════════════════════════════════════════════════════════════

    private void ResetRegisterTextBox(TextBox tb, string placeholderKey)
    {
        tb.Text = LocalizationService.Instance[placeholderKey];
        tb.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 238, 238, 238));
    }

    private void RegisterInput_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        string? placeholderKey = tb.Name switch
        {
            "RegisterEmailTextBox" => "Login.EmailPlaceholder",
            "RegisterUsernameTextBox" => "Login.UsernamePlaceholder",
            "RegisterVerificationCodeTextBox" => "Settings.VerificationCodePlaceholder",
            _ => null
        };
        if (placeholderKey != null && tb.Text == LocalizationService.Instance[placeholderKey])
        {
            tb.Text = string.Empty;
            tb.Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238));
        }
    }

    private void RegisterInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        string? placeholderKey = tb.Name switch
        {
            "RegisterEmailTextBox" => "Login.EmailPlaceholder",
            "RegisterUsernameTextBox" => "Login.UsernamePlaceholder",
            "RegisterVerificationCodeTextBox" => "Settings.VerificationCodePlaceholder",
            _ => null
        };
        if (placeholderKey != null && string.IsNullOrWhiteSpace(tb.Text))
        {
            tb.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 238, 238, 238));
            tb.Text = LocalizationService.Instance[placeholderKey];
        }
    }

    private void ClearRegisterInput_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            var element = FindName(name);
            if (element is TextBox tb)
            {
                tb.Text = string.Empty;
                tb.Focus();
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 注册表单 — 密码
    // ════════════════════════════════════════════════════════════════

    private void RegisterPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        RegisterPasswordPlaceholder.Visibility = string.IsNullOrEmpty(RegisterPasswordBox.Password)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RegisterPasswordBox_GotFocus(object sender, RoutedEventArgs e)
        => RegisterPasswordPlaceholder.Visibility = Visibility.Collapsed;

    private void RegisterPasswordBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(RegisterPasswordBox.Password))
            RegisterPasswordPlaceholder.Visibility = Visibility.Visible;
    }

    private void ClearRegisterPassword_Click(object sender, RoutedEventArgs e)
    {
        RegisterPasswordBox.Password = string.Empty;
        RegisterPasswordBox.Focus();
        RemoveOverlayTextBox(RegisterPasswordBox, "overlay_RegisterPassword");
        RegisterPasswordBox.Visibility = Visibility.Visible;
        ResetEyeIcon(RegisterPasswordEyeBtn);
        RegisterPasswordPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void ToggleRegisterPasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        TogglePasswordVisibilityInner(RegisterPasswordEyeBtn, RegisterPasswordBox, RegisterPasswordPlaceholder, "overlay_RegisterPassword");
    }

    // ════════════════════════════════════════════════════════════════
    // 注册表单 — 确认密码
    // ════════════════════════════════════════════════════════════════

    private void RegisterConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        RegisterConfirmPasswordPlaceholder.Visibility = string.IsNullOrEmpty(RegisterConfirmPasswordBox.Password)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RegisterConfirmPasswordBox_GotFocus(object sender, RoutedEventArgs e)
        => RegisterConfirmPasswordPlaceholder.Visibility = Visibility.Collapsed;

    private void RegisterConfirmPasswordBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(RegisterConfirmPasswordBox.Password))
            RegisterConfirmPasswordPlaceholder.Visibility = Visibility.Visible;
    }

    private void ClearRegisterConfirmPassword_Click(object sender, RoutedEventArgs e)
    {
        RegisterConfirmPasswordBox.Password = string.Empty;
        RegisterConfirmPasswordBox.Focus();
        RemoveOverlayTextBox(RegisterConfirmPasswordBox, "overlay_RegisterConfirmPassword");
        RegisterConfirmPasswordBox.Visibility = Visibility.Visible;
        RegisterConfirmPasswordPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void ToggleRegisterConfirmPasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        TogglePasswordVisibilityInner(null, RegisterConfirmPasswordBox, RegisterConfirmPasswordPlaceholder, "overlay_RegisterConfirmPassword");
    }

    // ════════════════════════════════════════════════════════════════
    // 密码可见性切换（通用）
    // ════════════════════════════════════════════════════════════════

    private void TogglePasswordVisibilityInner(Button? eyeBtn, PasswordBox pwdBox, TextBlock placeholder, string overlayTag)
    {
        Path? eyeClosed = null, eyeOpen = null;
        if (eyeBtn != null)
        {
            eyeClosed = eyeBtn.Template?.FindName("EyeClosed", eyeBtn) as Path;
            eyeOpen = eyeBtn.Template?.FindName("EyeOpen", eyeBtn) as Path;
        }

        bool currentlyHidden = eyeClosed == null || eyeClosed.Visibility == Visibility.Visible;

        if (currentlyHidden)
        {
            if (eyeClosed != null) eyeClosed.Visibility = Visibility.Collapsed;
            if (eyeOpen != null) eyeOpen.Visibility = Visibility.Visible;

            var parent = VisualTreeHelper.GetParent(pwdBox) as Panel;
            if (parent != null)
            {
                var overlay = new TextBox
                {
                    Text = pwdBox.Password,
                    Tag = overlayTag,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
                    CaretBrush = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 0, 0)
                };
                overlay.TextChanged += (_, _) => pwdBox.Password = overlay.Text;
                overlay.GotFocus += (_, _) => placeholder.Visibility = Visibility.Collapsed;
                overlay.LostFocus += (_, _) => { if (string.IsNullOrEmpty(overlay.Text)) placeholder.Visibility = Visibility.Visible; };
                Grid.SetColumn(overlay, 0);
                parent.Children.Add(overlay);
            }
            pwdBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (eyeClosed != null) eyeClosed.Visibility = Visibility.Visible;
            if (eyeOpen != null) eyeOpen.Visibility = Visibility.Collapsed;

            pwdBox.Password = pwdBox.Password;
            pwdBox.Visibility = Visibility.Visible;
            RemoveOverlayTextBox(pwdBox, overlayTag);
        }

        placeholder.Visibility = string.IsNullOrEmpty(pwdBox.Password)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void RemoveOverlayTextBox(PasswordBox pwdBox, string overlayTag)
    {
        var parent = VisualTreeHelper.GetParent(pwdBox) as Panel;
        if (parent != null)
        {
            var overlays = parent.Children.OfType<TextBox>()
                .Where(tb => tb.Tag is string s && s == overlayTag).ToList();
            foreach (var ov in overlays)
                parent.Children.Remove(ov);
        }
    }

    private static void ResetEyeIcon(Button eyeBtn)
    {
        var eyeClosed = eyeBtn.Template?.FindName("EyeClosed", eyeBtn) as Path;
        var eyeOpen = eyeBtn.Template?.FindName("EyeOpen", eyeBtn) as Path;
        if (eyeClosed != null) eyeClosed.Visibility = Visibility.Visible;
        if (eyeOpen != null) eyeOpen.Visibility = Visibility.Collapsed;
    }

    // ════════════════════════════════════════════════════════════════
    // 输入框动态边框
    // ════════════════════════════════════════════════════════════════

    private void InputBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var w = grid.ActualWidth;
        var h = grid.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var geom = $"M{w:F3},5 H11 L5,11 V{h - 1:F3} H6.8 H{w - 6:F3} L{w:F3},{h - 7:F3} V5 Z";

        foreach (var child in grid.Children)
        {
            if (child is Path path)
            {
                path.Data = Geometry.Parse(geom);
                break;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 业务操作
    // ════════════════════════════════════════════════════════════════

    private void LoginButton_Click(object sender, RoutedEventArgs e) { }

    private void RegisterButton_Click(object sender, RoutedEventArgs e) { }

    private void TermsLink_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { }

    private void GetRegisterVerificationCode_Click(object sender, RoutedEventArgs e) { }

    // ════════════════════════════════════════════════════════════════
    // 找回密码表单
    // ════════════════════════════════════════════════════════════════

    private void ForgotPasswordInput_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        string? placeholderKey = tb.Name switch
        {
            "ForgotPasswordEmailTextBox" => "Login.EmailPlaceholder",
            "ForgotPasswordVerificationCodeTextBox" => "Settings.VerificationCodePlaceholder",
            _ => null
        };
        if (placeholderKey != null && tb.Text == LocalizationService.Instance[placeholderKey])
        {
            tb.Text = string.Empty;
            tb.Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238));
        }
    }

    private void ForgotPasswordInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        string? placeholderKey = tb.Name switch
        {
            "ForgotPasswordEmailTextBox" => "Login.EmailPlaceholder",
            "ForgotPasswordVerificationCodeTextBox" => "Settings.VerificationCodePlaceholder",
            _ => null
        };
        if (placeholderKey != null && string.IsNullOrWhiteSpace(tb.Text))
        {
            tb.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 238, 238, 238));
            tb.Text = LocalizationService.Instance[placeholderKey];
        }
    }

    private void ClearForgotPasswordInput_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            var element = FindName(name);
            if (element is TextBox tb)
            {
                tb.Text = string.Empty;
                tb.Focus();
            }
        }
    }

    private void GetForgotPasswordVerificationCode_Click(object sender, RoutedEventArgs e) { }

    private void ResetPasswordRequest_Click(object sender, RoutedEventArgs e)
    {
        ForgotPasswordStep1Panel.Visibility = Visibility.Collapsed;
        ForgotPasswordStep2Panel.Visibility = Visibility.Visible;
    }

    private void ForgotNewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ForgotNewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(ForgotNewPasswordBox.Password)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ForgotNewPasswordBox_GotFocus(object sender, RoutedEventArgs e)
        => ForgotNewPasswordPlaceholder.Visibility = Visibility.Collapsed;

    private void ForgotNewPasswordBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ForgotNewPasswordBox.Password))
            ForgotNewPasswordPlaceholder.Visibility = Visibility.Visible;
    }

    private void ClearForgotNewPassword_Click(object sender, RoutedEventArgs e)
    {
        ForgotNewPasswordBox.Password = string.Empty;
        ForgotNewPasswordBox.Focus();
        RemoveOverlayTextBox(ForgotNewPasswordBox, "overlay_ForgotNewPassword");
        ForgotNewPasswordBox.Visibility = Visibility.Visible;
        ResetEyeIcon(ForgotNewPasswordEyeBtn);
        ForgotNewPasswordPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void ToggleForgotNewPasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        TogglePasswordVisibilityInner(ForgotNewPasswordEyeBtn, ForgotNewPasswordBox, ForgotNewPasswordPlaceholder, "overlay_ForgotNewPassword");
    }

    private void ForgotConfirmNewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ForgotConfirmNewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(ForgotConfirmNewPasswordBox.Password)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ForgotConfirmNewPasswordBox_GotFocus(object sender, RoutedEventArgs e)
        => ForgotConfirmNewPasswordPlaceholder.Visibility = Visibility.Collapsed;

    private void ForgotConfirmNewPasswordBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ForgotConfirmNewPasswordBox.Password))
            ForgotConfirmNewPasswordPlaceholder.Visibility = Visibility.Visible;
    }

    private void ClearForgotConfirmNewPassword_Click(object sender, RoutedEventArgs e)
    {
        ForgotConfirmNewPasswordBox.Password = string.Empty;
        ForgotConfirmNewPasswordBox.Focus();
        RemoveOverlayTextBox(ForgotConfirmNewPasswordBox, "overlay_ForgotConfirmNewPassword");
        ForgotConfirmNewPasswordBox.Visibility = Visibility.Visible;
        ForgotConfirmNewPasswordPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void ToggleForgotConfirmNewPasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        TogglePasswordVisibilityInner(null, ForgotConfirmNewPasswordBox, ForgotConfirmNewPasswordPlaceholder, "overlay_ForgotConfirmNewPassword");
    }

    private void SetNewPasswordNext_Click(object sender, RoutedEventArgs e)
    {
        ForgotPasswordStep2Panel.Visibility = Visibility.Collapsed;
        ForgotPasswordStep3Panel.Visibility = Visibility.Visible;
    }

    private void PrivacyLink_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { }
}
