using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Helper and utility methods for MainWindow
    /// </summary>
    public partial class MainWindow
    {
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

        private async Task SearchForOpenTicketsAsync()
        {
            try
            {
                // Only search if we have a NinjaOne user (need their name to match)
                if (_currentNinjaUser == null)
                {
                    System.Diagnostics.Debug.WriteLine("[OpenTickets] No NinjaOne user - skipping ticket search");
                    if (_testModeEnabled)
                    {
                        AddStatusMessage(StatusMessageKey.NoOpenTickets);
                    }
                    return;
                }

                // Only show in developer/test mode
                if (_testModeEnabled)
                {
                    AddStatusMessage(StatusMessageKey.SearchingOpenTickets);
                }
                System.Diagnostics.Debug.WriteLine($"[OpenTickets] Starting search for user: {_currentNinjaUser.FullName}");

                // Search for open tickets using board ID 1010
                var openTickets = await _ninjaApi.SearchOpenTicketsAsync(1010);

                if (openTickets == null || openTickets.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[OpenTickets] No open tickets returned from API");
                    if (_testModeEnabled)
                    {
                        AddStatusMessage(StatusMessageKey.NoOpenTickets);
                    }
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[OpenTickets] Found {openTickets.Count} open tickets total");

                // Match tickets by requester name
                string currentUserName = _currentNinjaUser.FullName.Trim().ToLower();
                System.Diagnostics.Debug.WriteLine($"[OpenTickets] Looking for matches with: '{currentUserName}'");

                int ticketIndex = 0;
                foreach (var ticket in openTickets)
                {
                    ticketIndex++;
                    System.Diagnostics.Debug.WriteLine($"[OpenTickets] --- Checking ticket #{ticketIndex} ---");

                    // Extract ticket ID for logging
                    string ticketIdStr = ticket.ContainsKey("id") ? ticket["id"]?.ToString() ?? "N/A" : "N/A";
                    System.Diagnostics.Debug.WriteLine($"[OpenTickets] Ticket ID: {ticketIdStr}");

                    // Extract requester name from the ticket
                    if (ticket.ContainsKey("requester") && ticket["requester"] != null)
                    {
                        var requesterValue = ticket["requester"];
                        System.Diagnostics.Debug.WriteLine($"[OpenTickets] Requester raw value: '{requesterValue}'");

                        string requesterName = requesterValue.ToString().Trim().ToLower();
                        System.Diagnostics.Debug.WriteLine($"[OpenTickets] Requester normalized: '{requesterName}'");

                        // Check if the requester name matches the current user
                        bool isMatch = requesterName == currentUserName;
                        System.Diagnostics.Debug.WriteLine($"[OpenTickets] Match result: {isMatch}");

                        if (isMatch)
                        {
                            // Prevent showing dialog twice using semaphore
                            if (!await _dialogSemaphore.WaitAsync(0))
                            {
                                System.Diagnostics.Debug.WriteLine($"[OpenTickets] Dialog already showing, skipping duplicate");
                                return;
                            }

                            try
                            {
                                // Extract ticket ID and subject
                                int ticketId = Convert.ToInt32(ticket["id"]);
                                string ticketSubject = ticket.ContainsKey("summary") ? ticket["summary"]?.ToString() ?? "No Subject" : "No Subject";

                                System.Diagnostics.Debug.WriteLine($"[OpenTickets] ✓ MATCH FOUND! Ticket #{ticketId}: {ticketSubject}");
                                AddStatusMessage(StatusMessageKey.FoundOpenTicket, ticketId, ticketSubject);

                                // Show dialog to ask user what they want to do
                                var dialog = new TicketChoiceDialog(ticketId, ticketSubject);
                                bool? result = dialog.ShowDialog();

                                if (result == true)
                                {
                                    if (dialog.Choice == TicketChoice.Continue)
                                    {
                                        // Store existing ticket info and navigate to Page 3
                                        _existingTicketId = ticketId;
                                        _existingTicketSubject = ticketSubject;
                                        NavigateToPage3();
                                    }
                                    // else: user chose to file new ticket, continue normal flow
                                }
                            }
                            finally
                            {
                                _dialogSemaphore.Release();
                            }

                            // Only process the first matching ticket
                            return;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[OpenTickets] Ticket has no 'requester' field or it is null");
                    }

                    System.Diagnostics.Debug.WriteLine($"[OpenTickets] --- End ticket #{ticketIndex} ---\n");
                }

                // No matching tickets found
                System.Diagnostics.Debug.WriteLine($"[OpenTickets] No matching tickets found after checking {ticketIndex} tickets");
                if (_testModeEnabled)
                {
                    AddStatusMessage(StatusMessageKey.NoOpenTickets);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't block the user from continuing
                AddStatusMessage(StatusMessageKey.ErrorSearchingTickets, ex.Message);
                System.Diagnostics.Debug.WriteLine($"[OpenTickets] Error searching: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[OpenTickets] Stack trace: {ex.StackTrace}");
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
            DeviceComboBox.SelectedIndex = -1; // No default selection
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
                "999999" => "Graduated",
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
                "DIS",     // 10000
                "Graduated" // 999999
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

        /// <summary>
        /// Capitalizes the first letter of a string
        /// </summary>
        private string CapitalizeFirst(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Handle strings that are only whitespace
            if (string.IsNullOrWhiteSpace(input))
                return input;

            string trimmed = input.Trim();
            if (trimmed.Length == 0)
                return input;

            // Capitalize first character and concatenate the rest
            return char.ToUpper(trimmed[0]) + trimmed.Substring(1);
        }

        /// <summary>
        /// Gets the current device value from the DeviceComboBox
        /// Handles both selected items and custom text entry (write-in mode)
        /// </summary>
        private string GetDeviceValue()
        {
            if (DeviceComboBox.SelectedItem != null)
            {
                return DeviceComboBox.SelectedItem.ToString();
            }
            else if (!string.IsNullOrWhiteSpace(DeviceComboBox.Text))
            {
                return DeviceComboBox.Text.Trim();
            }
            return "Not specified";
        }

        private void ClearUserInfo()
        {
            NameLabel.Text = string.Empty;
            EmailLabel.Text = string.Empty;
            SchoolAffiliationComboBox.ItemsSource = null;
            SchoolAffiliationComboBox.SelectedIndex = -1;
            DeviceComboBox.ItemsSource = null;
            DeviceComboBox.SelectedIndex = -1;

            // Reset Device ComboBox write-in mode
            DeviceComboBox.Text = string.Empty;
            DeviceComboBox.IsEditable = false;
            _isDeviceWriteInMode = false;
        }

        private void ResetForm()
        {
            UsernameTextBox.Text = string.Empty;
            SubjectTextBox.Text = string.Empty;
            DescriptionTextBox.Text = string.Empty;
            CommentTextBox.Text = string.Empty;
            ClearUserInfo();

            // Clear existing ticket information
            _existingTicketId = null;
            _existingTicketSubject = null;
            _isContinueTicketMode = false;

            NavigateToPage(1);

            // Show all placeholders
            UsernamePlaceholder.Visibility = Visibility.Visible;
            SubjectPlaceholder.Visibility = Visibility.Visible;
            DescriptionPlaceholder.Visibility = Visibility.Visible;
            CommentPlaceholder.Visibility = Visibility.Visible;
            SchoolAffiliationPlaceholder.Visibility = Visibility.Visible;
            DevicePlaceholder.Visibility = Visibility.Visible;

            AddStatusMessage(StatusMessageKey.FormReset);
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

            // Cap to last 30 messages to prevent memory/performance issues
            var messages = StatusTextBlock.Text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (messages.Length > 30)
            {
                // Keep only the last 30 messages
                StatusTextBlock.Text = string.Join("\n\n", messages.Skip(messages.Length - 30)) + "\n\n";
            }

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

        /// <summary>
        /// Show custom ticket success dialog
        /// </summary>
        private async Task ShowTicketSuccessDialog(string ticketId, bool receiptPrinted)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var dialog = new TicketSuccessDialog(ticketId, receiptPrinted)
                {
                    Owner = this
                };
                dialog.ShowDialog();
            });
        }

        /// <summary>
        /// Handle unexpected errors in async void methods to prevent application crash
        /// </summary>
        private void HandleUnexpectedError(Exception ex, string context = "")
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Unexpected error in {context}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");

            string errorMessage = string.IsNullOrEmpty(context)
                ? $"An unexpected error occurred:\n\n{ex.Message}"
                : $"An unexpected error occurred during {context}:\n\n{ex.Message}";

            AddStatusMessage(errorMessage, StatusType.Error);

            try
            {
                MessageBox.Show(
                    errorMessage,
                    "Unexpected Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // If even showing the error dialog fails, just log it
                System.Diagnostics.Debug.WriteLine("[ERROR] Failed to show error dialog");
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }

            return null;
        }
    }
}
