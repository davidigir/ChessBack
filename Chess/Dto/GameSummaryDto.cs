namespace Chess.Dto
{
    public class GameSummaryDto
    {
        public Guid GameId { get; set; }
        public string RoomName { get; set; }
        public bool IsPrivate { get; set; }

        public string Status { get; set; }
        public int PlayerCount { get; set; }
        public string WhitePlayer { get; set; }
        public string BlackPlayer { get; set; }

    }
}
