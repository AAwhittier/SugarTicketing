# Phase 1 Completion Guide

## Status: 80% Complete

### ✅ Completed Files (4/6):
1. **MainWindow.API.cs** (~600 lines) - DONE
2. **MainWindow.Validation.cs** (~140 lines) - DONE  
3. **MainWindow.Navigation.cs** (~130 lines) - DONE
4. **MainWindow.Placeholders.cs** (~140 lines) - DONE

### ⏳ Remaining Work (2/6):

#### 5. MainWindow.Helpers.cs (~500 lines) - TO CREATE
**Methods to extract (lines 419-1820):**
- LoadBannerImage() - line 419
- SearchForOpenTicketsAsync() - line 595-724
- PopulateUserInfo() - line 883-979
- GetSchoolName() - line 981-996
- ShowAllSchoolOptions() - line 1001-1019
- ParseUsername() - line 1025-1041
- CapitalizeFirst() - line 1046-1061
- GetDeviceValue() - line 1067-1078 
- ClearUserInfo() - line 1080-1091
- ResetForm() - line 1403-1450
- AddStatusMessage(string, StatusType) - line 1741-1770
- AddStatusMessage(StatusMessageKey, params) - line 1772-1777
- ShowMessageDialog(string, string) - line 1779-1788
- ShowMessageDialog(PopupMessageKey, params) - line 1790-1797
- ShowTicketSuccessDialog() - line 1800-1813
- HandleUnexpectedError() - line 1815-1835
- FindVisualChild<T>() - line 1966-1984

#### 6. MainWindow.EventHandlers.cs (~500 lines) - TO CREATE
**Methods to extract (lines 449-1655):**
- UsernameTextBox_PreviewKeyDown() - line 449-457
- SearchButton_Click() - line 459-593
- SubmitButton_Click() - line 1095-1211
- SubmitCommentButton_Click() - line 1213-1272
- ResetButton_Click() - line 1452-1461
- MainWindow_KeyDown() - line 1463-1481
- UsernameTextBox_TextChanged() - line 1483-1558
- UsernameAutocompleteList_SelectionChanged() - line 1560-1571
- UsernameAutocompleteList_MouseUp() - line 1573-1584
- SchoolAffiliationComboBox_SelectionChanged() - line 1586-1608
- DeviceComboBox_SelectionChanged() - line 1614-1654
- DeviceComboBox_PreviewKeyDown() - line 1655-1679

### 7. MainWindow.xaml.cs Cleanup - TO DO
**After creating Helpers and EventHandlers, remove all duplicate methods from MainWindow.xaml.cs**

Keep only:
- Line 1-42: Using statements and namespace
- Line 43-417: Field declarations, constructor, Dispose, Window_Loaded
- Line 1987-1997: UserData class definition
- Line 1998: Closing braces

**Result: ~400 lines remaining**

## Next Steps:

1. Create MainWindow.Helpers.cs with all listed methods
2. Create MainWindow.EventHandlers.cs with all listed methods
3. Remove duplicate methods from MainWindow.xaml.cs (lines 419-1985)
4. Test compilation
5. Fix any errors
6. Commit Phase 1 complete

## Compilation Notes:
- All partial classes must be in same namespace
- All partial classes automatically share fields
- No changes needed to .csproj - C# compiler handles partial classes automatically

