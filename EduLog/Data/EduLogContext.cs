using EduLog.Models;
using EduLog.Services;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EduLog.Data
{
    public class EduLogContext : IdentityDbContext<ApplicationUser>
    {
        private readonly ITenantService? _tenantService;

        public EduLogContext(DbContextOptions<EduLogContext> options, ITenantService? tenantService = null)
            : base(options)
        {
            _tenantService = tenantService;
        }

        public DbSet<School> School { get; set; }
        public DbSet<Teacher> Teacher { get; set; }
        public DbSet<Subject> Subject { get; set; }
        public DbSet<Grade> Grade { get; set; }
        public DbSet<Student> Student { get; set; }
        public DbSet<Absence> Absence { get; set; }
        public DbSet<Class> Class { get; set; }
        public DbSet<ClassSubject> ClassSubject { get; set; }
        public DbSet<Invitation> Invitation { get; set; }
        public DbSet<AcademicYear> AcademicYear { get; set; }
        public DbSet<ScheduleSlot> ScheduleSlot { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ClassSubject composite key + relationships
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

            // ScheduleSlot FK — restrict delete to avoid cascade cycles
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(s => s.Class).WithMany().HasForeignKey(s => s.ClassId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(s => s.Subject).WithMany().HasForeignKey(s => s.SubjectId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(s => s.Teacher).WithMany().HasForeignKey(s => s.TeacherId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(s => s.AcademicYear).WithMany().HasForeignKey(s => s.AcademicYearId).OnDelete(DeleteBehavior.Restrict);

            // Global query filters
            modelBuilder.Entity<Teacher>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
            modelBuilder.Entity<Class>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
            modelBuilder.Entity<Student>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
            modelBuilder.Entity<Subject>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
            modelBuilder.Entity<Grade>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
            modelBuilder.Entity<Absence>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
            modelBuilder.Entity<ClassSubject>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
            modelBuilder.Entity<Invitation>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
            modelBuilder.Entity<AcademicYear>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
            modelBuilder.Entity<ScheduleSlot>().HasQueryFilter(e =>
                _tenantService == null || _tenantService.SchoolId == null || e.SchoolId == _tenantService.SchoolId);
        }

        // Auto-set SchoolId for new entities from current tenant
        public override int SaveChanges()
        {
            SetTenantId();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetTenantId();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void SetTenantId()
        {
            if (_tenantService?.SchoolId == null) return;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added && entry.Entity is ISchoolEntity schoolEntity && schoolEntity.SchoolId == 0)
                {
                    schoolEntity.SchoolId = _tenantService.SchoolId.Value;
                }
            }
        }
    }
}