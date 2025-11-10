using System;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Message keys for popup dialogs
    /// </summary>
    public enum PopupMessageKey
    {
        InputRequired,
        UsernameTooShort,
        UsernameTooLong,
        UserNotFound,
        SearchError,
        TicketSuccess,
        SessionExpired,
        MissingUser,
        MissingSchool,
        MissingSubject,
        SubjectTooShort,
        SubjectTooLong,
        MissingDescription,
        DescriptionTooShort,
        DescriptionTooLong,
        MissingDevice,
        MissingDeviceName,
        DeviceNameTooShort,
        DeviceNameTooLong,
        TicketSubmitError,
        SetupRequired,
        InitializationError,
        CriticalError,
        AuthenticationError
    }

    /// <summary>
    /// Message keys for status panel messages
    /// </summary>
    public enum StatusMessageKey
    {
        CredentialsNotConfigured,
        ApplicationCannotStart,
        InitializationError,
        TestModeDisabled,
        TestModeEnabled,
        SettingsLocked,
        SettingsUnlocked,
        InitializingPowerSchool,
        PowerSchoolInitialized,
        InitializingNinjaOne,
        NinjaOneInitialized,
        AllAPIsInitialized,
        FailedToInitializeAPIs,
        AuthenticatedReady,
        RefreshTokenInvalid,
        AuthenticationRequired,
        ErrorCheckingAuthentication,
        OpeningSettings,
        StartingOAuthFlow,
        AuthorizationCodeReceived,
        AuthenticatedSuccessfully,
        ReadyToCreateTickets,
        AuthenticationTimedOut,
        AuthenticationError,
        PleaseEnterUsername,
        UsernameTooShort,
        UsernameTooLong,
        SearchingForUser,
        QueryingPowerSchool,
        QueryingNinjaOne,
        FoundNinjaOneUser,
        NinjaOneUserNotFound,
        UserFoundInBoth,
        UserFoundPowerSchoolOnly,
        UserFoundNinjaOneOnly,
        UserNotFoundInEither,
        ErrorDuringSearch,
        CachedUsernames,
        UserAffiliatedWithSchools,
        FoundDevices,
        NoDevicesFound,
        SchoolAffiliationNotFound,
        ParsedUsernameFromEmail,
        SubmittingTicket,
        TicketCreatedSuccessfully,
        TicketReceiptPrinted,
        FailedToPrintReceipt,
        SessionExpired,
        ErrorSubmittingTicket,
        PleaseSearchForUser,
        SchoolAffiliationRequired,
        SubjectRequired,
        SubjectTooShortValidation,
        SubjectTooLongValidation,
        DescriptionRequired,
        DescriptionTooShortValidation,
        DescriptionTooLongValidation,
        DeviceSelectionRequired,
        DeviceNameRequired,
        DeviceNameTooShortValidation,
        DeviceNameTooLongValidation,
        FormReset,
        CredentialsSaved,
        ReinitializingAPIs,
        InitializationFailed,
        ErrorShowingSettings,
        EnterCustomDeviceName
    }

    /// <summary>
    /// Type of status message
    /// </summary>
    public enum StatusType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Centralized message service for all user-facing messages
    /// </summary>
    public static class MessageService
    {
        /// <summary>
        /// Get a popup message by key
        /// </summary>
        public static (string Title, string Content) GetPopupMessage(PopupMessageKey key)
        {
            return key switch
            {
                PopupMessageKey.InputRequired => ("Input Required", "Please enter a username"),
                PopupMessageKey.UsernameTooShort => ("Username Too Short", "Username must be at least 4 characters long"),
                PopupMessageKey.UsernameTooLong => ("Username Too Long", "Username must be 50 characters or less"),
                PopupMessageKey.UserNotFound => ("Not Found", "User not found in PowerSchool or NinjaOne. Please check the username."),
                PopupMessageKey.SearchError => ("Error", "{0}"), // Dynamic content
                PopupMessageKey.TicketSuccess => ("Success", "Ticket created successfully!\nTicket ID: {0}"),
                PopupMessageKey.SessionExpired => ("Session Expired", "Your session has expired. Please sign in again."),
                PopupMessageKey.MissingUser => ("Missing Information", "Please search for a user first"),
                PopupMessageKey.MissingSchool => ("Missing Information", "Please select your school affiliation"),
                PopupMessageKey.MissingSubject => ("Missing Information", "Please enter a subject"),
                PopupMessageKey.SubjectTooShort => ("Subject Too Short", "Subject must be at least 4 characters long"),
                PopupMessageKey.SubjectTooLong => ("Subject Too Long", "Subject must be 50 characters or less"),
                PopupMessageKey.MissingDescription => ("Missing Information", "Please enter a description"),
                PopupMessageKey.DescriptionTooShort => ("Description Too Short", "Description must be at least 4 characters long"),
                PopupMessageKey.DescriptionTooLong => ("Description Too Long", "Description must be 50 characters or less"),
                PopupMessageKey.MissingDevice => ("Missing Information", "Please select or enter a device"),
                PopupMessageKey.MissingDeviceName => ("Missing Information", "Please enter a device name"),
                PopupMessageKey.DeviceNameTooShort => ("Device Name Too Short", "Device name must be at least 4 characters long"),
                PopupMessageKey.DeviceNameTooLong => ("Device Name Too Long", "Device name must be 20 characters or less"),
                PopupMessageKey.TicketSubmitError => ("Error", "{0}"),
                PopupMessageKey.SetupRequired => ("Setup Required", "Credentials are required to use this application. Please configure your API credentials."),
                PopupMessageKey.InitializationError => ("Initialization Error", "{0}"),
                PopupMessageKey.CriticalError => ("Critical Error", "{0}"),
                PopupMessageKey.AuthenticationError => ("Authentication Error", "{0}"),
                _ => ("Unknown", "An unknown error occurred")
            };
        }

        /// <summary>
        /// Get a status message by key
        /// </summary>
        public static (string Message, StatusType Type) GetStatusMessage(StatusMessageKey key)
        {
            return key switch
            {
                StatusMessageKey.CredentialsNotConfigured => ("Credentials not configured - please enter API credentials", StatusType.Warning),
                StatusMessageKey.ApplicationCannotStart => ("Application cannot start without credentials", StatusType.Error),
                StatusMessageKey.InitializationError => ("Initialization error: {0}", StatusType.Error),
                StatusMessageKey.TestModeDisabled => ("Test mode disabled - Settings locked", StatusType.Info),
                StatusMessageKey.TestModeEnabled => ("Test mode enabled - Press F12 to unlock settings", StatusType.Info),
                StatusMessageKey.SettingsLocked => ("Settings menu locked", StatusType.Info),
                StatusMessageKey.SettingsUnlocked => ("Settings menu unlocked", StatusType.Info),
                StatusMessageKey.InitializingPowerSchool => ("Initializing PowerSchool API...", StatusType.Info),
                StatusMessageKey.PowerSchoolInitialized => ("PowerSchool API initialized", StatusType.Success),
                StatusMessageKey.InitializingNinjaOne => ("Initializing NinjaOne API...", StatusType.Info),
                StatusMessageKey.NinjaOneInitialized => ("NinjaOne API initialized", StatusType.Success),
                StatusMessageKey.AllAPIsInitialized => ("All APIs initialized successfully", StatusType.Success),
                StatusMessageKey.FailedToInitializeAPIs => ("Failed to initialize APIs: {0}", StatusType.Error),
                StatusMessageKey.AuthenticatedReady => ("Authenticated - Ready to create tickets", StatusType.Success),
                StatusMessageKey.RefreshTokenInvalid => ("Refresh token invalid: {0}", StatusType.Warning),
                StatusMessageKey.AuthenticationRequired => ("Authentication required to access ticketing system", StatusType.Warning),
                StatusMessageKey.ErrorCheckingAuthentication => ("Error checking authentication: {0}", StatusType.Error),
                StatusMessageKey.OpeningSettings => ("Opening settings to configure credentials", StatusType.Info),
                StatusMessageKey.StartingOAuthFlow => ("Starting OAuth authentication flow...", StatusType.Info),
                StatusMessageKey.AuthorizationCodeReceived => ("Authorization code received, exchanging for tokens...", StatusType.Info),
                StatusMessageKey.AuthenticatedSuccessfully => ("Successfully authenticated with NinjaOne", StatusType.Success),
                StatusMessageKey.ReadyToCreateTickets => ("Ready to create tickets", StatusType.Info),
                StatusMessageKey.AuthenticationTimedOut => ("Authentication timed out - user did not complete sign-in", StatusType.Error),
                StatusMessageKey.AuthenticationError => ("Authentication error: {0}", StatusType.Error),
                StatusMessageKey.PleaseEnterUsername => ("Please enter a username", StatusType.Warning),
                StatusMessageKey.UsernameTooShort => ("Username must be at least 4 characters", StatusType.Warning),
                StatusMessageKey.UsernameTooLong => ("Username must be 50 characters or less", StatusType.Warning),
                StatusMessageKey.SearchingForUser => ("Searching for user: {0}...", StatusType.Info),
                StatusMessageKey.QueryingPowerSchool => ("Querying PowerSchool for devices...", StatusType.Info),
                StatusMessageKey.QueryingNinjaOne => ("Querying NinjaOne for user details...", StatusType.Info),
                StatusMessageKey.FoundNinjaOneUser => ("Found NinjaOne user: {0} ({1})", StatusType.Success),
                StatusMessageKey.NinjaOneUserNotFound => ("NinjaOne user not found for username: '{0}'", StatusType.Warning),
                StatusMessageKey.UserFoundInBoth => ("User found in both PowerSchool and NinjaOne", StatusType.Success),
                StatusMessageKey.UserFoundPowerSchoolOnly => ("User found in PowerSchool only (devices available)", StatusType.Warning),
                StatusMessageKey.UserFoundNinjaOneOnly => ("User found in NinjaOne only (no devices from PowerSchool)", StatusType.Warning),
                StatusMessageKey.UserNotFoundInEither => ("User '{0}' not found in either system", StatusType.Error),
                StatusMessageKey.ErrorDuringSearch => ("Error during search: {0}", StatusType.Error),
                StatusMessageKey.CachedUsernames => ("Cached {0} usernames for autocomplete", StatusType.Info),
                StatusMessageKey.UserAffiliatedWithSchools => ("User affiliated with {0} schools: {1}", StatusType.Info),
                StatusMessageKey.FoundDevices => ("Found {0} device(s) for {1} '{2}'", StatusType.Success),
                StatusMessageKey.NoDevicesFound => ("No devices found in PowerSchool", StatusType.Warning),
                StatusMessageKey.SchoolAffiliationNotFound => ("School affiliation not found - please select your school", StatusType.Warning),
                StatusMessageKey.ParsedUsernameFromEmail => ("Parsed username '{0}' from email format", StatusType.Info),
                StatusMessageKey.SubmittingTicket => ("Submitting ticket...", StatusType.Info),
                StatusMessageKey.TicketCreatedSuccessfully => ("Ticket #{0} created successfully for {1}", StatusType.Success),
                StatusMessageKey.TicketReceiptPrinted => ("Ticket receipt printed", StatusType.Success),
                StatusMessageKey.FailedToPrintReceipt => ("Failed to print ticket receipt (check printer status)", StatusType.Warning),
                StatusMessageKey.SessionExpired => ("Session expired - authentication required", StatusType.Warning),
                StatusMessageKey.ErrorSubmittingTicket => ("Error submitting ticket: {0}", StatusType.Error),
                StatusMessageKey.PleaseSearchForUser => ("Please search for a user before submitting", StatusType.Warning),
                StatusMessageKey.SchoolAffiliationRequired => ("School affiliation is required", StatusType.Warning),
                StatusMessageKey.SubjectRequired => ("Subject is required", StatusType.Warning),
                StatusMessageKey.SubjectTooShortValidation => ("Subject must be at least 4 characters", StatusType.Warning),
                StatusMessageKey.SubjectTooLongValidation => ("Subject must be 50 characters or less", StatusType.Warning),
                StatusMessageKey.DescriptionRequired => ("Description is required", StatusType.Warning),
                StatusMessageKey.DescriptionTooShortValidation => ("Description must be at least 4 characters", StatusType.Warning),
                StatusMessageKey.DescriptionTooLongValidation => ("Description must be 50 characters or less", StatusType.Warning),
                StatusMessageKey.DeviceSelectionRequired => ("Device selection is required", StatusType.Warning),
                StatusMessageKey.DeviceNameRequired => ("Device name is required when using Write In", StatusType.Warning),
                StatusMessageKey.DeviceNameTooShortValidation => ("Device name must be at least 4 characters", StatusType.Warning),
                StatusMessageKey.DeviceNameTooLongValidation => ("Device name must be 20 characters or less", StatusType.Warning),
                StatusMessageKey.FormReset => ("Form reset - Ready for next ticket", StatusType.Info),
                StatusMessageKey.CredentialsSaved => ("Credentials saved to Windows Credential Manager", StatusType.Success),
                StatusMessageKey.ReinitializingAPIs => ("Re-initializing APIs with new credentials...", StatusType.Info),
                StatusMessageKey.InitializationFailed => ("Initialization failed: {0}", StatusType.Error),
                StatusMessageKey.ErrorShowingSettings => ("Error showing settings dialog: {0}", StatusType.Error),
                StatusMessageKey.EnterCustomDeviceName => ("Enter custom device name", StatusType.Info),
                _ => ("Unknown status message", StatusType.Info)
            };
        }

        /// <summary>
        /// Format a status message with parameters
        /// </summary>
        public static string FormatStatusMessage(StatusMessageKey key, params object[] args)
        {
            var (message, _) = GetStatusMessage(key);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        /// <summary>
        /// Format a popup message content with parameters
        /// </summary>
        public static string FormatPopupContent(PopupMessageKey key, params object[] args)
        {
            var (_, content) = GetPopupMessage(key);
            return args.Length > 0 ? string.Format(content, args) : content;
        }
    }
}
