using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using HITAPEX.Services;

namespace HITAPEX.Views;

/// <summary>
/// 登录/注册弹窗，支持登录和注册两个面板切换。
/// </summary>
public partial class LoginPopup : UserControl
{
    /// <summary>
    /// 忘记密码流程中身份确认（verify-stepup）签发的 step-up JWT（5 分钟有效）。
    /// 只用于下一步 reset-password，不是登录凭证；用后即弃。
    /// </summary>
    private string? _stepupJwt;

    /// <summary>邮箱格式校验正则。</summary>
    private static readonly Regex s_emailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

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
        ResetForgotPasswordInputs();
        LoginFormPanel.Visibility = Visibility.Collapsed;
        RegisterFormPanel.Visibility = Visibility.Collapsed;
        ForgotPasswordFormPanel.Visibility = Visibility.Visible;
        ForgotPasswordStep1Panel.Visibility = Visibility.Visible;
        ForgotPasswordStep2Panel.Visibility = Visibility.Collapsed;
        ForgotPasswordStep3Panel.Visibility = Visibility.Collapsed;
        // 重新开始流程（或 step-up 失效回退），作废旧的身份确认凭证
        _stepupJwt = null;
    }

    private void SwitchToForgotPassword_Click(object sender, RoutedEventArgs e) => SwitchToForgotPasswordPanel();

    private void SwitchToRegister_Click(object sender, RoutedEventArgs e) => SwitchToRegisterPanel();

    private void SwitchToLogin_Click(object sender, RoutedEventArgs e) => SwitchToLoginPanel();

    private void ResetLoginInputs()
    {
        ClearAllErrors();
        LoginEmailTextBox.Text = LocalizationService.Instance["Login.EmailPlaceholder"];
        LoginEmailTextBox.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 238, 238, 238));
        LoginPasswordBox.Password = string.Empty;
        LoginPasswordPlaceholder.Visibility = Visibility.Visible;
        AgreementCheckBox.IsChecked = false;
    }

    private void ResetRegisterInputs()
    {
        ClearAllErrors();
        ResetRegisterTextBox(RegisterEmailTextBox, "Login.EmailPlaceholder");
        ResetRegisterTextBox(RegisterUsernameTextBox, "Login.UsernamePlaceholder");
        RegisterPasswordBox.Password = string.Empty;
        RegisterPasswordPlaceholder.Visibility = Visibility.Visible;
        RegisterConfirmPasswordBox.Password = string.Empty;
        RegisterConfirmPasswordPlaceholder.Visibility = Visibility.Visible;
        RegisterAgreementCheckBox.IsChecked = false;
    }

    private void ResetForgotPasswordInputs()
    {
        ClearAllErrors();

        // 邮箱 / 验证码：恢复占位水印
        ForgotPasswordEmailTextBox.Text = LocalizationService.Instance["Login.EmailPlaceholder"];
        ForgotPasswordEmailTextBox.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 238, 238, 238));
        ForgotPasswordVerificationCodeTextBox.Text = LocalizationService.Instance["Settings.VerificationCodePlaceholder"];
        ForgotPasswordVerificationCodeTextBox.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 238, 238, 238));

        // 新密码 / 确认新密码：清空并显示水印，同时还原明文浮层与眼睛图标
        ResetPasswordBoxState(ForgotNewPasswordBox, ForgotNewPasswordPlaceholder, ForgotNewPasswordEyeBtn, "overlay_ForgotNewPassword");
        ResetPasswordBoxState(ForgotConfirmNewPasswordBox, ForgotConfirmNewPasswordPlaceholder, ForgotConfirmNewPasswordEyeBtn, "overlay_ForgotConfirmNewPassword");
    }

    /// <summary>重置密码框到初始状态：清空密码、移除明文浮层、还原眼睛图标、显示占位水印。</summary>
    private void ResetPasswordBoxState(PasswordBox pb, TextBlock placeholder, Button eyeBtn, string overlayTag)
    {
        pb.Password = string.Empty;
        RemoveOverlayTextBox(pb, overlayTag);
        pb.Visibility = Visibility.Visible;
        if (eyeBtn != null) ResetEyeIcon(eyeBtn);
        placeholder.Visibility = Visibility.Visible;
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
        if (IsErrorVisible(LoginEmailTextBox)) return;
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
        if (IsErrorVisible(tb)) return;
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
        TogglePasswordVisibilityInner(RegisterConfirmPasswordEyeBtn, RegisterConfirmPasswordBox, RegisterConfirmPasswordPlaceholder, "overlay_RegisterConfirmPassword");
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
    // 输入框错误提示（警告图标 + 文本，样式与账户设置一致；不改输入框样式）
    // ════════════════════════════════════════════════════════════════

    /// <summary>在文本框内显示错误提示：清空原文本，展示警告图标 + 错误文本。</summary>
    private void ShowTextError(TextBox tb, ContentControl overlay, string message)
    {
        tb.Text = string.Empty;
        overlay.Content = message;
        overlay.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 在密码框内显示错误提示：清空密码、隐藏水印、重置眼睛图标，
    /// 移除明文显示浮层，再展示警告图标 + 错误文本。
    /// </summary>
    private void ShowPasswordError(PasswordBox pb, TextBlock placeholder, Button? eyeBtn, ContentControl overlay, string message, string overlayTag)
    {
        pb.Password = string.Empty;
        if (placeholder != null) placeholder.Visibility = Visibility.Collapsed;
        if (eyeBtn != null) ResetEyeIcon(eyeBtn);
        RemoveOverlayTextBox(pb, overlayTag);
        pb.Visibility = Visibility.Visible;
        overlay.Content = message;
        overlay.Visibility = Visibility.Visible;
    }

    /// <summary>点击输入框（或其所在输入区域）时清除对应输入框的错误提示。</summary>
    private void Input_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string overlayName &&
            FindName(overlayName) is ContentControl overlay)
        {
            overlay.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>清空所有表单的错误提示（面板切换时调用）。</summary>
    private void ClearAllErrors()
    {
        foreach (string name in new[]
        {
            "LoginEmailErrorOverlay", "LoginPasswordErrorOverlay",
            "RegisterEmailErrorOverlay", "RegisterUsernameErrorOverlay",
            "RegisterVerificationCodeErrorOverlay", "RegisterPasswordErrorOverlay",
            "RegisterConfirmPasswordErrorOverlay",
            "ForgotPasswordEmailErrorOverlay", "ForgotPasswordVerificationCodeErrorOverlay",
            "ForgotNewPasswordErrorOverlay", "ForgotConfirmNewPasswordErrorOverlay"
        })
        {
            if (FindName(name) is ContentControl overlay)
                overlay.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>判断某文本框当前是否显示错误提示（错误显示中不恢复占位水印，避免重叠）。</summary>
    private bool IsErrorVisible(FrameworkElement input)
    {
        string? overlayName = input.Name switch
        {
            "LoginEmailTextBox" => "LoginEmailErrorOverlay",
            "RegisterEmailTextBox" => "RegisterEmailErrorOverlay",
            "RegisterUsernameTextBox" => "RegisterUsernameErrorOverlay",
            "RegisterVerificationCodeTextBox" => "RegisterVerificationCodeErrorOverlay",
            "ForgotPasswordEmailTextBox" => "ForgotPasswordEmailErrorOverlay",
            "ForgotPasswordVerificationCodeTextBox" => "ForgotPasswordVerificationCodeErrorOverlay",
            _ => null
        };
        return overlayName != null && FindName(overlayName) is ContentControl overlay
            && overlay.Visibility == Visibility.Visible;
    }

    /// <summary>简单邮箱格式校验。</summary>
    private static bool IsValidEmail(string email) => s_emailRegex.IsMatch(email);

    // ════════════════════════════════════════════════════════════════
    // 用户协议勾选校验（未勾选 → 抖动 checkbox 提醒）
    // ════════════════════════════════════════════════════════════════

    /// <summary>校验用户协议是否勾选：未勾选则抖动 checkbox 提醒。返回 true 表示未勾选（应中止登录/注册操作）。</summary>
    private bool CheckAgreement(CheckBox checkbox)
    {
        if (checkbox.IsChecked == true) return false;
        ShakeCheckBox(checkbox);
        return true;
    }

    /// <summary>水平抖动指定元素（左右快速摆动后归位）以吸引注意。</summary>
    private void ShakeCheckBox(FrameworkElement element)
    {
        var transform = element.RenderTransform as TranslateTransform;
        if (transform == null)
        {
            transform = new TranslateTransform();
            element.RenderTransform = transform;
        }
        // 停止上一次抖动动画
        transform.BeginAnimation(TranslateTransform.XProperty, null);

        var anim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(500) };
        anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(-10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(-8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480))));
        transform.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    // ════════════════════════════════════════════════════════════════
    // 业务操作
    // ════════════════════════════════════════════════════════════════

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[LoginPopup] === 登录按钮点击 ===");
        if (App.UserApi == null)
        {
            Debug.WriteLine("[LoginPopup] App.UserApi 为 null！请检查 App.xaml.cs 是否初始化了 UserApiService");
            return;
        }

        ClearAllErrors();

        // 用户协议未勾选 → 抖动提醒并中止登录
        if (CheckAgreement(AgreementCheckBox))
            return;

        var email = GetActualText(LoginEmailTextBox, "Login.EmailPlaceholder");
        var password = LoginPasswordBox.Password;
        Debug.WriteLine($"[LoginPopup] 邮箱={email}, 密码长度={password.Length}");

        // 先检查邮箱是否为空 → "邮箱不能为空"
        if (string.IsNullOrWhiteSpace(email))
        {
            Debug.WriteLine("[LoginPopup] 邮箱为空，显示错误提示");
            ShowTextError(LoginEmailTextBox, LoginEmailErrorOverlay, LocalizationService.Instance["Login.EmailRequired"]);
            return;
        }
        // 再检查密码是否为空 → "密码不能为空"
        if (password.Length == 0)
        {
            Debug.WriteLine("[LoginPopup] 密码为空，显示错误提示");
            ShowPasswordError(LoginPasswordBox, LoginPasswordPlaceholder, LoginPasswordEyeBtn,
                LoginPasswordErrorOverlay, LocalizationService.Instance["Login.PasswordRequired"], "overlay_LoginPassword");
            return;
        }

        Debug.WriteLine("[LoginPopup] 调用 LoginPasswordAsync...");
        Debug.WriteLine($"[LoginPopup] UserApi.IsLoggedIn (调用前): {App.UserApi.IsLoggedIn}");
        Debug.WriteLine($"[LoginPopup] UserApi.CurrentUser (调用前): {(App.UserApi.CurrentUser != null ? App.UserApi.CurrentUser.Username : "null")}");

        var result = await App.UserApi.LoginPasswordAsync(email, password);

        Debug.WriteLine($"[LoginPopup] LoginPasswordAsync 返回: IsSuccess={result.IsSuccess}");
        Debug.WriteLine($"[LoginPopup] Data?.Jwt: {(result.Data != null && !string.IsNullOrEmpty(result.Data.Jwt) ? "***" + result.Data.Jwt.Substring(result.Data.Jwt.Length - 10, 10) : "null")}");
        Debug.WriteLine($"[LoginPopup] Data?.User: {(result.Data?.User != null ? result.Data.User.Username + "/" + result.Data.User.Email : "null")}");
        Debug.WriteLine($"[LoginPopup] UserApi.IsLoggedIn (调用后): {App.UserApi.IsLoggedIn}");
        Debug.WriteLine($"[LoginPopup] UserApi.CurrentUser (调用后): {(App.UserApi.CurrentUser != null ? App.UserApi.CurrentUser.Username : "null")}");

        if (result.IsSuccess)
        {
            Debug.WriteLine("[LoginPopup] 登录成功，调用 LoginSuccessful()");
            LoginSuccessful();
        }
        else
        {
            Debug.WriteLine($"[LoginPopup] 登录失败: {result.ErrorMessage}");
            // 登录失败：清空密码并显示"邮箱或密码不正确"
            ShowPasswordError(LoginPasswordBox, LoginPasswordPlaceholder, LoginPasswordEyeBtn,
                LoginPasswordErrorOverlay, LocalizationService.Instance["Login.InvalidCredentials"], "overlay_LoginPassword");
        }
    }

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.UserApi == null) return;

        ClearAllErrors();

        // 用户协议未勾选 → 抖动提醒并中止注册
        if (CheckAgreement(RegisterAgreementCheckBox))
            return;

        var email = GetActualText(RegisterEmailTextBox, "Login.EmailPlaceholder");
        var code = GetActualText(RegisterVerificationCodeTextBox, "Settings.VerificationCodePlaceholder");

        // 兜底：邮箱为空（正常流程在获取验证码时已校验，此处只为接口能正常调用）
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowTextError(RegisterEmailTextBox, RegisterEmailErrorOverlay, LocalizationService.Instance["Login.EmailRequired"]);
            return;
        }
        // 验证码为空 → "当前验证码错误"
        if (string.IsNullOrWhiteSpace(code))
        {
            ShowTextError(RegisterVerificationCodeTextBox, RegisterVerificationCodeErrorOverlay, LocalizationService.Instance["Login.InvalidCode"]);
            return;
        }

        var verifyResult = await App.UserApi.VerifyOtpAsync(email, code);
        if (verifyResult.IsSuccess)
        {
            // 注册成功：不弹提示，直接完成登录并隐藏弹窗
            SwitchToLoginPanel();
            LoginSuccessful();
        }
        else
        {
            // 验证码错误 → "当前验证码错误"
            ShowTextError(RegisterVerificationCodeTextBox, RegisterVerificationCodeErrorOverlay, LocalizationService.Instance["Login.InvalidCode"]);
        }
    }

    private async void LoginSuccessful()
    {
        Debug.WriteLine("[LoginPopup] LoginSuccessful: 登录成功，隐藏弹窗，通知 MainWindow 刷新登录状态");

        // 登录接口返回的 user 不含头像，先拉取 users/me 补齐完整资料（含头像）再刷新 UI
        if (App.UserApi != null)
            await App.UserApi.RefreshCurrentUserAsync();

        // 登录成功时，通知正在显示的设置页面立刻刷新
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            // 立即刷新（如果设置页面当前正在显示）
            var settingsView = mainWindow.GetCurrentSettingsView();
            if (settingsView != null)
            {
                Debug.WriteLine("[LoginPopup] 设置页面当前可见，直接刷新");
                settingsView.RefreshLoginState();
            }
            else
            {
                Debug.WriteLine("[LoginPopup] 设置页面当前不可见，将在用户导航到设置时通过 Loaded 事件刷新");
            }
        }

        Hide();
    }

    private static string GetActualText(TextBox tb, string placeholderKey)
    {
        var placeholder = LocalizationService.Instance[placeholderKey];
        return tb.Text == placeholder ? "" : tb.Text;
    }

    private void TermsLink_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { }

    /// <summary>
    /// 启动"获取验证码"按钮倒计时：禁用按钮、半透明，每秒更新剩余秒数；
    /// 倒计时结束恢复按钮可用和本地化文案。用于发送成功后的 60 秒冷却、以及 OTP_COOLDOWN / 429 限流。
    /// </summary>
    /// <param name="button">获取验证码按钮</param>
    /// <param name="textBlockName">按钮模板内文案 TextBlock 的 x:Name</param>
    /// <param name="placeholderKey">倒计时结束后恢复显示的本地化文案键</param>
    /// <param name="seconds">倒计时总秒数</param>
    private void StartCodeCountdown(Button button, string textBlockName, string placeholderKey, int seconds)
    {
        if (button.Template?.FindName(textBlockName, button) is not TextBlock text)
            return;
        if (seconds <= 0) seconds = 60;

        button.IsEnabled = false;
        button.Opacity = 0.5;
        text.Text = $"{seconds}s";

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        var remaining = seconds;
        timer.Tick += (_, _) =>
        {
            remaining--;
            if (remaining <= 0)
            {
                timer.Stop();
                text.Text = LocalizationService.Instance[placeholderKey];
                button.IsEnabled = true;
                button.Opacity = 1.0;
            }
            else
            {
                text.Text = $"{remaining}s";
            }
        };
        timer.Start();
    }

    private async void GetRegisterVerificationCode_Click(object sender, RoutedEventArgs e)
    {
        if (App.UserApi == null) return;
        ClearAllErrors();
        var email = GetActualText(RegisterEmailTextBox, "Login.EmailPlaceholder");
        var username = GetActualText(RegisterUsernameTextBox, "Login.UsernamePlaceholder");
        var password = RegisterPasswordBox.Password;

        // 邮箱为空 → "邮箱不能为空"
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowTextError(RegisterEmailTextBox, RegisterEmailErrorOverlay, LocalizationService.Instance["Login.EmailRequired"]);
            return;
        }
        // 邮箱格式不正确 → "邮箱格式不正确"
        if (!IsValidEmail(email))
        {
            ShowTextError(RegisterEmailTextBox, RegisterEmailErrorOverlay, LocalizationService.Instance["Login.InvalidEmailFormat"]);
            return;
        }
        // 用户名长度不符或为空 → "请输入 2-20 个字符的用户名"
        if (string.IsNullOrWhiteSpace(username) || username.Length < 2 || username.Length > 20)
        {
            ShowTextError(RegisterUsernameTextBox, RegisterUsernameErrorOverlay, LocalizationService.Instance["Login.InvalidUsernameLength"]);
            return;
        }
        // 密码长度至少 8 位 → "请输入至少 8 位的密码"
        if (password.Length < 8)
        {
            ShowPasswordError(RegisterPasswordBox, RegisterPasswordPlaceholder, RegisterPasswordEyeBtn,
                RegisterPasswordErrorOverlay, LocalizationService.Instance["Login.PasswordTooShort"], "overlay_RegisterPassword");
            return;
        }
        // 确认密码与密码一致 → "两次输入的密码不一致"
        if (RegisterConfirmPasswordBox.Password != password)
        {
            ShowPasswordError(RegisterConfirmPasswordBox, RegisterConfirmPasswordPlaceholder, RegisterConfirmPasswordEyeBtn,
                RegisterConfirmPasswordErrorOverlay, LocalizationService.Instance["Login.PasswordMismatch"], "overlay_RegisterConfirmPassword");
            return;
        }

        // POST /api/auth/local/register-otp：创建未激活账号，同时向邮箱发送 6 位验证码
        var result = await App.UserApi.RegisterOtpAsync(email, username, password);
        if (result.IsSuccess)
        {
            // 发送成功 → 进入 60 秒冷却，按钮倒计时防重复点击
            StartCodeCountdown(GetRegisterCodeButton, "GetRegisterCodeText", "Settings.GetVerificationCode",
                result.RetryAfterSeconds ?? 60);
            return;
        }

        // 服务端校验密码长度失败（密码为空或不足 8 位）→ 内联提示到密码框
        if (result.ErrorCode == "VALIDATION_PASSWORD_LENGTH")
        {
            ShowPasswordError(RegisterPasswordBox, RegisterPasswordPlaceholder, RegisterPasswordEyeBtn,
                RegisterPasswordErrorOverlay, LocalizationService.Instance["Login.PasswordTooShort"], "overlay_RegisterPassword");
            return;
        }
        // 邮箱已被注册 → "该邮箱已被注册"
        if (result.ErrorCode == "AUTH_EMAIL_TAKEN")
        {
            ShowTextError(RegisterEmailTextBox, RegisterEmailErrorOverlay, LocalizationService.Instance["Login.EmailTaken"]);
            return;
        }
        // 用户名已被占用 → "该用户名已被占用"
        if (result.ErrorCode == "AUTH_USERNAME_TAKEN")
        {
            ShowTextError(RegisterUsernameTextBox, RegisterUsernameErrorOverlay, LocalizationService.Instance["Login.UsernameTaken"]);
            return;
        }

        // 限流 / 冷却 → 启动按钮倒计时（429 优先用服务端返回的 retry_after，冷却默认 60 秒）
        var cooldownSeconds = result.RetryAfterSeconds ?? (result.ErrorCode == "OTP_COOLDOWN" ? 60 : (int?)null);
        if (cooldownSeconds.HasValue)
        {
            StartCodeCountdown(GetRegisterCodeButton, "GetRegisterCodeText", "Settings.GetVerificationCode",
                cooldownSeconds.Value);
        }

        // 明确的业务错误码（校验失败 / 冷却中 / 限流 / 发送失败）→ 直接展示服务端文案
        switch (result.ErrorCode)
        {
            case "OTP_COOLDOWN":
            case "SYSTEM_RATE_LIMITED":
            case "VALIDATION_FIELD_MISSING":
            case "VALIDATION_EMAIL_FORMAT":
            case "VALIDATION_USERNAME_LENGTH":
            case "VALIDATION_USERNAME_FORMAT":
            case "OTP_SEND_FAILED":
                MessageBox.Show(result.ErrorMessage ?? "Failed to send code", "Register", MessageBoxButton.OK);
                return;
        }

        // 未识别的客户端错误（可能是"已注册未激活"场景）→ 尝试重发验证码（邮箱未被其他激活用户占用时有效）
        if (result.IsClientError)
        {
            await App.UserApi.ResendOtpAsync(email);
        }
        else
        {
            MessageBox.Show(result.ErrorMessage ?? "Failed to send code", "Register", MessageBoxButton.OK);
        }
    }

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
        if (IsErrorVisible(tb)) return;
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

    private async void GetForgotPasswordVerificationCode_Click(object sender, RoutedEventArgs e)
    {
        if (App.UserApi == null) return;
        ClearAllErrors();
        var email = GetActualText(ForgotPasswordEmailTextBox, "Login.EmailPlaceholder");

        // 邮箱为空 → "邮箱不能为空"
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowTextError(ForgotPasswordEmailTextBox, ForgotPasswordEmailErrorOverlay, LocalizationService.Instance["Login.EmailRequired"]);
            return;
        }
        // 邮箱格式不正确 → "邮箱格式不正确"
        if (!IsValidEmail(email))
        {
            ShowTextError(ForgotPasswordEmailTextBox, ForgotPasswordEmailErrorOverlay, LocalizationService.Instance["Login.InvalidEmailFormat"]);
            return;
        }

        var result = await App.UserApi.ForgotPasswordAsync(email);
        if (result.IsSuccess)
        {
            // 发送成功 → 60 秒冷却，按钮倒计时防重复点击
            StartCodeCountdown(GetForgotPasswordCodeButton, "GetForgotPasswordCodeText", "Settings.GetVerificationCode",
                result.RetryAfterSeconds ?? 60);
            return;
        }

        // 限流 / 冷却 → 启动按钮倒计时（429 优先用服务端返回的 retry_after，冷却默认 60 秒）
        var cooldownSeconds = result.RetryAfterSeconds ?? (result.ErrorCode == "OTP_COOLDOWN" ? 60 : (int?)null);
        if (cooldownSeconds.HasValue)
        {
            StartCodeCountdown(GetForgotPasswordCodeButton, "GetForgotPasswordCodeText", "Settings.GetVerificationCode",
                cooldownSeconds.Value);
        }

        // 其他错误 → 展示服务端文案
        MessageBox.Show(result.ErrorMessage ?? "Failed to send code", "Reset Password", MessageBoxButton.OK);
    }

    private async void ResetPasswordRequest_Click(object sender, RoutedEventArgs e)
    {
        if (App.UserApi == null) return;
        ClearAllErrors();
        var email = GetActualText(ForgotPasswordEmailTextBox, "Login.EmailPlaceholder");
        var code = GetActualText(ForgotPasswordVerificationCodeTextBox, "Settings.VerificationCodePlaceholder");

        // 先检查邮箱是否为空 → "邮箱不能为空"
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowTextError(ForgotPasswordEmailTextBox, ForgotPasswordEmailErrorOverlay, LocalizationService.Instance["Login.EmailRequired"]);
            return;
        }
        // 再检查邮箱格式是否正确 → "邮箱格式不正确"
        if (!IsValidEmail(email))
        {
            ShowTextError(ForgotPasswordEmailTextBox, ForgotPasswordEmailErrorOverlay, LocalizationService.Instance["Login.InvalidEmailFormat"]);
            return;
        }
        // 检查验证码（为空也视为有误）→ "当前验证码错误"
        if (string.IsNullOrWhiteSpace(code))
        {
            ShowTextError(ForgotPasswordVerificationCodeTextBox, ForgotPasswordVerificationCodeErrorOverlay, LocalizationService.Instance["Login.InvalidCode"]);
            return;
        }

        // 身份确认：校验验证码，通过后签发 step-up JWT（5 分钟有效），再进入第二步设置新密码
        var result = await App.UserApi.VerifyStepupAsync(email, code);
        if (result.IsSuccess && result.Data != null && !string.IsNullOrEmpty(result.Data.StepupJwt))
        {
            _stepupJwt = result.Data.StepupJwt;
            ForgotPasswordStep1Panel.Visibility = Visibility.Collapsed;
            ForgotPasswordStep2Panel.Visibility = Visibility.Visible;
            return;
        }

        // 验证码错误 / 过期 / 次数用尽 → 清空验证码输入框并显示"当前验证码错误"
        if (result.ErrorCode is "OTP_INVALID" or "OTP_MAX_ATTEMPTS" or "OTP_EXPIRED")
        {
            ShowTextError(ForgotPasswordVerificationCodeTextBox, ForgotPasswordVerificationCodeErrorOverlay, LocalizationService.Instance["Login.InvalidCode"]);
            return;
        }
        MessageBox.Show(result.ErrorMessage ?? "验证码错误", "Reset Password", MessageBoxButton.OK);
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
        TogglePasswordVisibilityInner(ForgotConfirmNewPasswordEyeBtn, ForgotConfirmNewPasswordBox, ForgotConfirmNewPasswordPlaceholder, "overlay_ForgotConfirmNewPassword");
    }

    private async void SetNewPasswordNext_Click(object sender, RoutedEventArgs e)
    {
        if (App.UserApi == null || string.IsNullOrEmpty(_stepupJwt)) return;

        ClearAllErrors();
        var newPassword = ForgotNewPasswordBox.Password;
        var confirmPassword = ForgotConfirmNewPasswordBox.Password;

        // 新密码长度至少 8 位 → "请输入至少 8 位的密码"
        if (newPassword.Length < 8)
        {
            ShowPasswordError(ForgotNewPasswordBox, ForgotNewPasswordPlaceholder, ForgotNewPasswordEyeBtn,
                ForgotNewPasswordErrorOverlay, LocalizationService.Instance["Login.PasswordTooShort"], "overlay_ForgotNewPassword");
            return;
        }
        // 确认新密码与密码一致 → "两次输入的密码不一致"
        if (confirmPassword != newPassword)
        {
            ShowPasswordError(ForgotConfirmNewPasswordBox, ForgotConfirmNewPasswordPlaceholder, ForgotConfirmNewPasswordEyeBtn,
                ForgotConfirmNewPasswordErrorOverlay, LocalizationService.Instance["Login.PasswordMismatch"], "overlay_ForgotConfirmNewPassword");
            return;
        }

        // 凭第一步身份确认签发的 step-up JWT 设置新密码（本接口不签发登录凭证，成功后回登录页用新密码登录）
        var result = await App.UserApi.ResetPasswordAsync(_stepupJwt, newPassword);
        if (result.IsSuccess)
        {
            _stepupJwt = null; // 用后即弃
            ForgotPasswordStep2Panel.Visibility = Visibility.Collapsed;
            ForgotPasswordStep3Panel.Visibility = Visibility.Visible;
            return;
        }

        // step-up 凭证缺失 / 无效 / 过期 → 身份确认已失效，回到第一步重新发码
        if (result.ErrorCode is "STEPUP_TOKEN_MISSING" or "STEPUP_TOKEN_INVALID" or "STEPUP_TOKEN_EXPIRED" or "STEPUP_WRONG_PURPOSE")
        {
            MessageBox.Show(result.ErrorMessage ?? "身份验证已失效，请重新操作", "Reset Password", MessageBoxButton.OK);
            SwitchToForgotPasswordPanel();
            return;
        }

        MessageBox.Show(result.ErrorMessage ?? "Password reset failed", "Reset Password", MessageBoxButton.OK);
    }

    private void PrivacyLink_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { }
}
