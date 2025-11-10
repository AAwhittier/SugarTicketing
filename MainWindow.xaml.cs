using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ITTicketingKiosk
{
    public partial class MainWindow : Window
    {
        private PowerSchoolAPI _psApi;
        private NinjaOneAPI _ninjaApi;
        private UserData _currentUser;
        private NinjaEndUser _currentNinjaUser;
        private int _currentPage = 1;
        private bool _testModeEnabled = false;
        private bool _settingsUnlocked = false;
        private bool _isDeviceWriteInMode = false;
        private List<string> _cachedUsernames = new List<string>();
        private NinjaEndUser _kioskUser;

        public MainWindow()
        {
            InitializeComponent();
            LoadBannerImage();

            // Disable settings button by default
            SettingsButton.IsEnabled = false;

            // Add keyboard event handler for F1 bypass
            this.KeyDown += MainWindow_KeyDown;

            // Initialize application - check credentials and authentication
            _ = InitializeApplicationAsync();
        }

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

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1)
            {
                _testModeEnabled = !_testModeEnabled;

                // If F1 is disabled, also lock settings
                if (!_testModeEnabled)
                {
                    _settingsUnlocked = false;
                    SettingsButton.IsEnabled = false;
                    AddStatusMessage(StatusMessageKey.TestModeDisabled);
                }
                else
                {
                    AddStatusMessage(StatusMessageKey.TestModeEnabled);
                }

                UpdateNavigationButtons();
                e.Handled = true;
            }
            else if (e.Key == Key.F12 && _testModeEnabled)
            {
                _settingsUnlocked = !_settingsUnlocked;
                SettingsButton.IsEnabled = _settingsUnlocked;

                AddStatusMessage(_settingsUnlocked ? StatusMessageKey.SettingsUnlocked : StatusMessageKey.SettingsLocked);
                e.Handled = true;
            }
        }

        private void InitializeAPIs()
        {
            try
            {
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
                        // Token invalid or expired
                        AddStatusMessage(StatusMessageKey.RefreshTokenInvalid, ex.Message);
                        CredentialManager.ClearNinjaOneRefreshToken();
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
            // If credentials aren't configured, open settings instead
            if (!Config.AreCredentialsConfigured() || _ninjaApi == null)
            {
                AddStatusMessage(StatusMessageKey.OpeningSettings);
                await ShowSettingsDialogAsync(isRequired: true);
                return;
            }

            SignInButton.IsEnabled = false;
            AuthProgressRing.IsIndeterminate = true;
            AuthProgressRing.Visibility = Visibility.Visible;
            AuthStatusText.Text = "Opening browser for authentication...";
            AuthStatusText.Visibility = Visibility.Visible;

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
            catch (Exception ex)
            {
                AuthStatusText.Text = $"Authentication failed: {ex.Message}";
                AuthStatusText.Foreground = new SolidColorBrush(Colors.Red);
                AddStatusMessage(StatusMessageKey.AuthenticationError, ex.Message);
                await ShowMessageDialog(PopupMessageKey.AuthenticationError, ex.Message);
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

        private void LoadBannerImage()
        {
            try
            {
                string appPath = AppContext.BaseDirectory;
                string imagePath = Path.Combine(appPath, "Assets", "sugar_salem_technology_banner.png");

                if (File.Exists(imagePath))
                {
                    var bitmap = new BitmapImage(new Uri(imagePath));
                    BannerImage.Source = bitmap;
                }
                else
                {
                    // Image not found, use fallback
                    throw new FileNotFoundException($"Image not found at: {imagePath}");
                }
            }
            catch (Exception ex)
            {
                // Use fallback text if image not found
                System.Diagnostics.Debug.WriteLine($"Failed to load banner image: {ex.Message}");
                BannerImage.Visibility = Visibility.Collapsed;
                FallbackBanner.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Handle Enter key press in Username field to trigger search
        /// </summary>
        private void UsernameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Trigger search button click
                SearchButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                await ShowMessageDialog(PopupMessageKey.InputRequired);
                AddStatusMessage(StatusMessageKey.PleaseEnterUsername);
                return;
            }

            // Validate username length (minimum 4, maximum 50)
            if (username.Length < 4)
            {
                await ShowMessageDialog(PopupMessageKey.UsernameTooShort);
                AddStatusMessage(StatusMessageKey.UsernameTooShort);
                return;
            }

            if (username.Length > 50)
            {
                await ShowMessageDialog(PopupMessageKey.UsernameTooLong);
                AddStatusMessage(StatusMessageKey.UsernameTooLong);
                return;
            }

            // Parse username from email format if @ is present
            username = ParseUsername(username);

            SearchButton.IsEnabled = false;
            AddStatusMessage(StatusMessageKey.SearchingForUser, username);

            try
            {
                // Query PowerSchool for device information
                AddStatusMessage(StatusMessageKey.QueryingPowerSchool);
                _currentUser = await _psApi.LookupDevicesAsync(username);

                // Query NinjaOne for user name and email
                AddStatusMessage(StatusMessageKey.QueryingNinjaOne);
                _currentNinjaUser = await _ninjaApi.LookupEndUserAsync(username);

                // Debug logging for NinjaOne lookup
                if (_currentNinjaUser != null)
                {
                    AddStatusMessage(StatusMessageKey.FoundNinjaOneUser, _currentNinjaUser.FullName, _currentNinjaUser.Email);
                }
                else
                {
                    AddStatusMessage(StatusMessageKey.NinjaOneUserNotFound, username);
                }

                // Check if we found the user in at least one system
                if (_currentUser != null || _currentNinjaUser != null)
                {
                    PopulateUserInfo();
                    ContinueButton.IsEnabled = true;
                    UpdateNavigationButtons();

                    // Log what we found
                    if (_currentUser != null && _currentNinjaUser != null)
                    {
                        AddStatusMessage(StatusMessageKey.UserFoundInBoth);
                    }
                    else if (_currentUser != null)
                    {
                        AddStatusMessage(StatusMessageKey.UserFoundPowerSchoolOnly);
                    }
                    else
                    {
                        AddStatusMessage(StatusMessageKey.UserFoundNinjaOneOnly);
                    }
                }
                else
                {
                    await ShowMessageDialog(PopupMessageKey.UserNotFound);
                    AddStatusMessage(StatusMessageKey.UserNotFoundInEither, username);
                    ClearUserInfo();
                    ContinueButton.IsEnabled = false;
                    UpdateNavigationButtons();
                }
            }
            catch (Exception ex)
            {
                await ShowMessageDialog(PopupMessageKey.SearchError, $"Failed to search:\n{ex.Message}");
                AddStatusMessage(StatusMessageKey.ErrorDuringSearch, ex.Message);
                ClearUserInfo();
            }
            finally
            {
                SearchButton.IsEnabled = true;

                // Update username cache after search (whether successful or not)
                _ = UpdateUsernameCacheAsync();
            }
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

                AddStatusMessage(StatusMessageKey.CachedUsernames, _cachedUsernames.Count);
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
        /// Handle text changes in Username field for autocomplete
        /// </summary>
        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = UsernameTextBox.Text;

            // Only show autocomplete if 3+ characters and we have cached usernames
            if (string.IsNullOrEmpty(input) || input.Length < 3 || _cachedUsernames.Count == 0)
            {
                UsernameAutocompletePopup.IsOpen = false;
                return;
            }

            // Filter usernames that start with the input (case-insensitive)
            var filteredUsernames = _cachedUsernames
                .Where(u => u.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .ToList();

            if (filteredUsernames.Count > 0)
            {
                UsernameAutocompleteList.ItemsSource = filteredUsernames;
                UsernameAutocompletePopup.IsOpen = true;
            }
            else
            {
                UsernameAutocompletePopup.IsOpen = false;
            }
        }

        /// <summary>
        /// Handle selection from autocomplete list
        /// </summary>
        private void UsernameAutocompleteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsernameAutocompleteList.SelectedItem != null)
            {
                string selectedUsername = UsernameAutocompleteList.SelectedItem.ToString();
                UsernameTextBox.Text = selectedUsername;
                UsernameTextBox.CaretIndex = selectedUsername.Length;
                UsernameAutocompletePopup.IsOpen = false;
            }
        }

        /// <summary>
        /// Handle mouse click on autocomplete item
        /// </summary>
        private void UsernameAutocompleteList_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (UsernameAutocompleteList.SelectedItem != null)
            {
                string selectedUsername = UsernameAutocompleteList.SelectedItem.ToString();
                UsernameTextBox.Text = selectedUsername;
                UsernameTextBox.CaretIndex = selectedUsername.Length;
                UsernameAutocompletePopup.IsOpen = false;
                UsernameTextBox.Focus();
            }
        }

        private void PopulateUserInfo()
        {
            // Populate name and email from NinjaOne if available
            if (_currentNinjaUser != null)
            {
                NameLabel.Text = _currentNinjaUser.FullName;
                EmailLabel.Text = _currentNinjaUser.Email;
            }
            else
            {
                // Fallback to username if NinjaOne data not available
                NameLabel.Text = _currentUser?.Username ?? "Unknown";
                EmailLabel.Text = "Not found in NinjaOne";
            }

            // Populate School Affiliation dropdown
            bool hasValidSchools = _currentUser != null &&
                                    _currentUser.SchoolIds != null &&
                                    _currentUser.SchoolIds.Any() &&
                                    !(_currentUser.SchoolIds.Count == 1 && _currentUser.SchoolIds[0] == "0");

            if (hasValidSchools)
            {
                // User has valid school(s) from PowerSchool - filter out "0" if present
                var schoolNames = _currentUser.SchoolIds
                    .Where(id => id != "0")  // Filter out "0"
                    .Select(GetSchoolName)
                    .ToList();

                // If filtering removed all schools, treat as no valid schools
                if (schoolNames.Any())
                {
                    SchoolAffiliationComboBox.ItemsSource = schoolNames;

                    // Select first school by default
                    SchoolAffiliationComboBox.SelectedIndex = 0;

                    if (schoolNames.Count > 1)
                    {
                        AddStatusMessage(StatusMessageKey.UserAffiliatedWithSchools, schoolNames.Count, string.Join(", ", schoolNames));
                    }
                }
                else
                {
                    // All schools were "0" - show full list
                    ShowAllSchoolOptions();
                }
            }
            else
            {
                // School is "0" or not found - provide all school options for user to select
                ShowAllSchoolOptions();
            }

            // Populate devices from PowerSchool
            var deviceList = new List<string>();

            // Add devices from PowerSchool if available
            if (_currentUser != null && _currentUser.Devices != null && _currentUser.Devices.Any())
            {
                deviceList.AddRange(_currentUser.Devices);

                string userTypeDisplay = _currentUser.UserType switch
                {
                    "students" => "student",
                    "teachers" => "teacher",
                    "users" => "user",
                    _ => "user"
                };

                AddStatusMessage(StatusMessageKey.FoundDevices, _currentUser.Devices.Count, userTypeDisplay, _currentUser.Username);
            }
            else
            {
                AddStatusMessage(StatusMessageKey.NoDevicesFound);
            }

            // Always add "Other" and "Write In" options
            deviceList.Add("Other");
            deviceList.Add("Write In");

            DeviceComboBox.ItemsSource = deviceList;

            // Select first device if available, otherwise select "Other"
            if (_currentUser != null && _currentUser.Devices != null && _currentUser.Devices.Any())
            {
                DeviceComboBox.SelectedIndex = 0;
            }
            else
            {
                // Select "Other" by default if no devices
                DeviceComboBox.SelectedIndex = deviceList.Count - 2; // "Other" is second to last
            }

            // Update navigation buttons now that fields are populated
            UpdateNavigationButtons();
        }

        private string GetSchoolName(string schoolId)
        {
            // Map school IDs to school names
            return schoolId switch
            {
                "147" => "SSHS - High School",
                "226" => "SSJHS - Junior High",
                "781" => "CES - Central Elementary",
                "225" => "KIS - Kershaw",
                "874" => "VVHS - Valley View",
                "1483" => "SSO - Online",
                "10000" => "DIS - District",
                _ => schoolId  // Just return the school ID number without "School ID:" prefix
            };
        }

        /// <summary>
        /// Show all available school options in the dropdown (when user's school is unknown)
        /// </summary>
        private void ShowAllSchoolOptions()
        {
            var allSchools = new List<string>
            {
                "SSHS",    // 147
                "SSJHS",   // 226
                "CES",     // 781
                "KIS",     // 225
                "VVHS",    // 874
                "SSO",     // 1483
                "DIS"      // 10000
            };

            SchoolAffiliationComboBox.ItemsSource = allSchools;
            SchoolAffiliationComboBox.SelectedIndex = -1; // No default selection - user must choose

            AddStatusMessage(StatusMessageKey.SchoolAffiliationNotFound);
        }

        /// <summary>
        /// Parse username from email format if @ is present
        /// Handles both username and email@domain.com formats
        /// </summary>
        private string ParseUsername(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // If input contains @, extract just the username part (before @)
            int atIndex = input.IndexOf('@');
            if (atIndex > 0)
            {
                string username = input.Substring(0, atIndex);
                AddStatusMessage(StatusMessageKey.ParsedUsernameFromEmail, username);
                return username;
            }

            // Return original input if no @ found
            return input;
        }

        private void ClearUserInfo()
        {
            NameLabel.Text = string.Empty;
            EmailLabel.Text = string.Empty;
            SchoolAffiliationComboBox.ItemsSource = null;
            SchoolAffiliationComboBox.SelectedIndex = -1;
            DeviceComboBox.ItemsSource = null;
            DeviceComboBox.SelectedIndex = -1;
            DeviceComboBox.IsEditable = false;
            _isDeviceWriteInMode = false;
            _currentUser = null;
            _currentNinjaUser = null;
            ContinueButton.IsEnabled = false;
            UpdateNavigationButtons();
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateForm())
                return;

            // Disable button immediately to prevent double-submission
            SubmitButton.IsEnabled = false;
            AddStatusMessage(StatusMessageKey.SubmittingTicket);

            try
            {
                // Determine requester information - use kiosk fallback if needed
                string requesterUid = null;
                string requesterName = null;
                string requesterEmail = null;

                if (_currentNinjaUser != null && !string.IsNullOrEmpty(_currentNinjaUser.Uid))
                {
                    // Normal case: use the actual user from NinjaOne
                    requesterUid = _currentNinjaUser.Uid;
                    requesterName = _currentNinjaUser.FullName;
                    requesterEmail = _currentNinjaUser.Email;
                }
                else if (_currentUser != null && _kioskUser != null && !string.IsNullOrEmpty(_kioskUser.Uid))
                {
                    // Fallback case: user exists in PowerSchool but not NinjaOne
                    // Use kiosk account for ticket submission
                    requesterUid = _kioskUser.Uid;
                    requesterName = _kioskUser.FullName;
                    requesterEmail = _kioskUser.Email;
                    AddStatusMessage(StatusMessageKey.UsingKioskFallback, _currentUser.Username);
                }
                else
                {
                    throw new Exception("No NinjaOne user information available. User must exist in NinjaOne to create tickets.");
                }

                // Get device - handle both selected item and custom text entry
                string deviceValue = string.Empty;
                if (DeviceComboBox.SelectedItem != null)
                {
                    deviceValue = DeviceComboBox.SelectedItem.ToString();
                }
                else if (!string.IsNullOrWhiteSpace(DeviceComboBox.Text))
                {
                    // User typed custom device name
                    deviceValue = DeviceComboBox.Text.Trim();
                }

                // Get selected school affiliation
                string schoolAffiliation = string.Empty;
                if (SchoolAffiliationComboBox.SelectedItem != null)
                {
                    schoolAffiliation = SchoolAffiliationComboBox.SelectedItem.ToString();
                }

                // Description is just the user's text - no prefixes needed
                string description = DescriptionTextBox.Text.Trim();

                // Create ticket using proper API structure with custom fields
                var ticket = await _ninjaApi.CreateTicketAsync(
                    subject: SubjectTextBox.Text.Trim(),
                    body: description,
                    requesterUid: requesterUid,
                    requesterName: requesterName,
                    requesterEmail: requesterEmail,
                    schoolAffiliation: schoolAffiliation,
                    deviceName: deviceValue,
                    studentNumber: _currentUser?.StudentNumber,
                    teacherNumber: _currentUser?.TeacherNumber
                );

                string ticketId = ticket.ContainsKey("id") ? ticket["id"].ToString() : "N/A";
                await ShowMessageDialog(PopupMessageKey.TicketSuccess, ticketId);
                AddStatusMessage(StatusMessageKey.TicketCreatedSuccessfully, ticketId, requesterName);

                // Print ticket receipt if enabled
                if (ReceiptPrinter.IsEnabled())
                {
                    string deviceForPrint = DeviceComboBox.SelectedItem?.ToString() ?? "Not specified";
                    bool printSuccess = ReceiptPrinter.PrintTicketNumber(ticketId, deviceForPrint, SubjectTextBox.Text.Trim());
                    if (printSuccess)
                    {
                        AddStatusMessage(StatusMessageKey.TicketReceiptPrinted);
                    }
                    else
                    {
                        AddStatusMessage(StatusMessageKey.FailedToPrintReceipt);
                    }
                }

                ResetForm();
                // Button will be re-enabled when user fills out the form again and navigates to page 2
            }
            catch (Exception ex)
            {
                // Re-enable button on error so user can retry
                SubmitButton.IsEnabled = true;

                // Check if it's an authentication error
                if (ex.Message.Contains("refresh access token") || ex.Message.Contains("Please sign in again"))
                {
                    await ShowMessageDialog(PopupMessageKey.SessionExpired);
                    ShowAuthOverlay();
                    AddStatusMessage(StatusMessageKey.SessionExpired);
                }
                else
                {
                    await ShowMessageDialog(PopupMessageKey.TicketSubmitError, $"Failed to submit ticket:\n{ex.Message}");
                    AddStatusMessage(StatusMessageKey.ErrorSubmittingTicket, ex.Message);
                }
            }
        }

        private bool ValidateForm()
        {
            // In test mode, skip user validation to allow testing page 2
            // Check if user exists in either PowerSchool OR NinjaOne
            if (_currentUser == null && _currentNinjaUser == null && !_testModeEnabled)
            {
                _ = ShowMessageDialog(PopupMessageKey.MissingUser);
                AddStatusMessage(StatusMessageKey.PleaseSearchForUser);
                return false;
            }

            // School affiliation validation - require selection if not auto-selected
            if (SchoolAffiliationComboBox.SelectedItem == null)
            {
                _ = ShowMessageDialog(PopupMessageKey.MissingSchool);
                AddStatusMessage(StatusMessageKey.SchoolAffiliationRequired);
                return false;
            }

            // Subject validation - minimum 4 characters
            if (string.IsNullOrWhiteSpace(SubjectTextBox.Text))
            {
                _ = ShowMessageDialog(PopupMessageKey.MissingSubject);
                AddStatusMessage(StatusMessageKey.SubjectRequired);
                return false;
            }

            if (SubjectTextBox.Text.Trim().Length < 4)
            {
                _ = ShowMessageDialog(PopupMessageKey.SubjectTooShort);
                AddStatusMessage(StatusMessageKey.SubjectTooShortValidation);
                return false;
            }

            if (SubjectTextBox.Text.Trim().Length > 50)
            {
                _ = ShowMessageDialog(PopupMessageKey.SubjectTooLong);
                AddStatusMessage(StatusMessageKey.SubjectTooLongValidation);
                return false;
            }

            // Description validation - minimum 4 characters
            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                _ = ShowMessageDialog(PopupMessageKey.MissingDescription);
                AddStatusMessage(StatusMessageKey.DescriptionRequired);
                return false;
            }

            if (DescriptionTextBox.Text.Trim().Length < 4)
            {
                _ = ShowMessageDialog(PopupMessageKey.DescriptionTooShort);
                AddStatusMessage(StatusMessageKey.DescriptionTooShortValidation);
                return false;
            }

            if (DescriptionTextBox.Text.Trim().Length > 50)
            {
                _ = ShowMessageDialog(PopupMessageKey.DescriptionTooLong);
                AddStatusMessage(StatusMessageKey.DescriptionTooLongValidation);
                return false;
            }

            // Device validation - accept either selected item OR custom text entry (if Write In was selected)
            if (DeviceComboBox.SelectedItem == null && string.IsNullOrWhiteSpace(DeviceComboBox.Text) && !_testModeEnabled)
            {
                _ = ShowMessageDialog(PopupMessageKey.MissingDevice);
                AddStatusMessage(StatusMessageKey.DeviceSelectionRequired);
                return false;
            }

            // Don't allow "Write In" as the actual device value (either selected or as placeholder text)
            if (DeviceComboBox.SelectedItem?.ToString() == "Write In" ||
                DeviceComboBox.Text == "Write In" ||
                (DeviceComboBox.IsEditable && string.IsNullOrWhiteSpace(DeviceComboBox.Text)))
            {
                _ = ShowMessageDialog(PopupMessageKey.MissingDeviceName);
                AddStatusMessage(StatusMessageKey.DeviceNameRequired);
                return false;
            }

            // If Write In was used, validate minimum 4 and maximum 20 characters
            if (DeviceComboBox.IsEditable && !string.IsNullOrWhiteSpace(DeviceComboBox.Text) && DeviceComboBox.Text != "Write In")
            {
                if (DeviceComboBox.Text.Trim().Length < 4)
                {
                    _ = ShowMessageDialog(PopupMessageKey.DeviceNameTooShort);
                    AddStatusMessage(StatusMessageKey.DeviceNameTooShortValidation);
                    return false;
                }

                if (DeviceComboBox.Text.Trim().Length > 20)
                {
                    _ = ShowMessageDialog(PopupMessageKey.DeviceNameTooLong);
                    AddStatusMessage(StatusMessageKey.DeviceNameTooLongValidation);
                    return false;
                }
            }

            return true;
        }

        private void ResetForm()
        {
            UsernameTextBox.Text = string.Empty;
            SubjectTextBox.Text = string.Empty;
            DescriptionTextBox.Text = string.Empty;
            ClearUserInfo();
            NavigateToPage(1);
            AddStatusMessage(StatusMessageKey.FormReset);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetForm();
        }

        /// <summary>
        /// Settings button click handler
        /// </summary>
        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowSettingsDialogAsync(isRequired: false);
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

                        // Check auth status after initialization
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

                // If settings dialog fails to show during required setup, we're stuck
                if (isRequired)
                {
                    await ShowMessageDialog(PopupMessageKey.CriticalError, $"Failed to display settings dialog: {ex.Message}\n\nThe application cannot continue without credentials.");
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                NavigateToPage(_currentPage - 1);
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < 2)
            {
                NavigateToPage(_currentPage + 1);
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(2);
        }

        /// <summary>
        /// Handle when school selection changes
        /// </summary>
        private void SchoolAffiliationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateNavigationButtons();
        }

        /// <summary>
        /// Handle when device selection changes
        /// </summary>
        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // If "Write In" is selected, make the ComboBox editable
            if (DeviceComboBox.SelectedItem != null &&
                DeviceComboBox.SelectedItem.ToString() == "Write In")
            {
                _isDeviceWriteInMode = true;
                DeviceComboBox.IsEditable = true;
                DeviceComboBox.SelectedIndex = -1; // Clear the selection immediately
                DeviceComboBox.Text = "Write In"; // Keep placeholder text

                // Focus the ComboBox so user can start typing
                DeviceComboBox.Focus();

                AddStatusMessage(StatusMessageKey.EnterCustomDeviceName);
            }
            else if (DeviceComboBox.SelectedItem != null && _isDeviceWriteInMode)
            {
                // User selected a different item from the dropdown while in write-in mode
                _isDeviceWriteInMode = false;
                DeviceComboBox.IsEditable = false;
            }
            else if (!_isDeviceWriteInMode && DeviceComboBox.IsEditable)
            {
                // Shouldn't be editable if not in write-in mode
                DeviceComboBox.IsEditable = false;
            }

            UpdateNavigationButtons();
        }

        /// <summary>
        /// Handle key presses in device ComboBox to clear placeholder text
        /// </summary>
        private void DeviceComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // If in write-in mode and the text is still the placeholder "Write In"
            if (_isDeviceWriteInMode && DeviceComboBox.IsEditable)
            {
                // Clear placeholder text on first keystroke (except for special keys)
                if (DeviceComboBox.Text == "Write In" &&
                    e.Key != Key.Tab &&
                    e.Key != Key.Escape &&
                    e.Key != Key.Enter &&
                    e.Key != Key.Left &&
                    e.Key != Key.Right &&
                    e.Key != Key.Up &&
                    e.Key != Key.Down)
                {
                    DeviceComboBox.Text = string.Empty;
                }
            }
        }

        private void NavigateToPage(int pageNumber)
        {
            _currentPage = pageNumber;

            if (_currentPage == 1)
            {
                Page1Content.Visibility = Visibility.Visible;
                Page2Content.Visibility = Visibility.Collapsed;
            }
            else if (_currentPage == 2)
            {
                Page1Content.Visibility = Visibility.Collapsed;
                Page2Content.Visibility = Visibility.Visible;

                // Re-enable submit button when navigating to page 2
                SubmitButton.IsEnabled = true;
            }

            UpdateNavigationButtons();
        }

        private void UpdateNavigationButtons()
        {
            BackButton.IsEnabled = _currentPage > 1;

            // Forward button only enabled if on page 1 and required fields are filled
            if (_currentPage == 1)
            {
                // Enable Continue button if:
                // 1. School is selected AND
                // 2. Device is selected or entered
                // OR test mode is enabled

                bool schoolSelected = SchoolAffiliationComboBox.SelectedItem != null;
                bool deviceSelected = DeviceComboBox.SelectedItem != null || !string.IsNullOrWhiteSpace(DeviceComboBox.Text);

                bool canContinue = (schoolSelected && deviceSelected) || _testModeEnabled;

                ForwardButton.IsEnabled = canContinue;
                ContinueButton.IsEnabled = canContinue;
            }
            else
            {
                ForwardButton.IsEnabled = false;
            }
        }

        private void AddStatusMessage(string message, StatusType type)
        {
            string icon = type switch
            {
                StatusType.Info => "ℹ️",
                StatusType.Success => "✅",
                StatusType.Warning => "⚠️",
                StatusType.Error => "❌",
                _ => "ℹ️"
            };

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formatted = $"[{timestamp}] {icon} {message}\n\n";

            StatusTextBlock.Text += formatted;

            // Scroll to bottom to show latest message
            StatusScrollViewer.ScrollToEnd();
        }

        /// <summary>
        /// Add status message using MessageService key
        /// </summary>
        private void AddStatusMessage(StatusMessageKey key, params object[] args)
        {
            var (message, type) = MessageService.GetStatusMessage(key);
            string formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            AddStatusMessage(formattedMessage, type);
        }

        private async Task ShowMessageDialog(string title, string content)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(content, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        /// <summary>
        /// Show message dialog using MessageService key
        /// </summary>
        private async Task ShowMessageDialog(PopupMessageKey key, params object[] args)
        {
            var (title, content) = MessageService.GetPopupMessage(key);
            string formattedContent = args.Length > 0 ? string.Format(content, args) : content;
            await ShowMessageDialog(title, formattedContent);
        }
    }

    // Data Models
    /// <summary>
    /// Represents user data retrieved from PowerSchool including devices and identifiers
    /// </summary>
    public class UserData
    {
        public string Username { get; set; }
        public List<string> SchoolIds { get; set; } // Support multiple school affiliations
        public string StudentNumber { get; set; }
        public string TeacherNumber { get; set; }
        public List<string> Devices { get; set; }
        public string UserType { get; set; } // "students", "teachers", or "users"
    }
}
