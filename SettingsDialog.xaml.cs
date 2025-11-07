using System;
using System.Windows;

namespace ITTicketingKiosk
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
            LoadExistingCredentials();
            this.Closing += SettingsDialog_Closing;
        }

        /// <summary>
        /// Clear sensitive data when dialog closes
        /// </summary>
        private void SettingsDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Clear sensitive fields from memory
            PSClientIdTextBox.Text = string.Empty;
            PSClientSecretBox.Password = string.Empty;
            NinjaClientIdTextBox.Text = string.Empty;
            NinjaClientSecretBox.Password = string.Empty;
        }

        /// <summary>
        /// Load existing credentials into the form
        /// </summary>
        private void LoadExistingCredentials()
        {
            try
            {
                // Load PowerSchool credentials
                string psClientId = CredentialManager.GetPowerSchoolClientId();
                if (!string.IsNullOrEmpty(psClientId))
                {
                    PSClientIdTextBox.Text = psClientId;
                }

                string psClientSecret = CredentialManager.GetPowerSchoolClientSecret();
                if (!string.IsNullOrEmpty(psClientSecret))
                {
                    PSClientSecretBox.Password = psClientSecret;
                }

                // Load NinjaOne credentials
                string ninjaClientId = CredentialManager.GetNinjaOneClientId();
                if (!string.IsNullOrEmpty(ninjaClientId))
                {
                    NinjaClientIdTextBox.Text = ninjaClientId;
                }

                string ninjaClientSecret = CredentialManager.GetNinjaOneClientSecret();
                if (!string.IsNullOrEmpty(ninjaClientSecret))
                {
                    NinjaClientSecretBox.Password = ninjaClientSecret;
                }

                // Load organization ID from Config (not stored separately in credential manager)
                NinjaOrgIdTextBox.Text = Config.NINJA_ORGANIZATION_ID;

                // Load printer enabled setting
                PrinterEnabledCheckBox.IsChecked = CredentialManager.GetPrinterEnabled();
            }
            catch (Exception ex)
            {
                // Ignore errors loading credentials
                System.Diagnostics.Debug.WriteLine($"Error loading credentials: {ex.Message}");
            }
        }

        /// <summary>
        /// Save button clicked - validate and save credentials
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(PSClientIdTextBox.Text))
                {
                    ShowError("PowerSchool Client ID is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(PSClientSecretBox.Password))
                {
                    ShowError("PowerSchool Client Secret is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(NinjaClientIdTextBox.Text))
                {
                    ShowError("NinjaOne Client ID is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(NinjaClientSecretBox.Password))
                {
                    ShowError("NinjaOne Client Secret is required");
                    return;
                }

                if (string.IsNullOrWhiteSpace(NinjaOrgIdTextBox.Text))
                {
                    ShowError("NinjaOne Organization ID is required");
                    return;
                }

                // Save credentials
                try
                {
                    // Save PowerSchool credentials
                    CredentialManager.SavePowerSchoolClientId(PSClientIdTextBox.Text.Trim());
                    CredentialManager.SavePowerSchoolClientSecret(PSClientSecretBox.Password);

                    // Save NinjaOne credentials
                    CredentialManager.SaveNinjaOneClientId(NinjaClientIdTextBox.Text.Trim());
                    CredentialManager.SaveNinjaOneClientSecret(NinjaClientSecretBox.Password);

                    // Update the organization ID in Config (will be used at runtime)
                    Config.NINJA_ORGANIZATION_ID = NinjaOrgIdTextBox.Text.Trim();

                    // Save printer enabled setting
                    CredentialManager.SavePrinterEnabled(PrinterEnabledCheckBox.IsChecked == true);

                    // Set dialog result and close
                    this.DialogResult = true;
                    this.Close();
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to save credentials: {ex.Message}");
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancel button clicked
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Clear all credentials button clicked
        /// </summary>
        private void ClearCredentialsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all stored credentials? You will need to re-enter them and sign in again.",
                "Confirm Clear Credentials",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    CredentialManager.ClearAllCredentials();

                    // Clear the form
                    PSClientIdTextBox.Text = string.Empty;
                    PSClientSecretBox.Password = string.Empty;
                    NinjaClientIdTextBox.Text = string.Empty;
                    NinjaClientSecretBox.Password = string.Empty;
                    NinjaOrgIdTextBox.Text = "2"; // Reset to default

                    MessageBox.Show(
                        "All credentials have been cleared successfully.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to clear credentials: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Show error message
        /// </summary>
        private void ShowError(string message)
        {
            MessageBox.Show(message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
