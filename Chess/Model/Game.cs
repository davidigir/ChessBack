using Chess.Enums;
using Chess.Service;
using System.Security.Cryptography.X509Certificates;

namespace Chess.Model
{
    public class Game
    {

        public Guid Id { get; set; }
        public Board Board { get; set; }
        public Player WhitePlayer { get; set; }
        public Player BlackPlayer { get; set; }

        public PieceColor CurrentTurn { get; private set; }

        public GameState CurrentGameState { get; set; }

        public GameOverReason Finish { get; set;}



        public List<string> MovesHistory { get; set; } = new List<string>();


        //we can implement smtg to add viewers to the game
        public List<string> ConnectionIds { get; set; } = new List<string>();

        public string? WhitePlayerConnectionId { get; set; }
        public string? BlackPlayerConnectionId { get; set; }

        public bool IsWhiteAssigned => !string.IsNullOrEmpty(WhitePlayerConnectionId);
        public bool IsBlackAssigned => !string.IsNullOrEmpty(BlackPlayerConnectionId);



        public Game(
            //Player whitePlayer, Player blackPlayer
            )
        {
            //this.WhitePlayer = whitePlayer;
            //this.BlackPlayer = blackPlayer;
            
            this.Board = new Board();
            this.Board.InitializeBoard();
            this.WhitePlayer = new Player(false);
            this.BlackPlayer = new Player(false);

            this.CurrentTurn = PieceColor.White;
            this.CurrentGameState = GameState.Waiting;
            this.Finish = GameOverReason.PLAYING; //by default playing

            Console.WriteLine("Chess game Init");
        }

        public bool IsFull()
        {
            return ConnectionIds.Count >= 2;
        }

        

        public bool MakeMove(string move)
        {
            if (this.CurrentGameState != GameState.Playing) return false; //
            Coordinate source = Coordinate.FromAlgebraic(move.Substring(0, 2));
            Coordinate destination = Coordinate.FromAlgebraic(move.Substring(2, 2));
            Piece pieceToMove = Board.Pieces[source.Y, source.X];
            if (pieceToMove == null || pieceToMove.PieceColor != this.CurrentTurn)
            {
                Console.WriteLine("Error. Piece selected cant play in this turn");
                return false;
            }

            if(MovementValidator.IsMoveValid(this.Board, source, destination))
            {
                this.Board.Move(move);
                this.MovesHistory.Add(move);
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


                this.CurrentTurn = oppositePlayerColor;
                return true;


            }
            else
            {
                Console.WriteLine("Movimiento Ilegal");
                return false;
            }

        }

    }
}
