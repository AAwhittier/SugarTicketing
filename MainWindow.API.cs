using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ITTicketingKiosk
{
    /// <summary>
    /// API initialization and authentication logic for MainWindow
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Initialize application - check credentials and authentication status
        /// </summary>
        private async Task InitializeApplicationAsync()
        {
            try
            {
                // Check if credentials are configured
                if (!Config.AreCredentialsConfigured())
                {
                    AddStatusMessage(StatusMessageKey.CredentialsNotConfigured);
                    await ShowSettingsDialogAsync(isRequired: true);

                    // After settings dialog closes, check again
                    if (!Config.AreCredentialsConfigured())
                    {
                        // User still hasn't configured - show error and wait
                        AddStatusMessage(StatusMessageKey.ApplicationCannotStart);
                        ShowAuthOverlay();
                        return;
                    }
                }

                // Initialize APIs with stored credentials
                InitializeAPIs();

                // Check authentication status
                await CheckAuthenticationStatusAsync();
            }
            catch (Exception ex)
            {
                AddStatusMessage(StatusMessageKey.InitializationError, ex.Message);
                ShowAuthOverlay();
            }
        }

        private void InitializeAPIs()
        {
            try
            {
                // Dispose old API instances before creating new ones
                _psApi?.Dispose();
                _ninjaApi?.Dispose();

                // Clear any active OAuth tasks since we're creating new API instances
                _activeOAuthTask = null;

                // Cancel any ongoing OAuth attempts
                if (_oauthCancellationTokenSource != null)
                {
                    _oauthCancellationTokenSource.Cancel();
                    _oauthCancellationTokenSource.Dispose();
                    _oauthCancellationTokenSource = null;
                }

                // Get credentials from Credential Manager
                string psClientId = Config.GetPowerSchoolClientId();
                string psClientSecret = Config.GetPowerSchoolClientSecret();
                string ninjaClientId = Config.GetNinjaOneClientId();
                string ninjaClientSecret = Config.GetNinjaOneClientSecret();

                // Validate PowerSchool credentials
                if (string.IsNullOrEmpty(psClientId))
                {
                    throw new Exception("PowerSchool Client ID is not configured");
                }
                if (string.IsNullOrEmpty(psClientSecret))
                {
                    throw new Exception("PowerSchool Client Secret is not configured");
                }

                // Validate NinjaOne credentials
                if (string.IsNullOrEmpty(ninjaClientId))
                {
                    throw new Exception("NinjaOne Client ID is not configured");
                }
                if (string.IsNullOrEmpty(ninjaClientSecret))
                {
                    throw new Exception("NinjaOne Client Secret is not configured");
                }

                // Initialize PowerSchool API
                AddStatusMessage(StatusMessageKey.InitializingPowerSchool);
                _psApi = new PowerSchoolAPI(
                    Config.PS_BASE_URL,
                    psClientId,
                    psClientSecret
                );
                AddStatusMessage(StatusMessageKey.PowerSchoolInitialized);

                // Initialize NinjaOne API
                AddStatusMessage(StatusMessageKey.InitializingNinjaOne);
                _ninjaApi = new NinjaOneAPI(
                    Config.NINJA_BASE_URL,
                    ninjaClientId,
                    ninjaClientSecret,
                    Config.NINJA_ORGANIZATION_ID
                );

                // Load existing refresh token from Windows Credential Manager if it exists
                // This allows us to test new credentials with the existing token
                string existingRefreshToken = CredentialManager.GetNinjaOneRefreshToken();
                if (!string.IsNullOrEmpty(existingRefreshToken))
                {
                    _ninjaApi.SetRefreshToken(existingRefreshToken);
                }

                AddStatusMessage(StatusMessageKey.NinjaOneInitialized);

                AddStatusMessage(StatusMessageKey.AllAPIsInitialized);
            }
            catch (Exception ex)
            {
                AddStatusMessage(StatusMessageKey.FailedToInitializeAPIs, ex.Message);
                _psApi = null;
                _ninjaApi = null;
                throw; // Re-throw to be caught by caller
            }
        }

        /// <summary>
        /// Check if we have a valid refresh token stored
        /// </summary>
        private async Task CheckAuthenticationStatusAsync()
        {
            try
            {
                // Load refresh token from Windows Credential Manager
                string refreshToken = CredentialManager.GetNinjaOneRefreshToken();

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    _ninjaApi.SetRefreshToken(refreshToken);

                    // Try to use it - make a test API call
                    try
                    {
                        // Attempt a simple lookup to validate token
                        await _ninjaApi.LookupEndUserAsync("test_validation");

                        // Token is valid
                        HideAuthOverlay();
                        AddStatusMessage(StatusMessageKey.AuthenticatedReady);

                        // Initialize username cache and kiosk user
                        _ = UpdateUsernameCacheAsync();
                        _ = LookupKioskUserAsync();
                        return;
                    }
                    catch (Exception ex)
                    {
                        // Token invalid or expired - clear it from storage
                        AddStatusMessage(StatusMessageKey.RefreshTokenInvalid, ex.Message);
                        CredentialManager.ClearNinjaOneRefreshToken();

                        // Also clear from API instance
                        _ninjaApi.SetRefreshToken(null);
                    }
                }

                // No valid refresh token - show auth overlay
                ShowAuthOverlay();
                AddStatusMessage(StatusMessageKey.AuthenticationRequired);
            }
            catch (Exception ex)
            {
                AddStatusMessage(StatusMessageKey.ErrorCheckingAuthentication, ex.Message);
                ShowAuthOverlay();
            }
        }

        /// <summary>
        /// Handle Sign In button click
        /// </summary>
        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // If credentials aren't configured, open settings instead
                if (!Config.AreCredentialsConfigured() || _ninjaApi == null)
                {
                    AddStatusMessage(StatusMessageKey.OpeningSettings);
                    await ShowSettingsDialogAsync(isRequired: true);
                    return;
                }

                // If there's an active OAuth task still running, wait for it to complete first
                if (_activeOAuthTask != null && !_activeOAuthTask.IsCompleted)
                {
                    AddStatusMessage(StatusMessageKey.AuthenticationError, "Waiting for previous authentication attempt to complete...");
                    try
                    {
                        // Wait up to 3 seconds for the previous task to complete
                        await Task.WhenAny(_activeOAuthTask, Task.Delay(3000));
                    }
                    catch
                    {
                        // Ignore errors from previous task
                    }

                    // Give additional time for listener cleanup
                    await Task.Delay(1000);
                }

                // Cancel any existing cancellation token
                if (_oauthCancellationTokenSource != null)
                {
                    _oauthCancellationTokenSource.Cancel();
                    _oauthCancellationTokenSource.Dispose();
                    _oauthCancellationTokenSource = null;
                }

                // Create new cancellation token source for this OAuth attempt
                _oauthCancellationTokenSource = new System.Threading.CancellationTokenSource();

                SignInButton.IsEnabled = false;
                AuthProgressRing.IsIndeterminate = true;
                AuthProgressRing.Visibility = Visibility.Visible;
                AuthStatusText.Text = "Opening browser for authentication...";
                AuthStatusText.Visibility = Visibility.Visible;

                // Re-enable button after 5 seconds so user can retry if they closed the browser
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    Dispatcher.Invoke(() => SignInButton.IsEnabled = true);
                });

                // Track this OAuth attempt
                _activeOAuthTask = PerformOAuthFlowAsync();
                await _activeOAuthTask;
            }
            catch (Exception ex)
            {
                HandleUnexpectedError(ex, "authentication");
            }
        }

        private async Task PerformOAuthFlowAsync()
        {
            try
            {
                AddStatusMessage(StatusMessageKey.StartingOAuthFlow);

                // Start OAuth flow - opens browser and listens for callback
                string authCode = await _ninjaApi.StartOAuthFlowAsync();

                AuthStatusText.Text = "Exchanging authorization code for tokens...";
                AddStatusMessage(StatusMessageKey.AuthorizationCodeReceived);

                // Exchange code for access and refresh tokens
                await _ninjaApi.ExchangeCodeForTokensAsync(authCode);

                // Save refresh token to Windows Credential Manager
                string refreshToken = _ninjaApi.GetRefreshToken();
                CredentialManager.SaveNinjaOneRefreshToken(refreshToken);

                AuthStatusText.Text = "Authentication successful!";
                AddStatusMessage(StatusMessageKey.AuthenticatedSuccessfully);

                // Hide overlay after short delay
                await Task.Delay(1000);
                HideAuthOverlay();
                AddStatusMessage(StatusMessageKey.ReadyToCreateTickets);

                // Initialize username cache and kiosk user
                _ = UpdateUsernameCacheAsync();
                _ = LookupKioskUserAsync();
            }
            catch (TimeoutException)
            {
                AuthStatusText.Text = "Authentication timed out. Please try again.";
                AuthStatusText.Foreground = new SolidColorBrush(Colors.Red);
                AddStatusMessage(StatusMessageKey.AuthenticationTimedOut);
            }
            catch (ObjectDisposedException)
            {
                // HttpListener was disposed from a previous attempt - this is expected
                AuthStatusText.Text = "Please click Sign In again to retry authentication.";
                AuthStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                AddStatusMessage(StatusMessageKey.AuthenticationError, "Previous authentication session closed. Please try again.");
            }
            catch (System.Net.HttpListenerException ex)
            {
                // Handle port conflicts or listener issues
                AuthStatusText.Text = "Authentication error. Please wait a moment and try again.";
                AuthStatusText.Foreground = new SolidColorBrush(Colors.Red);
                AddStatusMessage(StatusMessageKey.AuthenticationError, $"Listener error: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Check if this is a state mismatch error
                if (ex.Message.Contains("OAuth state mismatch"))
                {
                    AuthStatusText.Text = "State mismatch detected. Please click Sign In again to retry.";
                    AuthStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                    AddStatusMessage(StatusMessageKey.AuthenticationError, "OAuth state mismatch - likely from a previous attempt. Please retry authentication.");
                }
                else
                {
                    AuthStatusText.Text = $"Authentication failed: {ex.Message}";
                    AuthStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    AddStatusMessage(StatusMessageKey.AuthenticationError, ex.Message);
                    await ShowMessageDialog(PopupMessageKey.AuthenticationError, ex.Message);
                }
            }
            finally
            {
                SignInButton.IsEnabled = true;
                AuthProgressRing.IsIndeterminate = false;
                AuthProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowAuthOverlay()
        {
            AuthOverlay.Visibility = Visibility.Visible;
            AuthStatusText.Visibility = Visibility.Collapsed;
            AuthStatusText.Foreground = (Brush)FindResource("AccentTextBrush");

            // Update description based on credential status
            if (!Config.AreCredentialsConfigured())
            {
                AuthDescriptionText.Text = "API credentials are not configured.\n\nClick the Settings button (⚙️) to enter your PowerSchool and NinjaOne credentials.";
                SignInButton.Content = "Open Settings";
            }
            else
            {
                AuthDescriptionText.Text = "Please sign in with your NinjaOne account to access the ticketing system.\n\nYour browser will open for authentication.";
                SignInButton.Content = "Sign In";
            }
        }

        private void HideAuthOverlay()
        {
            AuthOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Updates the cached username list from NinjaOne
        /// </summary>
        private async Task UpdateUsernameCacheAsync()
        {
            try
            {
                var allUsers = await _ninjaApi.GetAllUsersAsync();

                // Extract usernames from emails (part before @)
                _cachedUsernames = allUsers
                    .Where(u => !string.IsNullOrEmpty(u.Email) && u.Email.Contains("@"))
                    .Select(u => u.Email.Split('@')[0])
                    .Distinct()
                    .OrderBy(u => u)
                    .ToList();

                // Only show in developer/test mode
                if (_testModeEnabled)
                {
                    AddStatusMessage(StatusMessageKey.CachedUsernames, _cachedUsernames.Count);
                }
                System.Diagnostics.Debug.WriteLine($"[Autocomplete] Cached {_cachedUsernames.Count} usernames");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Autocomplete] Failed to update cache: {ex.Message}");
                // Don't show error to user - autocomplete is optional feature
            }
        }

        /// <summary>
        /// Lookup and cache the 'kiosk' user from NinjaOne for fallback ticket creation
        /// </summary>
        private async Task LookupKioskUserAsync()
        {
            try
            {
                _kioskUser = await _ninjaApi.LookupEndUserAsync("kiosk");

                if (_kioskUser != null && !string.IsNullOrEmpty(_kioskUser.Uid))
                {
                    System.Diagnostics.Debug.WriteLine($"[Kiosk] Successfully cached kiosk user: {_kioskUser.FullName} ({_kioskUser.Uid})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Kiosk] Warning: 'kiosk' user not found in NinjaOne");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Kiosk] Failed to lookup kiosk user: {ex.Message}");
                // Don't show error to user - kiosk fallback is a background feature
            }
        }

        /// <summary>
        /// Show settings dialog for credential configuration
        /// </summary>
        private async Task ShowSettingsDialogAsync(bool isRequired)
        {
            try
            {
                SettingsDialog dialog = new SettingsDialog
                {
                    Owner = this
                };

                // Show the dialog
                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    // Credentials were saved
                    AddStatusMessage(StatusMessageKey.CredentialsSaved);

                    // Re-initialize APIs with new credentials
                    try
                    {
                        AddStatusMessage(StatusMessageKey.ReinitializingAPIs);
                        InitializeAPIs();

                        // Test PowerSchool credentials
                        AddStatusMessage(StatusMessageKey.TestingPowerSchoolCredentials);
                        bool psValid = await _psApi.TestCredentialsAsync();
                        if (!psValid)
                        {
                            throw new Exception("PowerSchool credentials are invalid. Please check your Client ID and Client Secret.");
                        }
                        AddStatusMessage(StatusMessageKey.PowerSchoolCredentialsValid);

                        // Test NinjaOne credentials (if refresh token exists)
                        AddStatusMessage(StatusMessageKey.TestingNinjaOneCredentials);
                        bool ninjaValid = await _ninjaApi.TestCredentialsAsync();
                        if (!ninjaValid)
                        {
                            // Check if we have a refresh token - affects error message
                            string existingToken = CredentialManager.GetNinjaOneRefreshToken();
                            if (!string.IsNullOrEmpty(existingToken))
                            {
                                throw new Exception("Failed to validate NinjaOne credentials with existing refresh token. This could mean:\n\n" +
                                    "1. The Client ID or Client Secret is incorrect\n" +
                                    "2. The stored refresh token has expired\n\n" +
                                    "Please verify your credentials are correct. If they are, you may need to sign in again to obtain a new refresh token.");
                            }
                            else
                            {
                                throw new Exception("NinjaOne credentials are invalid. Please check your Client ID and Client Secret.");
                            }
                        }
                        AddStatusMessage(StatusMessageKey.NinjaOneCredentialsValid);

                        // Check NinjaOne auth status after initialization
                        await CheckAuthenticationStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        string errorDetails = $"Failed to initialize APIs:\n\n{ex.Message}";

                        // Add inner exception details if available
                        if (ex.InnerException != null)
                        {
                            errorDetails += $"\n\nInner Exception: {ex.InnerException.Message}";
                        }

                        AddStatusMessage(StatusMessageKey.InitializationFailed, ex.Message);

                        await ShowMessageDialog(PopupMessageKey.InitializationError, errorDetails);

                        // If this was required setup and failed, show guidance
                        if (isRequired)
                        {
                            var retryResult = MessageBox.Show(
                                "API initialization failed. Please verify:\n\n" +
                                "1. Credentials are correct\n" +
                                "2. Organization ID is correct\n" +
                                "3. Network connection is available\n\n" +
                                "Would you like to try again?",
                                "Setup Failed",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (retryResult == MessageBoxResult.Yes)
                            {
                                await ShowSettingsDialogAsync(isRequired: true);
                            }
                        }
                    }
                }
                else if (isRequired)
                {
                    // User cancelled during required setup - show warning and try again
                    await ShowMessageDialog(PopupMessageKey.SetupRequired);
                    await ShowSettingsDialogAsync(isRequired: true);
                }
            }
            catch (Exception ex)
            {
                AddStatusMessage(StatusMessageKey.ErrorShowingSettings, ex.Message);

                // If settings dialog fails to show during required setup, the app needs relaunched
                if (isRequired)
                {
                    await ShowMessageDialog(PopupMessageKey.CriticalError, $"Failed to display settings dialog: {ex.Message}\n\nThe application cannot continue without credentials.");
                }
            }
        }

        /// <summary>
        /// Settings button click handler
        /// </summary>
        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ShowSettingsDialogAsync(isRequired: false);
            }
            catch (Exception ex)
            {
                HandleUnexpectedError(ex, "settings");
            }
        }
    }
}
