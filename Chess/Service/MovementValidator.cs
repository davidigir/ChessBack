using Chess.Enums;
using Chess.Model;
using System.Net.NetworkInformation;

namespace Chess.Service
{
    public class MovementValidator
    {

        public static bool IsMoveValid(Board board, Coordinate source, Coordinate destination)
        {

            if (source.X == destination.X && source.Y == destination.Y) return false;

            Piece pieceToMove = board.Pieces[source.Y, source.X];
            Piece targetPiece = board.Pieces[destination.Y, destination.X] ?? Piece.NonePiece;

            if (pieceToMove == null || !pieceToMove.IsPiece()) return false;

            if (pieceToMove.PieceColor == targetPiece.PieceColor && targetPiece.IsPiece()) return false; //there is already a friendly piece in that position


            //specific validation
            bool isMoveValid = pieceToMove.PieceType switch
            {
                Enums.PieceType.Pawn => IsPawnMoveValid(board, source, destination),
                Enums.PieceType.Rook => IsRookMoveValid(board, source, destination),
                Enums.PieceType.Queen => IsQueenMoveValid(board, source, destination),
                Enums.PieceType.Knight => IsKnightMoveValid(board, source, destination),
                Enums.PieceType.Bishop => IsBishopMoveValid(board, source, destination),
                Enums.PieceType.King => IsKingMoveValid(board, source, destination),



                _ => false, //by default invalid move


            };



            if (isMoveValid)
            {
                Board _tempBoard = board.Clone();

                Piece pieceSelected = _tempBoard.Pieces[source.Y, source.X];

                _tempBoard.Pieces[source.Y, source.X] = Piece.NonePiece;

                _tempBoard.Pieces[destination.Y, destination.X] = pieceSelected;
                //_tempBoard.DisplayBoard();

                bool isMyKingExposed = IsTheKingExposed(_tempBoard, pieceSelected.PieceColor);

                if (isMyKingExposed) Console.WriteLine("The king is under attack with that move");

                return (!isMyKingExposed);



            }
            //bool isTheEnemyKingUnderAttack = IsTheEnemyKingUnderAttack(board, pieceToMove.PieceColor);
            //if (isTheEnemyKingUnderAttack) Console.WriteLine("Has puesto en jaque al enemigo");

            return isMoveValid;



        }

