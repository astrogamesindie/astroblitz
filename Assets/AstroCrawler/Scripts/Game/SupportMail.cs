using System;

namespace AstroCrawler
{
    public static class SupportMail
    {
        public const int MessageLimit = 1500;
        public static bool ValidEmail(string email) => !string.IsNullOrWhiteSpace(email) &&
            email.IndexOf('@') > 0 && email.IndexOf('@') == email.LastIndexOf('@') &&
            email.LastIndexOf('.') > email.IndexOf('@') + 1 && email.LastIndexOf('.') < email.Length - 1 &&
            email.IndexOfAny(new[] { '\r', '\n', ' ', '?', '&', '#' }) < 0;

        public static string ComposeBody(string message, string version, string build, string platform, string language)
        {
            var text = (message ?? "").Trim();
            if (text.Length > MessageLimit)
            {
                var length = MessageLimit;
                if (char.IsHighSurrogate(text[length - 1])) length--;
                text = text.Substring(0, length);
            }
            return text + "\n\n--- Astro Blitz ---\nVersion: " + version + "\nBuild: " + build +
                "\nPlatform: " + platform + "\nLanguage: " + language;
        }

        public static string ComposeUri(string email, string subject, string body)
        {
            if (!ValidEmail(email)) throw new ArgumentException("Invalid support email.");
            return "mailto:" + email + "?subject=" + Uri.EscapeDataString(subject ?? "") +
                "&body=" + Uri.EscapeDataString(body ?? "");
        }
    }
}
