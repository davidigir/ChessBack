namespace Chess.Model
{
    public struct Coordinate
    {
        public int X { get; set; }
        public int Y { get; set; }

        public Coordinate(int x, int y)
        {
            X = x;
            Y = y;
        }
        public static Coordinate FromAlgebraic(string algebraic)
        {
            if (string.IsNullOrWhiteSpace(algebraic) || algebraic.Length < 2)
            {
                throw new ArgumentException("La notación debe ser 'letra-número' (ej. 'a1').");
            }

            char fileChar = algebraic[0]; 
            char rankChar = algebraic[1];

            int file_X = char.ToLower(fileChar) - 'a';

            int rank_Y = 8 - (int)char.GetNumericValue(rankChar);


            return new Coordinate(file_X, rank_Y);
        }
    }
}