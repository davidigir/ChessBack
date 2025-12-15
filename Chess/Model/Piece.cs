using Chess.Enums;

namespace Chess.Model
{
    public class Piece
    {
        public PieceColor PieceColor { get; set; }

        public PieceType PieceType { get; set; }

        public bool HasMoved { get; set; } = false;

        private static readonly Piece _nonePiece = new Piece(PieceColor.White, PieceType.None);

        public static Piece NonePiece => _nonePiece;


        public Piece(PieceColor pieceColor, PieceType pieceType)
        {
            this.PieceColor = pieceColor;
            this.PieceType = pieceType;
        }

        public bool IsPiece() => this.PieceType != PieceType.None;


        public char GetFenChar()
        {
            if (this.PieceType == PieceType.None)
            {
                return ' ';
            }

            char fenChar = this.PieceType switch
            {
                PieceType.King => 'K',
                PieceType.Queen => 'Q',
                PieceType.Rook => 'R',
                PieceType.Bishop => 'B',
                PieceType.Knight => 'N',
                PieceType.Pawn => 'P',
                _ => '?'
            };

            return (PieceColor == PieceColor.Black) ? char.ToLower(fenChar) : fenChar;
        }

        public Piece Clone()
        {
            if (this == Piece.NonePiece)
            {
                return Piece.NonePiece;
            }

            Piece newPiece = new Piece(this.PieceColor, this.PieceType)
            {
                HasMoved = this.HasMoved
            };

            return newPiece;
        }
    }
}