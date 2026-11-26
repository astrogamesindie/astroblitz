using System;

namespace AstroCrawler
{
    public static class StoreVersion
    {
        /// <summary>Numeric dotted versions, including 1 == 1.0 and 1.10 > 1.9. Never guess malformed versions.</summary>
        public static bool TryCompare(string candidate, string installed, out int comparison)
        {
            comparison = 0;
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(installed)) return false;
            var left = candidate.Trim().Split('.');
            var right = installed.Trim().Split('.');
            if (!Valid(left) || !Valid(right)) return false;
            for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
            {
                var a = i < left.Length ? left[i].TrimStart('0') : "";
                var b = i < right.Length ? right[i].TrimStart('0') : "";
                var result = a.Length.CompareTo(b.Length);
                if (result == 0) result = string.CompareOrdinal(a, b);
                if (result != 0) { comparison = Math.Sign(result); return true; }
            }
            return true;
        }

        private static bool Valid(string[] parts)
        {
            foreach (var part in parts)
            {
                if (part.Length == 0) return false;
                foreach (var ch in part) if (ch < '0' || ch > '9') return false;
            }
            return true;
        }
    }
}
