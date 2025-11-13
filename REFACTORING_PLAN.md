# MainWindow Refactoring Plan

## Phase 1: Partial Classes (IN PROGRESS)

### Created:
- ✅ MainWindow.API.cs (~600 lines)
  - InitializeApplicationAsync
  - InitializeAPIs
  - CheckAuthenticationStatusAsync
  - SignInButton_Click
  - PerformOAuthFlowAsync
  - ShowAuthOverlay/HideAuthOverlay
  - UpdateUsernameCacheAsync
  - LookupKioskUserAsync
  - ShowSettingsDialogAsync
  - SettingsButton_Click

- ✅ MainWindow.Validation.cs (~140 lines)
  - ValidateForm
  - ValidateCommentForm

### To Create:
- ⏳ MainWindow.Navigation.cs
  - NavigateToPage
  - NavigateToPage3
  - UpdateNavigationButtons
  - BackButton_Click
  - ForwardButton_Click
  - ContinueButton_Click

- ⏳ MainWindow.EventHandlers.cs
  - SearchButton_Click
  - SubmitButton_Click
  - SubmitCommentButton_Click
  - ResetButton_Click
  - DeviceComboBox_SelectionChanged
  - DeviceComboBox_PreviewKeyDown
  - SchoolAffiliationComboBox_SelectionChanged
  - UsernameTextBox_PreviewKeyDown
  - UsernameTextBox_TextChanged
  - UsernameAutocompleteList_SelectionChanged
  - UsernameAutocompleteList_MouseUp
  - MainWindow_KeyDown

- ⏳ MainWindow.Placeholders.cs
  - All GotFocus/LostFocus handlers
  - All TextChanged handlers for placeholders
  - All DropDownOpened/DropDownClosed handlers

- ⏳ MainWindow.Helpers.cs
  - PopulateUserInfo
  - ClearUserInfo
  - GetSchoolName
  - ShowAllSchoolOptions
  - ParseUsername
  - CapitalizeFirst
  - GetDeviceValue
  - ResetForm
  - LoadBannerImage
  - SearchForOpenTicketsAsync
  - ShowMessageDialog methods
  - ShowTicketSuccessDialog
  - AddStatusMessage methods
  - HandleUnexpectedError
  - FindVisualChild

### MainWindow.xaml.cs (Core - will remain ~400 lines)
- Field declarations
- Constructor
- Dispose pattern
- UserData class

## Phase 2: UserControls (PENDING)
- UserSearchPage.xaml/.cs
- TicketDetailsPage.xaml/.cs
- AddCommentPage.xaml/.cs

## Estimated Line Reduction:
- Before: 2025 lines (MainWindow.xaml.cs)
- After Phase 1: ~400 lines (MainWindow.xaml.cs) + 6 partial classes
- After Phase 2: ~300 lines (MainWindow.xaml.cs) + 6 partial classes + 3 UserControls
