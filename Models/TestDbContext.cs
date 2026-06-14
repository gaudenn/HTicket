
using Microsoft.EntityFrameworkCore;
using HTicket.Models;

namespace HTicket.Models
{

    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options)
            : base(options)
        { }

        public DbSet<Event> Events { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Event)
                .WithMany(e => e.Tickets)
                .HasForeignKey(t => t.EventId);
        }
    }
}