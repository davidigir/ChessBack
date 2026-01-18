using Chess.Enums;

namespace Chess.Model
{
    public class Player
    {
        public bool IsReady { get; set; } = false;
        public string Nickname { get; set; }
        public int Id { get; set; }

        public PieceColor Color { get; set; }

        public string ConnectionId { get; set; }

        public bool IsConnected { get; set; } = true;

        public int Elo { get; set; }
        public bool IsBot { get; set; } = false;


        public Player(string nickname, int id, int elo, PieceColor color) {
            this.Nickname = nickname;
            this.Id = id;
            this.Elo = elo;
            this.Color = color;
        }
    }
}
