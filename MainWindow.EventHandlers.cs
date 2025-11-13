using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Event handlers for MainWindow UI interactions
    /// </summary>
    public partial class MainWindow
    {
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
            try
            {
                // Prevent duplicate searches
                if (_isSearching)
                {
                    System.Diagnostics.Debug.WriteLine("[Search] Search already in progress, ignoring duplicate request");
                    return;
                }

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

                _isSearching = true;
                SearchButton.IsEnabled = false;
                AddStatusMessage(StatusMessageKey.SearchingForUser, username);

                try
                {
                    // Query PowerSchool for device information
                    if (_testModeEnabled)
                    {
                        AddStatusMessage(StatusMessageKey.QueryingPowerSchool);
                    }
                    _currentUser = await _psApi.LookupDevicesAsync(username);

                    // Query NinjaOne for user name and email
                    if (_testModeEnabled)
                    {
                        AddStatusMessage(StatusMessageKey.QueryingNinjaOne);
                    }
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

                        // Search for open tickets for this user (skip for 'kiosk' fallback account)
                        bool isKioskUser = _currentNinjaUser != null &&
                                          (_currentNinjaUser.Email.StartsWith("kiosk@", StringComparison.OrdinalIgnoreCase) ||
                                           _currentNinjaUser.Email.Equals("kiosk", StringComparison.OrdinalIgnoreCase));

                        if (!isKioskUser)
                        {
                            await SearchForOpenTicketsAsync();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[OpenTickets] Skipping ticket search for kiosk user - fallback account cannot continue tickets");
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
                    _isSearching = false;
                    SearchButton.IsEnabled = true;

                    // Update username cache after search (whether successful or not)
                    _ = UpdateUsernameCacheAsync();
                }
            }
            catch (Exception ex)
            {
                HandleUnexpectedError(ex, "user search");
            }
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            try
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
                    string deviceValue = GetDeviceValue();

                    // Get selected school affiliation
                    string schoolAffiliation = string.Empty;
                    if (SchoolAffiliationComboBox.SelectedItem != null)
                    {
                        schoolAffiliation = SchoolAffiliationComboBox.SelectedItem.ToString() ?? string.Empty;
                    }

                    // Description is just the user's text - capitalize first letter
                    string description = CapitalizeFirst(DescriptionTextBox.Text.Trim());

                    // Get subject and capitalize first letter
                    string subject = CapitalizeFirst(SubjectTextBox.Text.Trim());

                    // Create ticket using proper API structure with custom fields
                    var ticket = await _ninjaApi.CreateTicketAsync(
                        subject: subject,
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
                    AddStatusMessage(StatusMessageKey.TicketCreatedSuccessfully, ticketId, requesterName);

                    // Print ticket receipt if enabled (BEFORE showing the dialog)
                    bool receiptPrinted = false;
                    if (ReceiptPrinter.IsEnabled())
                    {
                        string deviceForPrint = GetDeviceValue();
                        receiptPrinted = ReceiptPrinter.PrintTicketNumber(ticketId, deviceForPrint, subject, requesterName);
                        if (receiptPrinted)
                        {
                            AddStatusMessage(StatusMessageKey.TicketReceiptPrinted);
                        }
                        else
                        {
                            AddStatusMessage(StatusMessageKey.FailedToPrintReceipt);
                        }
                    }

                    // Show custom success dialog (AFTER printing)
                    await ShowTicketSuccessDialog(ticketId, receiptPrinted);

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
            catch (Exception ex)
            {
                HandleUnexpectedError(ex, "ticket submission");
            }
        }

        private async void SubmitCommentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateCommentForm())
                    return;

                // Disable button immediately to prevent double-submission
                SubmitCommentButton.IsEnabled = false;
                AddStatusMessage(StatusMessageKey.AddingCommentToTicket, _existingTicketId);

                try
                {
                    if (!_existingTicketId.HasValue)
                    {
                        throw new Exception("No existing ticket ID available");
                    }

                    // Capitalize first letter of comment
                    string commentBody = CapitalizeFirst(CommentTextBox.Text.Trim());

                    // Add comment to the existing ticket
                    var result = await _ninjaApi.AddTicketCommentAsync(_existingTicketId.Value, commentBody);

                    await ShowMessageDialog(PopupMessageKey.CommentSuccess, _existingTicketId.Value);
                    AddStatusMessage(StatusMessageKey.CommentAddedSuccessfully, _existingTicketId.Value);

                    ResetForm();
                    // Button will be re-enabled when user fills out the form again
                }
                catch (Exception ex)
                {
                    // Re-enable button on error so user can retry
                    SubmitCommentButton.IsEnabled = true;

                    // Check if it's an authentication error
                    if (ex.Message.Contains("refresh access token") || ex.Message.Contains("Please sign in again"))
                    {
                        await ShowMessageDialog(PopupMessageKey.SessionExpired);
                        ShowAuthOverlay();
                        AddStatusMessage(StatusMessageKey.SessionExpired);
                    }
                    else
                    {
                        await ShowMessageDialog(PopupMessageKey.CommentSubmitError, $"Failed to add comment:\n{ex.Message}");
                        AddStatusMessage(StatusMessageKey.ErrorAddingComment, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                HandleUnexpectedError(ex, "comment submission");
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetForm();
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = UsernameTextBox.Text;

            // Handle placeholder visibility
            if (UsernameTextBox.IsFocused)
            {
                UsernamePlaceholder.Visibility = Visibility.Collapsed;
            }

            // Only show autocomplete if 2+ characters and we have cached usernames
            if (string.IsNullOrEmpty(input) || input.Length < 2 || _cachedUsernames.Count == 0)
            {
                UsernameAutocompletePopup.IsOpen = false;
                return;
            }

            // Filter usernames that start with the input (case-insensitive)
            var filteredUsernames = _cachedUsernames
                .Where(u => u.StartsWith(input, StringComparison.OrdinalIgnoreCase))
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

                // Automatically trigger search for the selected user
                SearchButton_Click(SearchButton, new RoutedEventArgs());
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

                // Automatically trigger search for the selected user
                SearchButton_Click(SearchButton, new RoutedEventArgs());
            }
        }

        private void SchoolAffiliationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Hide placeholder when an item is selected
            if (SchoolAffiliationComboBox.SelectedItem != null)
            {
                SchoolAffiliationPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                SchoolAffiliationPlaceholder.Visibility = Visibility.Visible;
            }

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

                // Focus the ComboBox for typing
                DeviceComboBox.Focus();

                AddStatusMessage(StatusMessageKey.EnterCustomDeviceName);
                DevicePlaceholder.Visibility = Visibility.Collapsed;
            }
            else if (DeviceComboBox.SelectedItem != null && _isDeviceWriteInMode)
            {
                // User selected a different item from the dropdown while in write-in mode
                _isDeviceWriteInMode = false;
                DeviceComboBox.IsEditable = false;
                DevicePlaceholder.Visibility = Visibility.Collapsed;
            }
            else if (!_isDeviceWriteInMode && DeviceComboBox.IsEditable)
            {
                // Shouldn't be editable if not in write-in mode
                DeviceComboBox.IsEditable = false;
            }

            // Update placeholder visibility based on selection
            if (DeviceComboBox.SelectedItem != null)
            {
                DevicePlaceholder.Visibility = Visibility.Collapsed;
            }
            else if (!_isDeviceWriteInMode && string.IsNullOrWhiteSpace(DeviceComboBox.Text))
            {
                DevicePlaceholder.Visibility = Visibility.Visible;
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
                // Hide the visual placeholder when user starts typing
                DevicePlaceholder.Visibility = Visibility.Collapsed;

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
    }
}
