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
    public partial class MainWindow : Window, IDisposable
    {
        private PowerSchoolAPI? _psApi;
        private NinjaOneAPI? _ninjaApi;
        private bool _disposed = false;
        private UserData? _currentUser;
        private NinjaEndUser? _currentNinjaUser;
        private int _currentPage = 1;
        private bool _testModeEnabled = false;
        private bool _settingsUnlocked = false;
        private bool _isDeviceWriteInMode = false;
        private List<string> _cachedUsernames = new List<string>();
        private NinjaEndUser? _kioskUser;
        private System.Threading.CancellationTokenSource? _oauthCancellationTokenSource;
        private Task? _activeOAuthTask;
        private int? _existingTicketId;
        private string? _existingTicketSubject;
        private bool _isContinueTicketMode = false;
        private bool _isSearching = false;
        private readonly System.Threading.SemaphoreSlim _dialogSemaphore = new System.Threading.SemaphoreSlim(1, 1);

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
        /// Dispose pattern implementation for proper resource cleanup
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _psApi?.Dispose();
                    _ninjaApi?.Dispose();
                    _dialogSemaphore?.Dispose();
                    _oauthCancellationTokenSource?.Dispose();
                }
                _disposed = true;
            }
        }
    }

    // Data Models
    /// <summary>
    /// Represents user data retrieved from PowerSchool including devices and identifiers
    /// </summary>
    public class UserData
    {
        public required string Username { get; set; }
        public required List<string> SchoolIds { get; set; } // Support multiple school affiliations
        public required string StudentNumber { get; set; }
        public required string TeacherNumber { get; set; }
        public required List<string> Devices { get; set; }
        public required string UserType { get; set; } // "students", "teachers", or "users"
    }
}
