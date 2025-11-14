using System.Threading.Tasks;
using System.Windows;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Page navigation logic for MainWindow
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Navigate to Page 3 (Add Comment to Existing Ticket)
        /// </summary>
        private void NavigateToPage3()
        {
            _currentPage = 3;
            _isContinueTicketMode = true; // Track that we're in continue ticket mode

            // Hide other pages
            Page1Content.Visibility = Visibility.Collapsed;
            Page2Content.Visibility = Visibility.Collapsed;
            Page3Content.Visibility = Visibility.Visible;

            // Populate existing ticket information
            if (_existingTicketId.HasValue && !string.IsNullOrEmpty(_existingTicketSubject))
            {
                ExistingTicketLabel.Text = $"#{_existingTicketId}";
                ExistingTicketSubjectLabel.Text = _existingTicketSubject;
            }

            // Clear comment field
            CommentTextBox.Text = string.Empty;
            CommentPlaceholder.Visibility = Visibility.Visible;

            // Re-enable submit button (in case we're navigating here again)
            SubmitCommentButton.IsEnabled = true;

            UpdateNavigationButtons();
        }

        private void NavigateToPage(int pageNumber)
        {
            _currentPage = pageNumber;

            if (_currentPage == 1)
            {
                Page1Content.Visibility = Visibility.Visible;
                Page2Content.Visibility = Visibility.Collapsed;
                Page3Content.Visibility = Visibility.Collapsed;
            }
            else if (_currentPage == 2)
            {
                Page1Content.Visibility = Visibility.Collapsed;
                Page2Content.Visibility = Visibility.Visible;
                Page3Content.Visibility = Visibility.Collapsed;

                // Re-enable submit button when navigating to page 2
                SubmitButton.IsEnabled = true;
            }
            else if (_currentPage == 3)
            {
                Page1Content.Visibility = Visibility.Collapsed;
                Page2Content.Visibility = Visibility.Collapsed;
                Page3Content.Visibility = Visibility.Visible;

                // Re-enable submit comment button when navigating to page 3
                SubmitCommentButton.IsEnabled = true;
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
                bool deviceSelected = DeviceComboBox.SelectedItem != null ||
                                     _isDeviceWriteInMode ||
                                     !string.IsNullOrWhiteSpace(DeviceComboBox.Text);

                bool canContinue = (schoolSelected && deviceSelected) || _testModeEnabled;

                ForwardButton.IsEnabled = canContinue;
                ContinueButton.IsEnabled = canContinue;
            }
            else
            {
                ForwardButton.IsEnabled = false;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                // If on Page 3 (continue ticket mode), go directly back to Page 1
                if (_currentPage == 3 && _isContinueTicketMode)
                {
                    _isContinueTicketMode = false; // Exit continue ticket mode
                    NavigateToPage(1);
                }
                else
                {
                    NavigateToPage(_currentPage - 1);
                }
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < 2)
            {
                NavigateToPage(_currentPage + 1);
            }
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate School and Device fields before allowing user to continue
            // School affiliation validation
            if (SchoolAffiliationComboBox.SelectedItem == null)
            {
                await ShowMessageDialog(PopupMessageKey.MissingSchool);
                AddStatusMessage(StatusMessageKey.SchoolAffiliationRequired);
                return;
            }

            // Device validation - require either a selected device OR custom text entry
            if (DeviceComboBox.SelectedItem == null && string.IsNullOrWhiteSpace(DeviceComboBox.Text) && !_testModeEnabled)
            {
                await ShowMessageDialog(PopupMessageKey.MissingDevice);
                AddStatusMessage(StatusMessageKey.DeviceSelectionRequired);
                return;
            }

            // Validate write-in mode: don't allow placeholder text or empty input
            if (DeviceComboBox.Text == "Write In" ||
                (DeviceComboBox.IsEditable && string.IsNullOrWhiteSpace(DeviceComboBox.Text)))
            {
                await ShowMessageDialog(PopupMessageKey.MissingDeviceName);
                AddStatusMessage(StatusMessageKey.DeviceNameRequired);
                return;
            }

            // Validate write-in device name length (4-20 characters)
            if (DeviceComboBox.IsEditable && !string.IsNullOrWhiteSpace(DeviceComboBox.Text) && DeviceComboBox.Text != "Write In")
            {
                if (DeviceComboBox.Text.Trim().Length < 4)
                {
                    await ShowMessageDialog(PopupMessageKey.DeviceNameTooShort);
                    AddStatusMessage(StatusMessageKey.DeviceNameTooShortValidation);
                    return;
                }

                if (DeviceComboBox.Text.Trim().Length > 20)
                {
                    await ShowMessageDialog(PopupMessageKey.DeviceNameTooLong);
                    AddStatusMessage(StatusMessageKey.DeviceNameTooLongValidation);
                    return;
                }
            }

            NavigateToPage(2);
        }
    }
}
