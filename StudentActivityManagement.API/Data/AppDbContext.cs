using Microsoft.EntityFrameworkCore;
using StudentActivityManagement.API.Models;

namespace StudentActivityManagement.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Class> Classes { get; set; } = null!;
        public DbSet<Activity> Activities { get; set; } = null!;
        public DbSet<ActivityRegistration> ActivityRegistrations { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.StudentCode).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();

                entity.HasOne(u => u.Class)
                      .WithMany(c => c.Students)
                      .HasForeignKey(u => u.ClassId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Class configuration
            modelBuilder.Entity<Class>(entity =>
            {
                entity.HasIndex(c => c.ClassCode).IsUnique();

                entity.HasOne(c => c.MonitorStudent)
                      .WithMany()
                      .HasForeignKey(c => c.MonitorStudentId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ActivityRegistration configuration
            modelBuilder.Entity<ActivityRegistration>(entity =>
            {
                entity.HasIndex(ar => new { ar.ActivityId, ar.StudentId }).IsUnique();

                entity.HasOne(ar => ar.Activity)
                      .WithMany(a => a.Registrations)
                      .HasForeignKey(ar => ar.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ar => ar.Student)
                      .WithMany(u => u.Registrations)
                      .HasForeignKey(ar => ar.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
