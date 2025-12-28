namespace Chess.Dto
{
    public class StatsDto
    {
        public int TotalGames { get; set; }
        public int TotalWins { get; set; }
        public double Winrate { get; set; }
        public int Elo { get; set; }
        public double BlackWinrate { get; set; }
        public double WhiteWinrate { get; set; }

        public int TotalDraws { get; set; }
        public int TotalLosses { get; set; }
    }
}