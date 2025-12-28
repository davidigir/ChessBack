namespace Chess.Dto
{
    public class GameStatusDto
    {
        public string White { get; set; }
        public bool WhiteIsReady { get; set; }
        public bool WhitePlayerOnline { get; set; }
        public int WhitePlayerElo { get; set; }
        public int BlackPlayerElo { get; set; }
        public string Black { get; set; }
        public bool BlackIsReady { get; set; }
        public bool BlackPlayerOnline { get; set; }
        public string Status { get; set; }
        public string RoomName { get; set; }
    }
}
