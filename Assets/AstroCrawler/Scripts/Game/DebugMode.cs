// AstroCrawler - hidden debug mode for testing on a device.
//
// Opened by tapping the menu logo 10 times and typing the password. While on,
// every campaign stage and the endless mode are open; the stages are not
// marked as beaten, so turning it off puts the real progress back. The flag
// lives only on this device (it is not part of the cloud save), but credits
// granted here are normal credits and do sync.

using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AstroCrawler
{
    public static class DebugMode
    {
        public const int TapsToOpen = 10;
        public const int CreditGrant = 1000000;

        private const string EnabledKey = "AstroCrawler.Debug.v1.enabled";

        // SHA-256 of the password, so the plain text is not in the build.
        private const string PasswordHash = "927a9cffdf8c766a8164d4d1cf35df5426115a4327adca1df969f0ed93509a68";

        public static bool Enabled => PlayerPrefs.GetInt(EnabledKey, 0) == 1;

        public static bool CheckPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(password.Trim()));
                var text = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2"));
                return text.ToString() == PasswordHash;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(EnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
