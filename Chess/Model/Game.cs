using Chess.Enums;
using Chess.Service;
using System.Security.Cryptography.X509Certificates;

namespace Chess.Model
{
    public class Game
    {

        public Guid Id { get; set; }

        public string? PasswordHash { get; set; }

        public bool IsPrivate => !string.IsNullOrEmpty(PasswordHash);

        public string? RoomName { get; set; }


        public Board Board { get; set; }
        public Player? WhitePlayer { get; set; }
        public Player? BlackPlayer { get; set; }
        public int EloChange { get; set; }
        public int WhiteEloBefore { get; set; }
        public int WhiteEloAfter { get; set; }
        public int BlackEloBefore { get; set; }
        public int BlackEloAfter { get; set; }

        public PieceColor CurrentTurn { get; private set; }

        public GameState CurrentGameState { get; set; }

        public GameOverReason Finish { get; set;}



        public List<string> MovesHistory { get; set; } = new List<string>();
        public string LastMove { get; set; } = "";

        public System.Threading.Timer? CleanTimer { get; set; }


        //we can implement smtg to add viewers to the game


        public Game(
            //Player whitePlayer, Player blackPlayer
            )
        {
            //this.WhitePlayer = whitePlayer;
            //this.BlackPlayer = blackPlayer;
            
            this.Board = new Board();
            this.Board.InitializeBoard();
            //this.Board.Pieces[0, 0] = new Piece(PieceColor.White, PieceType.King);
            //this.Board.Pieces[7, 7] = new Piece(PieceColor.Black, PieceType.King);
            //this.Board.Pieces[3, 4] = new Piece(PieceColor.Black, PieceType.Queen);


            this.CurrentTurn = PieceColor.White;
            this.CurrentGameState = GameState.Waiting;
            this.Finish = GameOverReason.PLAYING; //by default playing

            Console.WriteLine("Chess game Init");
        }

        


        public void SetPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return;
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            PasswordHash = Convert.ToBase64String(bytes);
        }

        public bool CheckPassword(string password)
        {
            if (!IsPrivate) return true;
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return PasswordHash == Convert.ToBase64String(bytes);
        }

        public string PromotePiece(PieceType pieceType)
        {
            if (this.CurrentGameState != GameState.Promoting) return "";
            string lastMove = this.LastMove;
            if (lastMove == "") return "";
            Coordinate destination = Coordinate.FromAlgebraic(lastMove.Substring(2, 2));
            Piece pieceToPromote = Board.Pieces[destination.Y, destination.X];
            if (pieceToPromote.PieceType != PieceType.Pawn) return "";
            this.Board.Promote(pieceToPromote, pieceType);
            lastMove = lastMove + pieceType.ToString().Substring(0, 1);

            this.MovesHistory.Add(lastMove);

            this.CurrentGameState = GameState.Playing;


            PieceColor oppositePlayerColor = MovementValidator.GetOppositeColor(this.CurrentTurn);

            bool isCheckStaleMate = MovementValidator.IsCheckStaleMate(this.Board, oppositePlayerColor);
            bool isCheck = MovementValidator.IsTheEnemyKingUnderAttack(this.Board, this.CurrentTurn);

            if (isCheckStaleMate && isCheck)
            {
                //checkmate
                Console.WriteLine($"{oppositePlayerColor} Lose by checkmate");
                if (oppositePlayerColor == PieceColor.Black) this.Finish = GameOverReason.WHITE_WINS;
                else this.Finish = GameOverReason.BLACK_WINS;
                this.CurrentGameState = GameState.Finished;


            }
            if (isCheckStaleMate && !isCheck)
            {
                //stalemate
                Console.WriteLine("This is a Draw");
                this.Finish = GameOverReason.STALEMATE;
                this.CurrentGameState = GameState.Finished;

            }
            if (!isCheckStaleMate && isCheck)
            {
                //check
                Console.WriteLine($"{oppositePlayerColor} is under check");

            }
            this.CurrentTurn = oppositePlayerColor;


            return lastMove;

        }

