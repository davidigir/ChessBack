namespace Chess.Service
{
    public static class EloCalculator
    {
        public static int CalculateDelta(int whiteElo, int blackElo, double whiteScore, int kFactor = 30)
        {
            double expectedWhite = 1 / (1 + Math.Pow(10, (blackElo - whiteElo) / 400.0));

            int delta = (int)Math.Round(kFactor * (whiteScore - expectedWhite));

            return delta;
        }
    }
}