using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Configuration constants for the application
    /// </summary>
    public static class Config
    {
        // PowerSchool API Configuration
        public const string PS_BASE_URL = "https://powerschool.sugarsalem.org";
        public const string PS_OAUTH_TOKEN_ENDPOINT = "/oauth/access_token";
        public const string PS_NAMED_QUERY_ENDPOINT = "/ws/schema/query/{0}";
        public const string PS_STUDENT_QUERY = "com.sugarsalem.device.lookup.student";
        public const string PS_TEACHER_QUERY = "com.sugarsalem.device.lookup.teacher";
        public const string PS_USER_QUERY = "com.sugarsalem.device.lookup.user";

        // NinjaOne RMM Configuration
        public const string NINJA_BASE_URL = "https://app.ninjarmm.com";  // Note: lowercase 'app' in URL
        public const string NINJA_REDIRECT_URI = "http://localhost:8080/callback";
        public const string NINJA_OAUTH_SCOPE = "monitoring management offline_access";
        public const string NINJA_USERS_ENDPOINT = "/v2/users"; // Full URL will be: https://app.ninjarmm.com/v2/users

        // Organization ID - can be updated from settings dialog
        public static string NINJA_ORGANIZATION_ID = "2"; // Default organization ID

        /// <summary>
        /// Get PowerSchool Client ID from Credential Manager
        /// </summary>
        public static string GetPowerSchoolClientId()
        {
            return CredentialManager.GetPowerSchoolClientId();
        }

        /// <summary>
        /// Get PowerSchool Client Secret from Credential Manager
        /// </summary>
        public static string GetPowerSchoolClientSecret()
        {
            return CredentialManager.GetPowerSchoolClientSecret();
        }

        /// <summary>
        /// Get NinjaOne Client ID from Credential Manager
        /// </summary>
        public static string GetNinjaOneClientId()
        {
            return CredentialManager.GetNinjaOneClientId();
        }

        /// <summary>
        /// Get NinjaOne Client Secret from Credential Manager
        /// </summary>
        public static string GetNinjaOneClientSecret()
        {
            return CredentialManager.GetNinjaOneClientSecret();
        }

        /// <summary>
        /// Check if all required credentials are configured
        /// </summary>
        public static bool AreCredentialsConfigured()
        {
            return CredentialManager.HasPowerSchoolCredentials() &&
                   CredentialManager.HasNinjaOneCredentials();
        }
    }

    /// <summary>
    /// Handles PowerSchool API authentication and requests using OAuth2 client credentials flow
    /// </summary>
    public class PowerSchoolAPI
    {
        private readonly string _baseUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly HttpClient _httpClient;

        private string _accessToken;
        private DateTime _tokenExpiry;

        public PowerSchoolAPI(string baseUrl, string clientId, string clientSecret)
        {
            _baseUrl = baseUrl;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// Obtains an access token using OAuth2 client credentials flow
        /// </summary>
        private async Task<string> GetAccessTokenAsync()
        {
            // Return cached token if still valid
            if (IsTokenValid())
                return _accessToken;

            // Request new token
            string tokenUrl = $"{_baseUrl}{Config.PS_OAUTH_TOKEN_ENDPOINT}";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret)
            });

            try
            {
                var response = await _httpClient.PostAsync(tokenUrl, content);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                var tokenData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

                _accessToken = tokenData["access_token"].ToString();

                // Set expiry with 60 second buffer
                int expiresIn = tokenData.ContainsKey("expires_in")
                    ? Convert.ToInt32(tokenData["expires_in"])
                    : 3600;
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

                return _accessToken;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to obtain access token: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Check if the current token is still valid
        /// </summary>
        private bool IsTokenValid()
        {
            return !string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiry;
        }

        /// <summary>
        /// Queries PowerSchool for user information and associated devices.
        /// Tries student, teacher, and user queries in sequence.
        /// </summary>
        public async Task<UserData> LookupDevicesAsync(string username)
        {
            // Try student query first
            var result = await TryQueryAsync(Config.PS_STUDENT_QUERY, username, "students");
            if (result != null)
                return result;

            // Try teacher query
            result = await TryQueryAsync(Config.PS_TEACHER_QUERY, username, "teachers");
            if (result != null)
                return result;

            // Try user query
            result = await TryQueryAsync(Config.PS_USER_QUERY, username, "users");
            if (result != null)
                return result;

            return null;
        }

        /// <summary>
        /// Attempts a specific PowerSchool query and parses the response
        /// </summary>
        private async Task<UserData> TryQueryAsync(string queryName, string username, string tableName)
        {
            string token = await GetAccessTokenAsync();
            string queryUrl = $"{_baseUrl}{string.Format(Config.PS_NAMED_QUERY_ENDPOINT, queryName)}";

            var request = new HttpRequestMessage(HttpMethod.Post, queryUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Create JSON payload
            var payload = new { username = username };
            request.Content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                // Debug: Log the raw response
                System.Diagnostics.Debug.WriteLine($"[PowerSchool] Query: {queryName}");
                System.Diagnostics.Debug.WriteLine($"[PowerSchool] Username: {username}");
                System.Diagnostics.Debug.WriteLine($"[PowerSchool] Response length: {responseBody.Length} characters");
                System.Diagnostics.Debug.WriteLine($"[PowerSchool] Raw Response: {responseBody}");

                var data = JObject.Parse(responseBody);

                // Check if records exist
                if (data["record"] == null || !data["record"].Any())
                {
                    System.Diagnostics.Debug.WriteLine($"[PowerSchool] No records returned for query {queryName}");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"[PowerSchool] Found {data["record"].Count()} record(s)");

                // Parse all records to extract devices and school IDs
                var devices = new HashSet<string>(); // Use HashSet to ensure unique device names
                var schoolIds = new HashSet<string>(); // Use HashSet for unique school IDs
                string studentNumber = null;
                string teacherNumber = null;

                int recordIndex = 0;
                foreach (var record in data["record"])
                {
                    recordIndex++;
                    System.Diagnostics.Debug.WriteLine($"[PowerSchool] Processing record #{recordIndex}:");

                    // Log all fields in the record
                    foreach (var property in record.Children<JProperty>())
                    {
                        System.Diagnostics.Debug.WriteLine($"  {property.Name}: {property.Value}");
                    }

                    // FLATTENED structure: fields are directly on the record
                    // Extract device name (changed from device_number to device_name)
                    var deviceName = record["device_name"]?.ToString();
                    if (!string.IsNullOrEmpty(deviceName))
                    {
                        System.Diagnostics.Debug.WriteLine($"  → Found device: {deviceName}");
                        devices.Add(deviceName);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  → No device_name field or empty");
                    }

                    // Extract school ID
                    var schoolId = record["schoolid"]?.ToString();
                    if (!string.IsNullOrEmpty(schoolId))
                    {
                        System.Diagnostics.Debug.WriteLine($"  → Found schoolid: {schoolId}");
                        schoolIds.Add(schoolId);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  → No schoolid field or empty");
                    }

                    // Extract student or teacher number (should be same across all records)
                    if (studentNumber == null)
                    {
                        studentNumber = record["studentnumber"]?.ToString();
                        if (!string.IsNullOrEmpty(studentNumber))
                        {
                            System.Diagnostics.Debug.WriteLine($"  → Found student_number: {studentNumber}");
                        }
                    }
                    if (teacherNumber == null)
                    {
                        teacherNumber = record["teachernumber"]?.ToString();
                        if (!string.IsNullOrEmpty(teacherNumber))
                        {
                            System.Diagnostics.Debug.WriteLine($"  → Found teachernumber: {teacherNumber}");
                        }
                    }
                }

                // Summary
                System.Diagnostics.Debug.WriteLine($"[PowerSchool] Summary for {queryName}:");
                System.Diagnostics.Debug.WriteLine($"  School IDs: {string.Join(", ", schoolIds)}");
                System.Diagnostics.Debug.WriteLine($"  Student Number: {studentNumber ?? "null"}");
                System.Diagnostics.Debug.WriteLine($"  Teacher Number: {teacherNumber ?? "null"}");
                System.Diagnostics.Debug.WriteLine($"  Devices: {string.Join(", ", devices)}");

                // With LEFT JOIN, we should always have school IDs and identity numbers
                // even if no devices are found
                if (schoolIds.Any() && (studentNumber != null || teacherNumber != null))
                {
                    System.Diagnostics.Debug.WriteLine($"[PowerSchool] ✓ Returning UserData for {username}");
                    return new UserData
                    {
                        Username = username,
                        SchoolIds = schoolIds.ToList(),
                        StudentNumber = studentNumber,
                        TeacherNumber = teacherNumber,
                        Devices = devices.ToList(), // May be empty list if no devices
                        UserType = tableName
                    };
                }

                System.Diagnostics.Debug.WriteLine($"[PowerSchool] ✗ Not returning data - missing required fields");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PowerSchool] ERROR in {queryName}: {ex.Message}");
                throw new Exception($"PowerSchool query failed for {queryName}: {ex.Message}", ex);
            }
        }
    }
    
    /// <summary>
    /// Handles NinjaOne API authentication using OAuth2 Authorization Code flow with PKCE and refresh tokens
    /// </summary>
    public class NinjaOneAPI
    {
        private readonly string _baseUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _organizationId;
        private readonly HttpClient _httpClient;

        private string _accessToken;
        private string _refreshToken;
        private DateTime _tokenExpiry;
        private string _codeVerifier; // PKCE code verifier
        private string _codeChallenge; // PKCE code challenge

        public NinjaOneAPI(string baseUrl, string clientId, string clientSecret, string organizationId)
        {
            _baseUrl = baseUrl;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _organizationId = organizationId;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// Check if we have a valid refresh token stored
        /// </summary>
        public bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(_refreshToken);
        }

        /// <summary>
        /// Get the stored refresh token (for persistence later)
        /// </summary>
        public string GetRefreshToken()
        {
            return _refreshToken;
        }

        /// <summary>
        /// Set the refresh token (for loading from storage)
        /// </summary>
        public void SetRefreshToken(string refreshToken)
        {
            _refreshToken = refreshToken;
            _accessToken = null; // Clear access token to force refresh
        }

        /// <summary>
        /// Generates a cryptographically secure PKCE code verifier
        /// </summary>
        private string GenerateCodeVerifier()
        {
            // PKCE code verifier: 43-128 character random string
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
            var bytes = new byte[64];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

            var result = new StringBuilder(64);
            foreach (var b in bytes)
            {
                result.Append(chars[b % chars.Length]);
            }
            return result.ToString();
        }

        /// <summary>
        /// Generates PKCE code challenge from code verifier using SHA256
        /// </summary>
        private string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
                return Convert.ToBase64String(hash)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }
        }

        /// <summary>
        /// Builds the OAuth authorization URL for user authentication with PKCE
        /// </summary>
        public string BuildAuthorizationUrl(string state = null)
        {
            if (string.IsNullOrEmpty(state))
            {
                state = Guid.NewGuid().ToString("N");
            }

            // Generate PKCE values
            _codeVerifier = GenerateCodeVerifier();
            _codeChallenge = GenerateCodeChallenge(_codeVerifier);

            var queryParams = new Dictionary<string, string>
            {
                { "response_type", "code" },
                { "client_id", _clientId },
                { "redirect_uri", Config.NINJA_REDIRECT_URI },
                { "scope", Config.NINJA_OAUTH_SCOPE },
                { "state", state },
                { "code_challenge", _codeChallenge },
                { "code_challenge_method", "S256" }
            };

            var queryString = string.Join("&", queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            return $"{_baseUrl}/ws/oauth/authorize?{queryString}";
        }

        /// <summary>
        /// Starts the OAuth flow by opening browser and listening for callback
        /// Returns the authorization code or throws exception
        /// </summary>
        public async Task<string> StartOAuthFlowAsync()
        {
            string state = Guid.NewGuid().ToString("N");
            string authUrl = BuildAuthorizationUrl(state);

            // Start local HTTP listener for callback
            using (var listener = new OAuthCallbackListener("http://localhost:8080/"))
            {
                // Open browser to authorization URL
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = authUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to open browser for authentication: {ex.Message}", ex);
                }

                // Wait for callback (with timeout)
                var result = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(5));

                // Verify state matches
                if (result.State != state)
                {
                    throw new Exception("OAuth state mismatch - possible security issue");
                }

                if (!string.IsNullOrEmpty(result.Error))
                {
                    throw new Exception($"OAuth error: {result.Error} - {result.ErrorDescription}");
                }

                if (string.IsNullOrEmpty(result.Code))
                {
                    throw new Exception("No authorization code received");
                }

                return result.Code;
            }
        }

        /// <summary>
        /// Exchanges authorization code for access token and refresh token with PKCE
        /// </summary>
        public async Task ExchangeCodeForTokensAsync(string code)
        {
            if (string.IsNullOrEmpty(_codeVerifier))
            {
                throw new Exception("PKCE code verifier is missing. This is a security error.");
            }

            string tokenUrl = $"{_baseUrl}/ws/oauth/token";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("redirect_uri", Config.NINJA_REDIRECT_URI),
                new KeyValuePair<string, string>("code_verifier", _codeVerifier)
            });

            try
            {
                var response = await _httpClient.PostAsync(tokenUrl, content);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                var tokenData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

                _accessToken = tokenData["access_token"].ToString();
                _refreshToken = tokenData["refresh_token"].ToString();

                // Set expiry with 60 second buffer
                int expiresIn = tokenData.ContainsKey("expires_in")
                    ? Convert.ToInt32(tokenData["expires_in"])
                    : 3600;
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

                // Clear PKCE code verifier for security (one-time use)
                _codeVerifier = null;
                _codeChallenge = null;
            }
            catch (Exception ex)
            {
                // Clear PKCE values on error
                _codeVerifier = null;
                _codeChallenge = null;
                throw new Exception($"Failed to exchange code for tokens: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Refreshes the access token using the refresh token
        /// </summary>
        private async Task RefreshAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
            {
                throw new Exception("No refresh token available. Please authenticate first.");
            }

            string tokenUrl = $"{_baseUrl}/ws/oauth/token";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", _refreshToken),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret)
            });

            try
            {
                var response = await _httpClient.PostAsync(tokenUrl, content);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                var tokenData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);

                _accessToken = tokenData["access_token"].ToString();

                // Update refresh token if a new one was provided
                if (tokenData.ContainsKey("refresh_token"))
                {
                    _refreshToken = tokenData["refresh_token"].ToString();
                }

                // Set expiry with 60 second buffer
                int expiresIn = tokenData.ContainsKey("expires_in")
                    ? Convert.ToInt32(tokenData["expires_in"])
                    : 3600;
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);
            }
            catch (Exception ex)
            {
                // If refresh token is invalid, clear it
                _refreshToken = null;
                _accessToken = null;
                throw new Exception($"Failed to refresh access token: {ex.Message}. Please sign in again.", ex);
            }
        }

        /// <summary>
        /// Gets a valid access token, refreshing if necessary
        /// </summary>
        private async Task<string> GetAccessTokenAsync()
        {
            // If access token is still valid, return it
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiry)
            {
                return _accessToken;
            }

            // Need to refresh
            await RefreshAccessTokenAsync();
            return _accessToken;
        }

        /// <summary>
        /// Gets all users from NinjaOne for caching/autocomplete purposes
        /// Returns list of all end-users and technicians
        /// </summary>
        public async Task<List<NinjaEndUser>> GetAllUsersAsync()
        {
            string token = await GetAccessTokenAsync();
            string usersUrl = $"{_baseUrl}{Config.NINJA_USERS_ENDPOINT}";

            var request = new HttpRequestMessage(HttpMethod.Get, usersUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                var users = JsonConvert.DeserializeObject<List<NinjaEndUser>>(responseBody);

                System.Diagnostics.Debug.WriteLine($"[NinjaOne] GetAllUsersAsync returned {users?.Count ?? 0} users");

                return users ?? new List<NinjaEndUser>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NinjaOne] ERROR in GetAllUsersAsync: {ex.Message}");
                throw new Exception($"Failed to get all users: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Looks up a user by username (matches email prefix before @)
        /// Uses /v2/users which includes both end-users and technicians
        /// </summary>
        public async Task<NinjaEndUser> LookupEndUserAsync(string username)
        {
            string token = await GetAccessTokenAsync();
            string usersUrl = $"{_baseUrl}{Config.NINJA_USERS_ENDPOINT}";

            var request = new HttpRequestMessage(HttpMethod.Get, usersUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                // Debug: Log the full URL being called
                System.Diagnostics.Debug.WriteLine($"[NinjaOne] URL: {usersUrl}");
                System.Diagnostics.Debug.WriteLine($"[NinjaOne] Response length: {responseBody.Length} characters");

                var users = JsonConvert.DeserializeObject<List<NinjaEndUser>>(responseBody);

                System.Diagnostics.Debug.WriteLine($"[NinjaOne] Total users returned: {users?.Count ?? 0}");

                if (users == null || users.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[NinjaOne] No users returned from API");
                    return null;
                }

                // Normalize the search username (trim, uppercase)
                string normalizedSearch = username.Trim().ToUpper();
                System.Diagnostics.Debug.WriteLine($"[NinjaOne] Searching for normalized username: '{normalizedSearch}'");

                // Find matching user by comparing email prefix (before @)
                int checkedCount = 0;
                foreach (var user in users)
                {
                    checkedCount++;

                    if (!string.IsNullOrEmpty(user.Email) && user.Email.Contains("@"))
                    {
                        // Extract email prefix and normalize (trim, uppercase)
                        string emailPrefix = user.Email.Split('@')[0];
                        string normalizedEmailPrefix = emailPrefix.Trim().ToUpper();

                        // Debug: Log first 10 comparisons
                        if (checkedCount <= 10)
                        {
                            System.Diagnostics.Debug.WriteLine($"[NinjaOne] Checking user #{checkedCount}: {user.FirstName} {user.LastName}");
                            System.Diagnostics.Debug.WriteLine($"  Email: {user.Email}");
                            System.Diagnostics.Debug.WriteLine($"  Email prefix: '{emailPrefix}' → Normalized: '{normalizedEmailPrefix}'");
                            System.Diagnostics.Debug.WriteLine($"  Match? {(normalizedEmailPrefix == normalizedSearch ? "YES ✓" : "NO")}");
                        }

                        // Match if the normalized versions are equal
                        // This handles: clstewart, CLSTEWART, ClStewart, " clstewart " all matching clstewart@sugarsalem.com
                        if (normalizedEmailPrefix == normalizedSearch)
                        {
                            System.Diagnostics.Debug.WriteLine($"[NinjaOne] ✓ MATCH FOUND: {user.FirstName} {user.LastName} ({user.Email})");
                            System.Diagnostics.Debug.WriteLine($"[NinjaOne]   UID: {user.Uid}");
                            return user;
                        }
                    }
                    else
                    {
                        if (checkedCount <= 10)
                        {
                            System.Diagnostics.Debug.WriteLine($"[NinjaOne] User #{checkedCount}: {user.FirstName} {user.LastName} - No valid email");
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[NinjaOne] No match found after checking {checkedCount} users");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NinjaOne] ERROR: {ex.Message}");
                throw new Exception($"Failed to lookup user: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a new ticket in NinjaOne RMM using the proper API structure
        /// </summary>
        public async Task<Dictionary<string, object>> CreateTicketAsync(
            string subject,
            string body,
            string requesterUid,
            string requesterName,
            string requesterEmail,
            string schoolAffiliation,
            string? deviceName = null,
            string? studentNumber = null,
            string? teacherNumber = null)
        {
            string token = await GetAccessTokenAsync();
            string ticketUrl = $"{_baseUrl}/v2/ticketing/ticket";

            // Determine ticket form based on school affiliation
            int ticketFormId = schoolAffiliation == "SSJHS" ? 9 : 2;

            // Map school affiliation to location ID
            int? locationId = schoolAffiliation switch
            {
                "SSHS" => 5,   // High School
                "SSJHS" => 6,  // Junior High
                "CES" => 8,    // Central Elementary
                "KIS" => 7,    // Kershaw
                "VVHS" => 9,   // Valley View High School
                "SSO" => 10,   // Sugar Salem Online
                _ => null      // No location if school not matched
            };

            // Build attributes array for custom fields
            var attributes = new List<Dictionary<string, object>>();

            // Field 173: Assigned User (Name and Email)
            if (!string.IsNullOrEmpty(requesterName) && !string.IsNullOrEmpty(requesterEmail))
            {
                attributes.Add(new Dictionary<string, object>
        {
            { "attributeId", 173 },
            { "value", $"{requesterName} ({requesterEmail})" }
        });
            }

            // Field 164: EDUID (Student or Teacher Number)
            string eduId = studentNumber ?? teacherNumber;
            if (!string.IsNullOrEmpty(eduId))
            {
                attributes.Add(new Dictionary<string, object>
        {
            { "attributeId", 164 },
            { "value", eduId }
        });
            }

            // Field 158: Asset Tag (Device Name)
            if (!string.IsNullOrEmpty(deviceName))
            {
                attributes.Add(new Dictionary<string, object>
        {
            { "attributeId", 158 },
            { "value", deviceName }
        });
            }

            // Build request using proper NinjaOne API structure
            var ticketData = new Dictionary<string, object>
    {
        { "clientId", 2 },
        { "ticketFormId", ticketFormId },
        { "status", "1000" },
        { "type", "PROBLEM" },
        { "subject", subject },
        { "description", new Dictionary<string, object>
            {
                { "public", true },
                { "htmlBody", $"<p>{body.Replace("\n", "<br>")}</p>" }
            }
        },
        { "requesterUid", requesterUid },
        { "tags", new List<string> { "Kiosk" } }
    };

            // Add location ID if we have one
            if (locationId.HasValue)
            {
                ticketData.Add("locationId", locationId.Value);
            }

            // Add attributes if we have any
            if (attributes.Any())
            {
                ticketData.Add("attributes", attributes);
            }

            var jsonString = JsonConvert.SerializeObject(ticketData);

            // Debug logging
            System.Diagnostics.Debug.WriteLine($"[NinjaOne] Creating ticket with payload:");
            System.Diagnostics.Debug.WriteLine(jsonString);

            var content = new StringContent(
                jsonString,
                Encoding.UTF8,
                "application/json"
            );

            var request = new HttpRequestMessage(HttpMethod.Post, ticketUrl)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await _httpClient.SendAsync(request);

                // Get response body regardless of success/failure
                string responseBody = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[NinjaOne] Response status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[NinjaOne] Response body: {responseBody}");

                response.EnsureSuccessStatusCode();

                return JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);
            }
            catch (HttpRequestException ex)
            {
                string errorDetails = ex.Message;
                if (ex.InnerException != null)
                {
                    errorDetails += $"\nInner: {ex.InnerException.Message}";
                }
                throw new Exception($"Failed to create ticket: {errorDetails}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create ticket: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// HTTP listener for OAuth callback
    /// </summary>
    public class OAuthCallbackListener : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly TaskCompletionSource<OAuthCallbackResult> _callbackReceived;

        public OAuthCallbackListener(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _callbackReceived = new TaskCompletionSource<OAuthCallbackResult>();
        }

        public async Task<OAuthCallbackResult> WaitForCallbackAsync(TimeSpan timeout)
        {
            _listener.Start();

            // Start listening for requests
            _ = Task.Run(async () =>
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;

                    // Parse query parameters
                    var queryParams = HttpUtility.ParseQueryString(request.Url.Query);

                    // Security check: Detect if tokens are being passed in URL (should never happen with code flow)
                    if (queryParams["access_token"] != null || queryParams["refresh_token"] != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[SECURITY WARNING] Tokens detected in callback URL! This should not happen with authorization code flow.");
                        System.Diagnostics.Debug.WriteLine("[SECURITY WARNING] Check OAuth provider configuration - it may be using implicit flow instead of code flow.");
                    }

                    var result = new OAuthCallbackResult
                    {
                        Code = queryParams["code"],
                        State = queryParams["state"],
                        Error = queryParams["error"],
                        ErrorDescription = queryParams["error_description"]
                    };

                    // Send response to browser
                    string responseString = result.Error == null
                        ? "<html><body><h1>Authentication Successful!</h1><p>You can close this window and return to the application.</p><script>window.history.replaceState({}, document.title, window.location.pathname);</script></body></html>"
                        : $"<html><body><h1>Authentication Failed</h1><p>Error: {result.Error}</p><p>{result.ErrorDescription}</p></body></html>";

                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html";
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();

                    _callbackReceived.SetResult(result);
                }
                catch (Exception ex)
                {
                    _callbackReceived.SetException(ex);
                }
            });

            // Wait for callback with timeout
            using (var cts = new CancellationTokenSource(timeout))
            {
                var timeoutTask = Task.Delay(timeout, cts.Token);
                var completedTask = await Task.WhenAny(_callbackReceived.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("OAuth callback timeout - user did not complete authentication");
                }

                cts.Cancel();
                return await _callbackReceived.Task;
            }
        }

        public void Dispose()
        {
            _listener?.Stop();
            _listener?.Close();
        }
    }

    /// <summary>
    /// Result from OAuth callback
    /// </summary>
    public class OAuthCallbackResult
    {
        public required string Code { get; set; }
        public required string State { get; set; }
        public required string Error { get; set; }
        public required string ErrorDescription { get; set; }
    }

    /// <summary>
    /// Represents an end user from NinjaOne
    /// </summary>
    public class NinjaEndUser
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("uid")]
        public required string Uid { get; set; }

        [JsonProperty("firstName")]
        public required string FirstName { get; set; }

        [JsonProperty("lastName")]
        public required string LastName { get; set; }

        [JsonProperty("email")]
        public required string Email { get; set; }

        [JsonProperty("phone")]
        public required string Phone { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("userType")]
        public required string UserType { get; set; }

        // Full name helper property
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}