namespace Chess.Service
{
    public static class EloCalculator
    {
        public static (int newWhiteElo, int newBlackElo) GetNewRatings (int whiteElo, int blackElo, double whiteScore, int kFactor = 30)
        {
            double expectedWhite = 1 / (1 + Math.Pow(10, (blackElo - whiteElo) / 400));
            double expectedBlack = 1 - expectedWhite;
            double blackScore = 1 - whiteScore;

            int newWhite = (int)Math.Round(whiteElo + kFactor * (whiteScore - expectedWhite));
            int newBlack = (int)Math.Round(blackElo + kFactor * (blackScore - expectedBlack));

            return (newWhite, newBlack);
        }
    }
}
