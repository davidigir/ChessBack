using Chess.Entity;
using Microsoft.EntityFrameworkCore;

namespace Chess.Db
{
    public class ChessDbContext : DbContext
    {
        public ChessDbContext(DbContextOptions<ChessDbContext> options) : base(options)
        {
        }

        public DbSet<UserEntity> Users { get; set; }
        public DbSet<GameEntity> Games { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>()
                .HasIndex(u => u.Nickname)
                .IsUnique();
            modelBuilder.Entity<GameEntity>(entity =>
            {
                entity.HasKey(g => g.Id);

                entity.HasIndex(g => g.WhitePlayerId);
                entity.HasIndex(g => g.BlackPlayerId);

            });
            modelBuilder.Entity<GameEntity>()
        .HasOne(g => g.WhitePlayer)
        .WithMany()
        .HasForeignKey(g => g.WhitePlayerId)
        .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GameEntity>()
                .HasOne(g => g.BlackPlayer)
                .WithMany()
                .HasForeignKey(g => g.BlackPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}