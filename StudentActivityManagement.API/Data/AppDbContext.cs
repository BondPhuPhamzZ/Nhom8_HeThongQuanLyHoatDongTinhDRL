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
        public DbSet<ActivitySession> ActivitySessions { get; set; } = null!;
        public DbSet<ActivityAudienceRule> ActivityAudienceRules { get; set; } = null!;
        public DbSet<ActivityCheckIn> ActivityCheckIns { get; set; } = null!;
        public DbSet<ActivityCheckInConfig> ActivityCheckInConfigs { get; set; } = null!;
        public DbSet<ActivityReminderOffset> ActivityReminderOffsets { get; set; } = null!;
        public DbSet<ActivityEvidence> ActivityEvidences { get; set; } = null!;
        public DbSet<EvaluationPeriod> EvaluationPeriods { get; set; } = null!;
        public DbSet<PointRecord> PointRecords { get; set; } = null!;
        public DbSet<PointRecordHistory> PointRecordHistories { get; set; } = null!;
        public DbSet<ClassThread> ClassThreads { get; set; } = null!;
        public DbSet<ClassThreadReply> ClassThreadReplies { get; set; } = null!;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // SQLite has no time zone type: store every DateTime as UTC and mark it UTC when read back.
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        }

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

                // Registrations are participation history: an activity that has any must be cancelled, not deleted.
                entity.HasOne(ar => ar.Activity)
                      .WithMany(a => a.Registrations)
                      .HasForeignKey(ar => ar.ActivityId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ar => ar.Student)
                      .WithMany(u => u.Registrations)
                      .HasForeignKey(ar => ar.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ActivitySession>(entity =>
            {
                entity.HasIndex(session => new { session.ActivityId, session.SortOrder }).IsUnique();

                entity.HasOne(session => session.Activity)
                      .WithMany(activity => activity.Sessions)
                      .HasForeignKey(session => session.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ActivityAudienceRule>(entity =>
            {
                entity.HasIndex(rule => rule.ActivityId);

                entity.HasOne(rule => rule.Activity)
                      .WithMany(activity => activity.AudienceRules)
                      .HasForeignKey(rule => rule.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rule => rule.Class)
                      .WithMany()
                      .HasForeignKey(rule => rule.ClassId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Check-ins and evidence point at sessions with Restrict: a session that has attendance
            // data cannot be removed by editing the activity (ActivityService refuses it with 409).
            modelBuilder.Entity<ActivityCheckIn>(entity =>
            {
                entity.HasIndex(checkIn => new { checkIn.RegistrationId, checkIn.SessionId }).IsUnique();
                entity.HasIndex(checkIn => checkIn.SessionId);

                entity.HasOne(checkIn => checkIn.Registration)
                      .WithMany()
                      .HasForeignKey(checkIn => checkIn.RegistrationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(checkIn => checkIn.Session)
                      .WithMany()
                      .HasForeignKey(checkIn => checkIn.SessionId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(checkIn => checkIn.RecordedBy)
                      .WithMany()
                      .HasForeignKey(checkIn => checkIn.RecordedById)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ActivityCheckInConfig>(entity =>
            {
                entity.HasOne(config => config.Activity)
                      .WithOne()
                      .HasForeignKey<ActivityCheckInConfig>(config => config.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(config => config.UpdatedBy)
                      .WithMany()
                      .HasForeignKey(config => config.UpdatedById)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ActivityReminderOffset>(entity =>
            {
                entity.HasIndex(offset => new { offset.ActivityId, offset.OffsetMinutes }).IsUnique();

                entity.HasOne(offset => offset.Activity)
                      .WithMany()
                      .HasForeignKey(offset => offset.ActivityId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ActivityEvidence>(entity =>
            {
                entity.HasIndex(evidence => evidence.RegistrationId);
                entity.HasIndex(evidence => evidence.ReviewStatus);

                entity.HasOne(evidence => evidence.Registration)
                      .WithMany()
                      .HasForeignKey(evidence => evidence.RegistrationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(evidence => evidence.Session)
                      .WithMany()
                      .HasForeignKey(evidence => evidence.SessionId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(evidence => evidence.ReviewedBy)
                      .WithMany()
                      .HasForeignKey(evidence => evidence.ReviewedById)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Score ledger (US-22): one decision per registration; records keep their history and are
            // never cascaded away by deleting an activity or period.
            modelBuilder.Entity<EvaluationPeriod>(entity =>
            {
                entity.HasIndex(period => period.Name).IsUnique();
            });

            modelBuilder.Entity<PointRecord>(entity =>
            {
                entity.HasIndex(record => record.RegistrationId).IsUnique();
                entity.HasIndex(record => new { record.StudentId, record.PeriodId });
                entity.HasIndex(record => record.ActivityId);

                entity.HasOne(record => record.Registration)
                      .WithMany()
                      .HasForeignKey(record => record.RegistrationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(record => record.Student)
                      .WithMany()
                      .HasForeignKey(record => record.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(record => record.Activity)
                      .WithMany()
                      .HasForeignKey(record => record.ActivityId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(record => record.Period)
                      .WithMany()
                      .HasForeignKey(record => record.PeriodId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(record => record.DecidedBy)
                      .WithMany()
                      .HasForeignKey(record => record.DecidedById)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Class discussion space (US-09): posts go away with their class, their thread or their author,
            // matching how deleting a student already removes the student's registrations.
            modelBuilder.Entity<ClassThread>(entity =>
            {
                entity.HasIndex(thread => new { thread.ClassId, thread.IsPinned, thread.LastActivityAt });

                entity.HasOne(thread => thread.Class)
                      .WithMany()
                      .HasForeignKey(thread => thread.ClassId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(thread => thread.Author)
                      .WithMany()
                      .HasForeignKey(thread => thread.AuthorId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ClassThreadReply>(entity =>
            {
                entity.HasIndex(reply => new { reply.ThreadId, reply.CreatedAt });

                entity.HasOne(reply => reply.Thread)
                      .WithMany(thread => thread.Replies)
                      .HasForeignKey(reply => reply.ThreadId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(reply => reply.Author)
                      .WithMany()
                      .HasForeignKey(reply => reply.AuthorId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PointRecordHistory>(entity =>
            {
                entity.HasIndex(history => history.PointRecordId);

                entity.HasOne(history => history.PointRecord)
                      .WithMany(record => record.History)
                      .HasForeignKey(history => history.PointRecordId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(history => history.ChangedBy)
                      .WithMany()
                      .HasForeignKey(history => history.ChangedById)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
