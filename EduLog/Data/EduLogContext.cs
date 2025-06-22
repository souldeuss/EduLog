using EduLog.Models;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Data
{
    public class EduLogContext : DbContext
    {
        public EduLogContext(DbContextOptions<EduLogContext> options)
            : base(options)
        {
        }
        public DbSet<Teacher> Teacher { get; set; }
        public DbSet<Subject> Subject { get; set; }
        public DbSet<Grade> Grade { get; set; }
        public DbSet<Student> Student { get; set; }
        public DbSet<Absence> Absence { get; set; }
        public DbSet<Class> Class { get; set; }
        public DbSet<ClassSubject> ClassSubject { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            // Seed предмету "Англійська мова", прив'язаний до першого вчителя
            modelBuilder.Entity<Subject>().HasData(
                new Subject
                {
                    Id = 1,
                    Name = "Англійська мова",
                    TeacherId = 1,
                    ClassId = 1
                }
            );
            // Seed класів
            modelBuilder.Entity<Class>().HasData(
                new Class { Id = 1, Name = "5-A" , TeacherId = 1},
                new Class { Id = 2, Name = "5-B" , TeacherId = null}
            );

            //// Seed вчителя
            modelBuilder.Entity<Teacher>().HasData
                (
                    new Teacher
                    {
                        Id = 1,
                        Name = "Олена",
                        Surname = "Іваненко",
                        Patronymic = "Петрівна",
                        PhotoPath = "~/Data/UserImages/User-avatar.svg.png"
                    }
                );

            // Seed учнів
            var students = new List<Student>();
            for (int i = 1; i <= 20; i++)
            {
                students.Add(new Student
                {
                    Id = i,
                    Name = $"Ім'я{i}",
                    Surname = $"Прізвище{i}",
                    Patronymic = $"По-батькові{i}",
                    ClassId = 1
                });
            }
            for (int i = 21; i <= 40; i++)
            {
                students.Add(new Student
                {
                    Id = i,
                    Name = $"Ім'я{i}",
                    Surname = $"Прізвище{i}",
                    Patronymic = $"По-батькові{i}",
                    ClassId = 2
                });
            }
            modelBuilder.Entity<Student>().HasData(students);

            modelBuilder.Entity<ClassSubject>()
                .HasKey(cs => new { cs.ClassId, cs.SubjectId });

            modelBuilder.Entity<ClassSubject>()
                .HasOne(cs => cs.Class)
                .WithMany(c => c.ClassSubjects)
                .HasForeignKey(cs => cs.ClassId);

            modelBuilder.Entity<ClassSubject>()
                .HasOne(cs => cs.Subject)
                .WithMany(s => s.ClassSubjects)
                .HasForeignKey(cs => cs.SubjectId);

            base.OnModelCreating(modelBuilder);
        }
    }
}