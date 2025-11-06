using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ITTicketingKiosk
{
    /// <summary>
    /// Helper class for managing credentials in Windows Credential Manager using P/Invoke
    /// </summary>
    public static class CredentialManager
    {
        // Resource names for credential vault
        private const string RESOURCE_POWERSCHOOL = "ITTicketingKiosk_PowerSchool";
        private const string RESOURCE_NINJAONE = "ITTicketingKiosk_NinjaOne";

        // Credential identifiers
        private const string PS_CLIENT_ID = "PS_ClientID";
        private const string PS_CLIENT_SECRET = "PS_ClientSecret";
        private const string NINJA_CLIENT_ID = "Ninja_ClientID";
        private const string NINJA_CLIENT_SECRET = "Ninja_ClientSecret";
        private const string NINJA_REFRESH_TOKEN = "Ninja_RefreshToken";

        /// <summary>
        /// Check if all required PowerSchool credentials are stored
        /// </summary>
        public static bool HasPowerSchoolCredentials()
        {
            try
            {
                return !string.IsNullOrEmpty(GetCredential(RESOURCE_POWERSCHOOL, PS_CLIENT_ID)) &&
                       !string.IsNullOrEmpty(GetCredential(RESOURCE_POWERSCHOOL, PS_CLIENT_SECRET));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if all required NinjaOne OAuth credentials are stored (not including refresh token)
        /// </summary>
        public static bool HasNinjaOneCredentials()
        {
            try
            {
                return !string.IsNullOrEmpty(GetCredential(RESOURCE_NINJAONE, NINJA_CLIENT_ID)) &&
                       !string.IsNullOrEmpty(GetCredential(RESOURCE_NINJAONE, NINJA_CLIENT_SECRET));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if NinjaOne refresh token exists
        /// </summary>
        public static bool HasNinjaOneRefreshToken()
        {
            try
            {
                return !string.IsNullOrEmpty(GetCredential(RESOURCE_NINJAONE, NINJA_REFRESH_TOKEN));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if all credentials (including refresh token) are configured
        /// </summary>
        public static bool IsFullyConfigured()
        {
            return HasPowerSchoolCredentials() && HasNinjaOneCredentials() && HasNinjaOneRefreshToken();
        }

        #region PowerSchool Credentials

        /// <summary>
        /// Save PowerSchool client ID
        /// </summary>
        public static void SavePowerSchoolClientId(string clientId)
        {
            SaveCredential(RESOURCE_POWERSCHOOL, PS_CLIENT_ID, clientId);
        }

        /// <summary>
        /// Get PowerSchool client ID
        /// </summary>
        public static string GetPowerSchoolClientId()
        {
            return GetCredential(RESOURCE_POWERSCHOOL, PS_CLIENT_ID);
        }

        /// <summary>
        /// Save PowerSchool client secret
        /// </summary>
        public static void SavePowerSchoolClientSecret(string clientSecret)
        {
            SaveCredential(RESOURCE_POWERSCHOOL, PS_CLIENT_SECRET, clientSecret);
        }

        /// <summary>
        /// Get PowerSchool client secret
        /// </summary>
        public static string GetPowerSchoolClientSecret()
        {
            return GetCredential(RESOURCE_POWERSCHOOL, PS_CLIENT_SECRET);
        }

        /// <summary>
        /// Clear all PowerSchool credentials
        /// </summary>
        public static void ClearPowerSchoolCredentials()
        {
            RemoveCredential(RESOURCE_POWERSCHOOL, PS_CLIENT_ID);
            RemoveCredential(RESOURCE_POWERSCHOOL, PS_CLIENT_SECRET);
        }

        #endregion

        #region NinjaOne Credentials

        /// <summary>
        /// Save NinjaOne client ID
        /// </summary>
        public static void SaveNinjaOneClientId(string clientId)
        {
            SaveCredential(RESOURCE_NINJAONE, NINJA_CLIENT_ID, clientId);
        }

        /// <summary>
        /// Get NinjaOne client ID
        /// </summary>
        public static string GetNinjaOneClientId()
        {
            return GetCredential(RESOURCE_NINJAONE, NINJA_CLIENT_ID);
        }

        /// <summary>
        /// Save NinjaOne client secret
        /// </summary>
        public static void SaveNinjaOneClientSecret(string clientSecret)
        {
            SaveCredential(RESOURCE_NINJAONE, NINJA_CLIENT_SECRET, clientSecret);
        }

        /// <summary>
        /// Get NinjaOne client secret
        /// </summary>
        public static string GetNinjaOneClientSecret()
        {
            return GetCredential(RESOURCE_NINJAONE, NINJA_CLIENT_SECRET);
        }

        /// <summary>
        /// Save NinjaOne refresh token
        /// </summary>
        public static void SaveNinjaOneRefreshToken(string refreshToken)
        {
            SaveCredential(RESOURCE_NINJAONE, NINJA_REFRESH_TOKEN, refreshToken);
        }

        /// <summary>
        /// Get NinjaOne refresh token
        /// </summary>
        public static string GetNinjaOneRefreshToken()
        {
            return GetCredential(RESOURCE_NINJAONE, NINJA_REFRESH_TOKEN);
        }

        /// <summary>
        /// Clear NinjaOne refresh token (used when token is invalid or user signs out)
        /// </summary>
        public static void ClearNinjaOneRefreshToken()
        {
            RemoveCredential(RESOURCE_NINJAONE, NINJA_REFRESH_TOKEN);
        }

        /// <summary>
        /// Clear all NinjaOne credentials (OAuth + refresh token)
        /// </summary>
        public static void ClearNinjaOneCredentials()
        {
            RemoveCredential(RESOURCE_NINJAONE, NINJA_CLIENT_ID);
            RemoveCredential(RESOURCE_NINJAONE, NINJA_CLIENT_SECRET);
            RemoveCredential(RESOURCE_NINJAONE, NINJA_REFRESH_TOKEN);
        }

        #endregion

        #region Clear All

        /// <summary>
        /// Clear all stored credentials (PowerSchool + NinjaOne)
        /// </summary>
        public static void ClearAllCredentials()
        {
            ClearPowerSchoolCredentials();
            ClearNinjaOneCredentials();
        }

        #endregion

        #region Windows Credential Manager P/Invoke

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, uint type, int reservedFlag);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CredFree([In] IntPtr buffer);

        private const uint CRED_TYPE_GENERIC = 1;
        private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

        /// <summary>
        /// Save a credential to Windows Credential Manager
        /// </summary>
        private static void SaveCredential(string resource, string username, string password)
        {
            try
            {
                // Combine resource and username for unique target name
                string targetName = $"{resource}_{username}";

                byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
                IntPtr passwordPtr = Marshal.AllocHGlobal(passwordBytes.Length);
                Marshal.Copy(passwordBytes, 0, passwordPtr, passwordBytes.Length);

                try
                {
                    CREDENTIAL credential = new CREDENTIAL
                    {
                        Type = CRED_TYPE_GENERIC,
                        TargetName = targetName,
                        UserName = username,
                        CredentialBlob = passwordPtr,
                        CredentialBlobSize = (uint)passwordBytes.Length,
                        Persist = CRED_PERSIST_LOCAL_MACHINE
                    };

                    if (!CredWrite(ref credential, 0))
                    {
                        int error = Marshal.GetLastWin32Error();
                        throw new Exception($"Failed to save credential. Error code: {error}");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(passwordPtr);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save credential: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Retrieve a credential from Windows Credential Manager
        /// </summary>
        private static string GetCredential(string resource, string username)
        {
            try
            {
                string targetName = $"{resource}_{username}";
                IntPtr credPtr;

                if (!CredRead(targetName, CRED_TYPE_GENERIC, 0, out credPtr))
                {
                    return null;
                }

                try
                {
                    CREDENTIAL credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                    byte[] passwordBytes = new byte[credential.CredentialBlobSize];
                    Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, (int)credential.CredentialBlobSize);
                    return Encoding.Unicode.GetString(passwordBytes);
                }
                finally
                {
                    CredFree(credPtr);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Remove a credential from Windows Credential Manager
        /// </summary>
        private static void RemoveCredential(string resource, string username)
        {
            try
            {
                string targetName = $"{resource}_{username}";
                CredDelete(targetName, CRED_TYPE_GENERIC, 0);
            }
            catch
            {
                // Credential doesn't exist, ignore
            }
        }

        #endregion
    }
}