        public bool MakeMove(string move)
        {
            if (this.CurrentGameState != GameState.Playing) return false; //
            Coordinate source = Coordinate.FromAlgebraic(move.Substring(0, 2));
            Coordinate destination = Coordinate.FromAlgebraic(move.Substring(2, 2));
            Piece pieceToMove = Board.Pieces[source.Y, source.X];
            if (pieceToMove == null || pieceToMove.PieceColor != this.CurrentTurn)
            {
                return false;
            }

            if(MovementValidator.IsMoveValid(this.Board, source, destination, this.LastMove))
            {
                if (Board.Pieces[source.Y, source.X].PieceType == PieceType.King && Math.Abs(destination.X - source.X) == 2)
                {
                    //this is a castle
                    string castleType = this.Board.PerformCastle(move);
                    this.MovesHistory.Add(castleType);
                }

                else if (Board.Pieces[source.Y, source.X].PieceType == PieceType.Pawn &&
                   MovementValidator.IsThePawnInPromotingMode(this.Board, source, destination))
                {
                    //Promoting state
                    this.CurrentGameState = GameState.Promoting;
                    //this.PendingPromotionMove = move;
                    this.Board.Move(move);

                    this.LastMove = move;
                    return true;

                }
                else if ((Board.Pieces[source.Y, source.X].PieceType == PieceType.Pawn && MovementValidator.IsPassantMoveValid(this.Board, source, destination, this.LastMove)))
                {
                    this.Board.PerformPassantMove(move, this.LastMove);
                    this.MovesHistory.Add(move + "e.p.");

                }
                else
                {
                    // Normal Move
                    this.Board.Move(move);
                    this.MovesHistory.Add(move);
                }
                this.LastMove = move;


                //now we should verify if the opposite player is in checkmate or stalemate
                PieceColor oppositePlayerColor = MovementValidator.GetOppositeColor(this.CurrentTurn);

                bool isCheckStaleMate = MovementValidator.IsCheckStaleMate(this.Board, oppositePlayerColor);
                bool isCheck = MovementValidator.IsTheEnemyKingUnderAttack(this.Board, this.CurrentTurn);

                if(isCheckStaleMate && isCheck)
                {
                    //checkmate
                    Console.WriteLine($"{oppositePlayerColor} Lose by checkmate");
                    if (oppositePlayerColor == PieceColor.Black) this.Finish = GameOverReason.WHITE_WINS;
                    else this.Finish = GameOverReason.BLACK_WINS;
                    this.CurrentGameState = GameState.Finished;


                }
                if (isCheckStaleMate && !isCheck)
                {
                    //stalemate
                    Console.WriteLine("This is a Draw");
                    this.Finish = GameOverReason.STALEMATE;
                    this.CurrentGameState = GameState.Finished;

                }

                if (!isCheckStaleMate && isCheck)
                {
                    //check
                    Console.WriteLine($"{oppositePlayerColor} is under check");

                }
                if (isInsufficentMaterial())
                {
                    this.Finish = GameOverReason.INSUFFICIENT_MATERIAL;
                    this.CurrentGameState = GameState.Finished;
                }


                this.CurrentTurn = oppositePlayerColor;
                return true;


            }
            else
            {
                return false;
            }

        }

        private bool isInsufficentMaterial()
        {
            var whitePieces = new List<(Piece Piece, int Row, int Col)>();
            var blackPieces = new List<(Piece Piece, int Row, int Col)>();

            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = Board.Pieces[r, c];
                    if (p == null) continue;
                    if (p.PieceType == PieceType.Pawn || p.PieceType == PieceType.Rook || p.PieceType == PieceType.Queen)
                        return false;
                }
            }
            int whiteCount = whitePieces.Count;
            int blackCount = blackPieces.Count;
            //totalPieces Without King
            int totalPieces = whiteCount + blackCount;

            //king vs king
            if (totalPieces == 0) return true;
            if (totalPieces == 1)
            {
                var piece = whiteCount > 0 ? whitePieces[0] : blackPieces[0];
                //king vs bishop or knight
                return piece.Piece.PieceType == PieceType.Bishop || piece.Piece.PieceType == PieceType.Knight;                
            }
            if (whiteCount == 1 && blackCount == 1)
            {
                //bishop vs bishop
                var whiteP = whitePieces[0];
                var blackP = blackPieces[0];
                if (whiteP.Piece.PieceType == PieceType.Bishop && blackP.Piece.PieceType == PieceType.Bishop)
                {
                    bool whiteBishopIsWhiteSquare = (whiteP.Row + whiteP.Col) % 2 == 0;
                    bool blackBishopIsWhiteSquare = (blackP.Row + blackP.Col) % 2 == 0;
                    //only stalemate if they are in the same square color
                    return whiteBishopIsWhiteSquare == blackBishopIsWhiteSquare;

                }
            }
            return false;
            
        }
        

    }
}
 