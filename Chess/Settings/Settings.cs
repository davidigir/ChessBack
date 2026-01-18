namespace Chess.Settings
{
    public class ConfigSettings
    {
        public AuthSettings Auth { get; set; }
        public Gameplay Gameplay { get; set; }

    }
    public class JwtSettings
    {
        public string Key { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public double ExpirationTime { get; set; }
    }
    public class Gameplay
    {
        public int ReconnectionTimeoutSeconds {  get; set; }
        public int InactivityDeleteTimeoutSeconds { get; set; }
        public int MinimumElo {  get; set; }
        public int KFactor { get; set; }
        public string RematchRoomName { get; set; }
        public string RematchRoomPassword {  get; set; }

        public bool Bot { get; set; }
    }
    public class AuthSettings
    {
        public string CookieName { get; set; }
        public int CookieExpirationTime { get; set; }
        public string CookiePath { get; set; }
        public bool CookieSecure { get; set; }
        public bool CookieHttpOnly { get; set; }

    }
}
