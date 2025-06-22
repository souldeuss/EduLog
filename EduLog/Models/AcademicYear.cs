using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class AcademicYear : ISchoolEntity
    {
        [Key]
        public int Id { get; set; }
        public int SchoolId { get; set; }

        [Required(ErrorMessage = "Введіть назву")]
        [Display(Name = "Назва")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Початок")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Кінець")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Поточний")]
        public bool IsCurrent { get; set; }

        [Display(Name = "Архівований")]
        public bool IsArchived { get; set; }
    }
}
