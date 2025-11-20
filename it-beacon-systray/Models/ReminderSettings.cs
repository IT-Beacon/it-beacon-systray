namespace it_beacon_systray.Models
{
    /// <summary>
    /// Holds all configuration settings related to the restart reminder feature.
    /// </summary>
    public class ReminderSettings
    {
        public string Title { get; set; } = string.Empty;
        public string PrimaryButtonText { get; set; } = string.Empty;
        public string DeferralButtonText { get; set; } = string.Empty;
        public string Glyph { get; set; } = string.Empty;
        public int MaxDeferrals { get; set; }
        public string AggressiveMessage { get; set; } = string.Empty;
        public string NormalMessage { get; set; } = string.Empty;
        public int DeferralDuration { get; set; }
        public int TriggerTime { get; set; }
        public bool Enabled { get; set; }
    }
}
