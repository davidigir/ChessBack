using Chess.Enums;
using Chess.Service;
using System.Drawing;

namespace Chess.Model
{
    public class Board
    {
        public int Id { get; set; }
        public Piece[,] Pieces { get; set; } = new Piece[8, 8];



        public Board(
            )
        {


        }

        public void InitializeBoard()
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    Pieces[r, c] = Piece.NonePiece;
                }
            }


            for (int r = 0; r < 8; r += 7)
            {
                PieceColor color = (r == 0) ? PieceColor.Black : PieceColor.White;

                int pawnRank = (r == 0) ? 1 : 6;

                Pieces[r, 0] = new Piece(color, PieceType.Rook);
                Pieces[r, 1] = new Piece(color, PieceType.Knight);
                Pieces[r, 2] = new Piece(color, PieceType.Bishop);
                Pieces[r, 3] = new Piece(color, PieceType.Queen);
                Pieces[r, 4] = new Piece(color, PieceType.King);
                Pieces[r, 5] = new Piece(color, PieceType.Bishop);
                Pieces[r, 6] = new Piece(color, PieceType.Knight);
                Pieces[r, 7] = new Piece(color, PieceType.Rook);

                for (int c = 0; c < 8; c++)
                {
                    Pieces[pawnRank, c] = new Piece(color, PieceType.Pawn);
                }
            }

        }

        public void DisplayBoard()
        {
            Console.WriteLine("-----------------------------------------");
            for (int rank = 0; rank < 8; rank++)
            {
                Console.Write($" {8 - rank} |");

                for (int file = 0; file < 8; file++)
                {
                    Piece piece = Pieces[rank, file];

                    char pieceChar = (piece != null && piece.IsPiece()) ? piece.GetFenChar() : '.';

                    Console.Write($" {pieceChar} ");
                }
                Console.WriteLine("|"); // Salto de línea después de cada fila
            }

            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("   | A  B  C  D  E  F  G  H |"); // Imprime las etiquetas de columna
            Console.WriteLine();
        }

        public void Move(string move)
        {
            string pos1 = move.Substring(0, 2);
            string pos2 = move.Substring(2, 2);

            Coordinate coord1 = Coordinate.FromAlgebraic(pos1);
            Coordinate coord2 = Coordinate.FromAlgebraic(pos2);

            Console.WriteLine($"Movimiento recibido: {move}");
            Console.WriteLine($"Origen: (X: {coord1.X}, Y: {coord1.Y}) | Destino: (X: {coord2.X}, Y: {coord2.Y})");

            Piece pieceSelected = Pieces[coord1.Y, coord1.X];





                    pieceSelected.HasMoved = true;
                

                Pieces[coord2.Y, coord2.X] = pieceSelected;
                Pieces[coord1.Y, coord1.X] = Piece.NonePiece;




            
        }


        public string GetFenPlacement()
        {
            var fenBuilder = new System.Text.StringBuilder();

            for (int rank = 0; rank < 8; rank++)
            {
                int emptyCount = 0;

                for (int file = 0; file < 8; file++)
                {
                    Piece piece = Pieces[rank, file];

                    if (piece != null && piece.IsPiece())
                    {
                        if (emptyCount > 0)
                        {
                            fenBuilder.Append(emptyCount);
                            emptyCount = 0; 
                        }

                        fenBuilder.Append(piece.GetFenChar());
                    }
                    else
                    {
                        emptyCount++;
                    }
                }

                if (emptyCount > 0)
                {
                    fenBuilder.Append(emptyCount);
                }

                if (rank < 7)
                {
                    fenBuilder.Append('/');
                }
            }

            Console.WriteLine(fenBuilder.ToString());

            return fenBuilder.ToString();
        }

        public Board Clone()
        {
            Board newBoard = new Board();

            for(int y = 0; y< 8; y++)
            {
                for(int x = 0; x< 8; x++)
                {
                    Piece piece = this.Pieces[y, x];
                    if(piece != null && piece.IsPiece())
                    {
                        newBoard.Pieces[y, x] = piece.Clone();
                    }
                    else
                    {
                        newBoard.Pieces[y, x] = Piece.NonePiece;
                    }
                }
            }

            return newBoard;
        }
    }
}
