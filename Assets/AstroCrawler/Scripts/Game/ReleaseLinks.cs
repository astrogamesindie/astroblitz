using System;

namespace AstroCrawler
{
    /// <summary>Links read from Resources/AstroCrawler/ReleaseLinks.json.</summary>
    [Serializable]
    public sealed class ReleaseLinks
    {
        public string privacyPolicyUrl;
        public string supportUrl;
        public string supportEmail;
    }
}
