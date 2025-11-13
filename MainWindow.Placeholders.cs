using System.Windows;
using System.Windows.Controls;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Placeholder visibility management for MainWindow text inputs
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Handle Username TextBox focus and text changes for placeholder visibility
        /// </summary>
        private void UsernameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UsernamePlaceholder.Visibility = Visibility.Collapsed;
        }

        private void UsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                UsernamePlaceholder.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Handle Subject TextBox focus and text changes for placeholder visibility
        /// </summary>
        private void SubjectTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            SubjectPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void SubjectTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SubjectTextBox.Text))
            {
                SubjectPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void SubjectTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SubjectTextBox.IsFocused)
            {
                SubjectPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handle Description TextBox focus and text changes for placeholder visibility
        /// </summary>
        private void DescriptionTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            DescriptionPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void DescriptionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                DescriptionPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void DescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (DescriptionTextBox.IsFocused)
            {
                DescriptionPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handle Comment TextBox focus and text changes for placeholder visibility
        /// </summary>
        private void CommentTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            CommentPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void CommentTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CommentTextBox.Text))
            {
                CommentPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void CommentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CommentTextBox.IsFocused)
            {
                CommentPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handle school affiliation ComboBox dropdown events for placeholder visibility
        /// </summary>
        private void SchoolAffiliationComboBox_DropDownOpened(object sender, System.EventArgs e)
        {
            SchoolAffiliationPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void SchoolAffiliationComboBox_DropDownClosed(object sender, System.EventArgs e)
        {
            // Show placeholder only if nothing is selected
            if (SchoolAffiliationComboBox.SelectedItem == null)
            {
                SchoolAffiliationPlaceholder.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Handle device ComboBox dropdown events for placeholder visibility
        /// </summary>
        private void DeviceComboBox_DropDownOpened(object sender, System.EventArgs e)
        {
            DevicePlaceholder.Visibility = Visibility.Collapsed;
        }

        private void DeviceComboBox_DropDownClosed(object sender, System.EventArgs e)
        {
            // Only show placeholder if nothing is selected AND not in write-in mode
            if (DeviceComboBox.SelectedItem == null && !_isDeviceWriteInMode)
            {
                DevicePlaceholder.Visibility = Visibility.Visible;
            }
        }
    }
}
