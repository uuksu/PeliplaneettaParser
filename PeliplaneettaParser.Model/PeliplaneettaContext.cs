namespace PeliplaneettaParser.Model
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    public partial class PeliplaneettaContext : DbContext
    {
        public PeliplaneettaContext()
            : base("name=PeliplaneettaContext")
        {
        }

        public virtual DbSet<Avatar> Avatar { get; set; }
        public virtual DbSet<Message> Message { get; set; }
        public virtual DbSet<Role> Role { get; set; }
        public virtual DbSet<Space> Space { get; set; }
        public virtual DbSet<Thread> Thread { get; set; }
        public virtual DbSet<User> User { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Avatar>()
                .Property(e => e.Filename)
                .IsUnicode(false);

            modelBuilder.Entity<Message>()
                .Property(e => e.Text)
                .IsUnicode(false);

            modelBuilder.Entity<Role>()
                .Property(e => e.Name)
                .IsUnicode(false);

            modelBuilder.Entity<Space>()
                .Property(e => e.Title)
                .IsUnicode(false);

            modelBuilder.Entity<Space>()
                .Property(e => e.Description)
                .IsUnicode(false);

            modelBuilder.Entity<Thread>()
                .Property(e => e.Title)
                .IsUnicode(false);

            modelBuilder.Entity<User>()
                .Property(e => e.Nickname)
                .IsUnicode(false);

            modelBuilder.Entity<User>()
                .Property(e => e.Signature)
                .IsUnicode(false);
        }
    }
}
