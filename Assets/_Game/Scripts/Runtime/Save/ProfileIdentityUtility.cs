using System;

namespace AlienDefense.Save
{
    /// <summary>One-time display name / avatar defaults derived from ProfileId. Never regenerates a filled name.</summary>
    public static class ProfileIdentityUtility
    {
        public const int MinDisplayNameLength = 3;
        public const int MaxDisplayNameLength = 20;

        public static void EnsureDisplayIdentity(PlayerProfileSaveData data)
        {
            if (data == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(data.ProfileId))
            {
                data.ProfileId = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(data.DisplayName))
            {
                data.DisplayName = BuildDefaultDisplayName(data.ProfileId);
            }

            if (data.AvatarId < 0)
            {
                data.AvatarId = 0;
            }
        }

        public static string BuildDefaultDisplayName(string profileId)
        {
            string suffix = string.IsNullOrEmpty(profileId)
                ? Guid.NewGuid().ToString("N").Substring(0, 6)
                : profileId.Substring(0, Math.Min(6, profileId.Length));
            return "UFO_Pilot_" + suffix.ToUpperInvariant();
        }

        public static bool TryNormalizeDisplayName(string raw, out string normalized, out string error)
        {
            normalized = null;
            error = null;

            if (raw == null)
            {
                error = "Name cannot be empty.";
                return false;
            }

            string trimmed = raw.Trim();
            if (trimmed.Length < MinDisplayNameLength)
            {
                error = "Name must be at least " + MinDisplayNameLength + " characters.";
                return false;
            }

            if (trimmed.Length > MaxDisplayNameLength)
            {
                error = "Name must be at most " + MaxDisplayNameLength + " characters.";
                return false;
            }

            if (trimmed.IndexOf('\n') >= 0 || trimmed.IndexOf('\r') >= 0)
            {
                error = "Name cannot contain line breaks.";
                return false;
            }

            normalized = trimmed;
            return true;
        }
    }
}