        public static bool IsCheckStaleMate(Board board, PieceColor color)
        {
            for (int sourceY = 0; sourceY < 8; sourceY++)
            {
                for (int sourceX = 0; sourceX < 8; sourceX++)
                {
                    Coordinate source = new Coordinate(sourceX, sourceY);
                    Piece piece = board.Pieces[sourceY, sourceX];
                    //Only for the color of the king in check
                    if (piece != null && piece.IsPiece() && piece.PieceColor == color)
                    {
                        //we need to move the piece in a square that saves the king
                        for (int destY = 0; destY < 8; destY++)
                        {
                            for (int destX = 0; destX < 8; destX++)
                            {
                                Coordinate destination = new Coordinate(destX, destY);
                                if (IsMoveValid(board, source, destination))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }

            }
            return IsTheKingExposed(board, color);

        }
        public static PieceColor GetOppositeColor(PieceColor color)
        {
            if (color == Enums.PieceColor.White)
            {
                return Enums.PieceColor.Black;
            }
            else if (color == Enums.PieceColor.Black)
            {
                return Enums.PieceColor.White;
            }
            return Enums.PieceColor.White;
        }


        public static bool IsTheEnemyKingUnderAttack(Board board, PieceColor friendlyColor)
        {
            PieceColor oppositeColor = GetOppositeColor(friendlyColor);
            //if is the opposite king exposed means a check
            
            return IsTheKingExposed(board, oppositeColor);

        }
        public static bool IsAttackingMoveValid(Board board, Coordinate source, Coordinate destination)
        {

            if (source.X == destination.X && source.Y == destination.Y) return false;

            Piece pieceToMove = board.Pieces[source.Y, source.X];
            Piece targetPiece = board.Pieces[destination.Y, destination.X] ?? Piece.NonePiece;

            if (pieceToMove == null || !pieceToMove.IsPiece()) return false;

            if (pieceToMove.PieceColor == targetPiece.PieceColor && targetPiece.IsPiece()) return false; //there is already a friendly piece in that position



            //specific validation
            bool isMoveValid = pieceToMove.PieceType switch
            {
                Enums.PieceType.Pawn => IsPawnMoveValid(board, source, destination),
                Enums.PieceType.Rook => IsRookMoveValid(board, source, destination),
                Enums.PieceType.Queen => IsQueenMoveValid(board, source, destination),
                Enums.PieceType.Knight => IsKnightMoveValid(board, source, destination),
                Enums.PieceType.Bishop => IsBishopMoveValid(board, source, destination),
                Enums.PieceType.King => IsKingMoveValid(board, source, destination),



                _ => false, //by default invalid move


            };


            //bool isSquareUnderAttack = IsSquareUnderAttack(board, destination, opositePieceColor);

            return isMoveValid;



        }

        public static bool IsTheKingExposed(Board board, PieceColor kingColor)
        {
            for (int y = 0; y < 8; y++)
            {

                for (int x = 0; x < 8; x++)
                {

                    Piece possibleKing = board.Pieces[y, x];

                    if(possibleKing.PieceType == PieceType.King && possibleKing.PieceColor == kingColor)
                    {
                        Coordinate kingPosition = new Coordinate(x, y);
                        PieceColor oppositeColor = GetOppositeColor(kingColor);
                        bool isSquareUnderAttack = IsSquareUnderAttack(board, kingPosition, oppositeColor);

                        return isSquareUnderAttack;


                    }



                }
            }

                    return false;
        }


        public static bool IsPawnMoveValid(Board board, Coordinate source, Coordinate destination)
        {

            Piece pawn = board.Pieces[source.Y, source.X] ?? Piece.NonePiece;
            Piece targetPiece = board.Pieces[destination.Y, destination.X] ?? Piece.NonePiece;

            int forwardDirection = (pawn.PieceColor == Enums.PieceColor.White) ? -1 : 1;
            int deltaY = destination.Y - source.Y;
            int deltaX = destination.X - source.X;

            //if deltaY == -1 and forwardDirection also -1 == 1 this mean that the black pawn move is legal
            if (
                Math.Abs(deltaX) == 1 && deltaY * forwardDirection == 1
                ) //meaning a capture
            {
                if (targetPiece.IsPiece() && targetPiece.PieceColor != pawn.PieceColor) return true;

                return false;
            }

            if (deltaX == 0) //move valid if X its the same
            {
                if (targetPiece.IsPiece()) return false; //pawn cant capture in this way
                if (deltaY * forwardDirection == 2 && !pawn.HasMoved)
                {
                    //the move is valid if its the first move of the pawn
                    //we should also check if there is no piece in forwardDirection ==1
                    Piece isEmpty = board.Pieces[(source.Y + forwardDirection), source.X] ?? Piece.NonePiece;

                    if (isEmpty.IsPiece()) return false;
                    return true;



                }
                if (deltaY * forwardDirection == 1) return true;
            }


            return false;
        }

        public static bool IsKingMoveValid(Board board, Coordinate source, Coordinate destination)
        {
            Piece king = board.Pieces[source.Y, source.X] ?? Piece.NonePiece;
            Piece targetPiece = board.Pieces[destination.Y, destination.X] ?? Piece.NonePiece;


            int deltaY = destination.Y - source.Y;
            int deltaX = destination.X - source.X;

            if (Math.Abs(deltaX) <= 1 && Math.Abs(deltaY) <= 1)
            {


                return true;

            }


            return false;

        }


        public static bool IsRookMoveValid(Board board, Coordinate source, Coordinate destination)
        {
            Piece rook = board.Pieces[source.Y, source.X] ?? Piece.NonePiece;
            Piece targetPiece = board.Pieces[destination.Y, destination.X] ?? Piece.NonePiece;


            int deltaY = destination.Y - source.Y;
            int deltaX = destination.X - source.X;


            if (deltaX != 0 && deltaY == 0)
            {//Left right

                int direction = Math.Sign(deltaX);

                for (int i = 1; i < Math.Abs(deltaX); i++)
                {

                    Piece isEmpty = board.Pieces[(source.Y), (source.X + i * direction)] ?? Piece.NonePiece;

                    if (isEmpty.IsPiece()) return false;

                }

                return true;

            }
            if (deltaX == 0 && deltaY != 0)//UpDown
            {
                int direction = Math.Sign(deltaY);


                for (int i = 1; i < Math.Abs(deltaY); i++)
                {

                    Piece isEmpty = board.Pieces[(source.Y + i * direction), (source.X)] ?? Piece.NonePiece;

                    if (isEmpty.IsPiece()) return false;

                }

                return true;


            }

            return false;
        }

        public static bool IsKnightMoveValid(Board board, Coordinate source, Coordinate destination)
        {
            Piece knight = board.Pieces[source.Y, source.X] ?? Piece.NonePiece;



            int deltaY = destination.Y - source.Y;
            int deltaX = destination.X - source.X;

            int absDeltaY = Math.Abs(deltaY);
            int absDeltaX = Math.Abs(deltaX);

            if ((absDeltaX == 2 && absDeltaY == 1) || (absDeltaX == 1 && absDeltaY == 2))
            {
                return true;
            }

            return false;
        }

        public static bool IsBishopMoveValid(Board board, Coordinate source, Coordinate destination)
        {
            Piece bishop = board.Pieces[source.Y, source.X] ?? Piece.NonePiece;

            int deltaY = destination.Y - source.Y;
            int deltaX = destination.X - source.X;

            if (Math.Abs(deltaY) == Math.Abs(deltaX))
            {
                int directionX = Math.Sign(deltaX);
                int directionY = Math.Sign(deltaY);

                int distance = Math.Abs(deltaX);

                for (int i = 1; i < distance; i++)
                {
                    int checkY = source.Y + i * directionY;
                    int checkX = source.X + i * directionX;

                    Piece intermediatePiece = board.Pieces[checkY, checkX] ?? Piece.NonePiece;

                    if (intermediatePiece.IsPiece())
                    {
                        return false; // Bloqueado
                    }
                }

                return true;
            }

            // No es un movimiento diagonal
            return false;
        }


        public static bool IsQueenMoveValid(Board board, Coordinate source, Coordinate destination)
        {

            int deltaY = destination.Y - source.Y;
            int deltaX = destination.X - source.X;

            int directionX = Math.Sign(deltaX);
            int directionY = Math.Sign(deltaY);

            if (Math.Abs(deltaY) == Math.Abs(deltaX))
            {
                int distance = Math.Abs(deltaX);

                for (int i = 1; i < distance; i++)
                {
                    Piece intermediatePiece = board.Pieces[(source.Y + i * directionY), (source.X + i * directionX)] ?? Piece.NonePiece;
                    if (intermediatePiece.IsPiece()) return false;
                }

                return true;
            }

            if (deltaX != 0 && deltaY == 0)
            {
                int direction = Math.Sign(deltaX);
                int distance = Math.Abs(deltaX);

                for (int i = 1; i < distance; i++)
                {
                    Piece intermediatePiece = board.Pieces[(source.Y), (source.X + i * direction)] ?? Piece.NonePiece;
                    if (intermediatePiece.IsPiece()) return false;
                }

                return true;
            }

            if (deltaX == 0 && deltaY != 0) //vertical move
            {
                int direction = Math.Sign(deltaY);
                int distance = Math.Abs(deltaY);

                for (int i = 1; i < distance; i++)
                {
                    Piece intermediatePiece = board.Pieces[(source.Y + i * direction), (source.X)] ?? Piece.NonePiece;
                    if (intermediatePiece.IsPiece()) return false; // Bloqueado
                }

                return true;
            }

            return false;
        }

        public static bool IsSquareUnderAttack(Board board, Coordinate targetSquare, PieceColor attackingColor)
        {

            for (int y = 0; y < 8; y++)
            {

                for (int x = 0; x < 8; x++)
                {
                    Coordinate source = new Coordinate(x, y);
                    Piece attacker = board.Pieces[y, x];

                    if (attacker != null && attacker.IsPiece() && attacker.PieceColor == attackingColor)
                    {
                        if (IsAttackingMoveValid(board, source, targetSquare)) return true;

                    }

                }

            }

            return false;
        }

    }
}
