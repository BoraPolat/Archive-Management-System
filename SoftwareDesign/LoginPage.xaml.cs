using Microsoft.Maui.Controls;
using SoftwareDesign.Services;
using System;

namespace SoftwareDesign
{
    public partial class LoginPage : ContentPage
    {
        private readonly AuthService _authService;

        public LoginPage()
        {
            InitializeComponent();
            _authService = new AuthService();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Clear entries
            UsernameEntry.Text = string.Empty;
            PasswordEntry.Text = string.Empty;
            ErrorLabel.IsVisible = false;
            UsernameEntry.Focus();
        }

        private void OnUsernameCompleted(object sender, EventArgs e)
        {
            // Username'de Enter'a basılınca Password'e geç
            PasswordEntry.Focus();
        }

        private void OnPasswordCompleted(object sender, EventArgs e)
        {
            // Password'de Enter'a basılınca Login butonunu tetikle
            OnLoginClicked(sender, e);
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            ErrorLabel.IsVisible = false;

            var username = UsernameEntry.Text?.Trim();
            var password = PasswordEntry.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            // Disable button during login
            LoginButton.IsEnabled = false;
            LoginButton.Text = "Logging in...";

            try
            {
                bool isValid = await _authService.ValidateCredentialsAsync(username, password);

                if (isValid)
                {
                    // Başarılı giriş - MainPage'e yönlendir
                    Application.Current.MainPage = new AppShell();
                }
                else
                {
                    ShowError("Invalid username or password.");
                    PasswordEntry.Text = string.Empty;
                    PasswordEntry.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Login failed: {ex.Message}");
            }
            finally
            {
                LoginButton.IsEnabled = true;
                LoginButton.Text = "Login";
            }
        }

        private void ShowError(string message)
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        }
    }
}