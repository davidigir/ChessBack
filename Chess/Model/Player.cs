namespace Chess.Model
{
    public class Player
    {
        public List<Piece> Pieces { get; set; }
        public bool IsMyTurn { get; set; }
        public Player(List<Piece> pieces, bool isMyTurn) {
            this.Pieces = pieces;
            this.IsMyTurn = isMyTurn;
        }
    }
}
