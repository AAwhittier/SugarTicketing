namespace ITTicketingKiosk
{
    /// <summary>
    /// Form validation logic for MainWindow
    /// </summary>
    public partial class MainWindow
    {
        private bool ValidateCommentForm()
        {
            // Comment validation - minimum 4 characters, maximum 500
            if (string.IsNullOrWhiteSpace(CommentTextBox.Text))
            {
                _ = ShowMessageDialog(PopupMessageKey.MissingComment);
                AddStatusMessage(StatusMessageKey.CommentRequired);
                return false;
            }

            if (CommentTextBox.Text.Trim().Length < 4)
            {
                _ = ShowMessageDialog(PopupMessageKey.CommentTooShort);
                AddStatusMessage(StatusMessageKey.CommentTooShortValidation);
                return false;
            }

            if (CommentTextBox.Text.Trim().Length > 500)
            {
                _ = ShowMessageDialog(PopupMessageKey.CommentTooLong);
                AddStatusMessage(StatusMessageKey.CommentTooLongValidation);
                return false;
            }

            return true;
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

            // Device validation - require either a selected device OR custom text entry
            // Accepts: device from list, "Other", or custom write-in device name
            if (DeviceComboBox.SelectedItem == null && string.IsNullOrWhiteSpace(DeviceComboBox.Text) && !_testModeEnabled)
            {
                _ = ShowMessageDialog(PopupMessageKey.MissingDevice);
                AddStatusMessage(StatusMessageKey.DeviceSelectionRequired);
                return false;
            }

            // Validate write-in mode: don't allow placeholder text or empty input
            if (DeviceComboBox.Text == "Write In" ||
                (DeviceComboBox.IsEditable && string.IsNullOrWhiteSpace(DeviceComboBox.Text)))
            {
                _ = ShowMessageDialog(PopupMessageKey.MissingDeviceName);
                AddStatusMessage(StatusMessageKey.DeviceNameRequired);
                return false;
            }

            // Validate write-in device name length (4-20 characters)
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
    }
}
