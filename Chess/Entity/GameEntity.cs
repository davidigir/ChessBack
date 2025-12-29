using System.ComponentModel.DataAnnotations.Schema;

namespace Chess.Entity
{
    public class GameEntity
    {
        public Guid Id { get; set; }

        public int WhitePlayerId { get; set; }

        [ForeignKey("WhitePlayerId")]
        public UserEntity? WhitePlayer { get; set; }
        public int WhiteEloBefore { get; set; }
        public int WhiteEloAfter { get; set; }


        public int BlackPlayerId { get; set; }

        [ForeignKey("BlackPlayerId")]
        public UserEntity? BlackPlayer { get; set; }
        public int BlackEloBefore { get; set; }
        public int BlackEloAfter { get;set; }
        public int EloChange { get; set; }
        public int TotalMovements { get; set; }

        public string PgnHistory { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty; 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    }
}