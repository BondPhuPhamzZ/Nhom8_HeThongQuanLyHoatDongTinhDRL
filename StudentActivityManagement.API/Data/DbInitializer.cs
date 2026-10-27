using StudentActivityManagement.API.Models;
using StudentActivityManagement.API.Services;

namespace StudentActivityManagement.API.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // Check if already seeded
            if (context.Users.Any())
            {
                return; // DB has been seeded
            }

            // Seed Classes
            var class1 = new Class
            {
                ClassCode = "21DTH1",
                ClassName = "Công nghệ thông tin 1 - K21",
                Department = "Khoa Công nghệ thông tin"
            };
            var class2 = new Class
            {
                ClassCode = "22DTH2",
                ClassName = "Công nghệ thông tin 2 - K22",
                Department = "Khoa Công nghệ thông tin"
            };

            context.Classes.AddRange(class1, class2);
            context.SaveChanges();

            // Seed System Admin User
            var adminUser = new User
            {
                StudentCode = "ADMIN001",
                FullName = "Quản trị hệ thống",
                Email = "admin@school.edu.vn",
                PasswordHash = PasswordHasher.HashPassword("Admin123@"),
                Phone = "0901234567",
                Role = Role.SystemAdmin,
                Campus = "Cơ sở 1",
                AcademicYear = "Staff",
                AccumulatedPoints = 0
            };

            // Seed Monitor Student
            var monitorUser = new User
            {
                StudentCode = "SV210001",
                FullName = "Nguyễn Văn Lớp Trưởng",
                Email = "loptruong@student.edu.vn",
                PasswordHash = PasswordHasher.HashPassword("123456"),
                Phone = "0987654321",
                Role = Role.Monitor,
                Campus = "Cơ sở 1",
                AcademicYear = "K21",
                ClassId = class1.Id,
                IsClassMonitor = true,
                AccumulatedPoints = 25
            };

            // Seed Normal Student
            var studentUser = new User
            {
                StudentCode = "SV210002",
                FullName = "Trần Thị Sinh Viên",
                Email = "sinhvien@student.edu.vn",
                PasswordHash = PasswordHasher.HashPassword("123456"),
                Phone = "0912345678",
                Role = Role.Student,
                Campus = "Cơ sở 1",
                AcademicYear = "K21",
                ClassId = class1.Id,
                IsClassMonitor = false,
                AccumulatedPoints = 15
            };

            context.Users.AddRange(adminUser, monitorUser, studentUser);
            context.SaveChanges();

            // Update Class Monitor Foreign Key
            class1.MonitorStudentId = monitorUser.Id;
            context.SaveChanges();

            // Seed Activities
            var now = DateTime.UtcNow;
            var activities = new List<Activity>
            {
                new Activity
                {
                    ActivityName = "Hội thảo Định hướng Nghề nghiệp CNTT 2026",
                    Description = "Hội thảo chia sẻ kinh nghiệm xin việc, phỏng vấn và xu hướng công nghệ AI/Cloud dành cho sinh viên.",
                    OrganizerName = "Khoa Công nghệ thông tin",
                    Location = "Hội trường A - Cơ sở 1",
                    Campus = "Cơ sở 1",
                    StartTime = now.AddDays(3),
                    EndTime = now.AddDays(3).AddHours(4),
                    RegistrationOpenTime = now.AddDays(-2),
                    RegistrationCloseTime = now.AddDays(2),
                    MaxParticipants = 100,
                    CurrentParticipantsCount = 1,
                    TrainingPoints = 10,
                    TargetAudience = "Tất cả sinh viên CNTT",
                    Status = "Published",
                    CreatedAt = now,
                    Sessions =
                    [
                        new ActivitySession
                        {
                            SessionName = "Hội thảo chính",
                            Campus = "Cơ sở 1",
                            Location = "Hội trường A - Cơ sở 1",
                            StartTime = now.AddDays(3),
                            EndTime = now.AddDays(3).AddHours(4),
                            SortOrder = 1
                        }
                    ]
                },
                new Activity
                {
                    ActivityName = "Ngày hội Hiến máu Nhân đạo - Giọt Hồng Trẻ",
                    Description = "Chương trình hiến máu tình nguyện đợt 1 năm học 2026.",
                    OrganizerName = "Đoàn Thanh niên - Hội Sinh viên",
                    Location = "Sảnh Nhà B - Cơ sở 2",
                    Campus = "Cơ sở 2",
                    StartTime = now.AddDays(7),
                    EndTime = now.AddDays(7).AddHours(8),
                    RegistrationOpenTime = now.AddDays(-1),
                    RegistrationCloseTime = now.AddDays(6),
                    MaxParticipants = 200,
                    CurrentParticipantsCount = 0,
                    TrainingPoints = 15,
                    TargetAudience = "Tất cả sinh viên",
                    Status = "Published",
                    CreatedAt = now,
                    Sessions =
                    [
                        new ActivitySession
                        {
                            SessionName = "Ca sáng",
                            Campus = "Cơ sở 2",
                            Location = "Sảnh Nhà B - Cơ sở 2",
                            StartTime = now.AddDays(7),
                            EndTime = now.AddDays(7).AddHours(4),
                            SortOrder = 1
                        },
                        new ActivitySession
                        {
                            SessionName = "Ca chiều",
                            Campus = "Cơ sở 2",
                            Location = "Sảnh Nhà B - Cơ sở 2",
                            StartTime = now.AddDays(7).AddHours(4),
                            EndTime = now.AddDays(7).AddHours(8),
                            SortOrder = 2
                        }
                    ]
                },
                new Activity
                {
                    ActivityName = "Giải Bóng đá Sinh viên Khóa 2021-2025",
                    Description = "Giải đấu giao lưu thể thao giữa các lớp khóa K21.",
                    OrganizerName = "Trung tâm Giáo dục thể chất",
                    Location = "Sân bóng đá Cơ sở 1",
                    Campus = "Cơ sở 1",
                    StartTime = now.AddDays(14),
                    EndTime = now.AddDays(20),
                    RegistrationOpenTime = now,
                    RegistrationCloseTime = now.AddDays(10),
                    MaxParticipants = 50,
                    CurrentParticipantsCount = 0,
                    TrainingPoints = 8,
                    TargetAudience = "Sinh viên Khóa K21",
                    Status = "Published",
                    CreatedAt = now,
                    Sessions =
                    [
                        new ActivitySession
                        {
                            SessionName = "Vòng bảng",
                            Campus = "Cơ sở 1",
                            Location = "Sân bóng đá Cơ sở 1",
                            StartTime = now.AddDays(14),
                            EndTime = now.AddDays(17),
                            SortOrder = 1
                        },
                        new ActivitySession
                        {
                            SessionName = "Vòng chung kết",
                            Campus = "Cơ sở 1",
                            Location = "Sân bóng đá Cơ sở 1",
                            StartTime = now.AddDays(18),
                            EndTime = now.AddDays(20),
                            SortOrder = 2
                        }
                    ],
                    AudienceRules =
                    [
                        new ActivityAudienceRule
                        {
                            AcademicYear = "K21",
                            ParticipationType = AudienceParticipationType.Recommended
                        }
                    ]
                }
            };

            context.Activities.AddRange(activities);
            context.SaveChanges();

            // Seed Activity Registration (for test student)
            var reg = new ActivityRegistration
            {
                ActivityId = activities[0].Id,
                StudentId = studentUser.Id,
                RegisteredAt = now.AddDays(-1),
                Status = "Registered"
            };
            context.ActivityRegistrations.Add(reg);
            context.SaveChanges();
        }

        /// <summary>
        /// Bổ sung tài khoản demo cho môi trường Development. Idempotent: chỉ tạo khi chưa tồn tại,
        /// nên chạy được cả trên database đã seed từ Sprint trước.
        /// </summary>
        public static void EnsureDevelopmentAccounts(AppDbContext context)
        {
            const string hssvCode = "HSSV001";
            const string hssvEmail = "hssv@school.edu.vn";

            if (context.Users.Any(user => user.StudentCode == hssvCode || user.Email == hssvEmail))
            {
                return;
            }

            context.Users.Add(new User
            {
                StudentCode = hssvCode,
                FullName = "Phòng Quản lý HSSV",
                Email = hssvEmail,
                PasswordHash = PasswordHasher.HashPassword("Hssv123@"),
                Phone = "0901234568",
                Role = Role.HSSV,
                Campus = "Cơ sở 1",
                AcademicYear = "Staff",
                CreatedAt = DateTime.UtcNow
            });
            context.SaveChanges();
        }
    }
}
