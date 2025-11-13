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

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(2);
        }
    }
}
