namespace Chess.Model
{
    public class Player
    {
        public bool Ready { get; set; }


        public Player(bool ready) {
            this.Ready = ready;
        }
    }
}
