using System.ComponentModel.DataAnnotations;

namespace EduLog.Models
{
    public class School
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Назва школи")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Адреса")]
        public string? Address { get; set; }

        [Display(Name = "Тип закладу")]
        public string? Type { get; set; }
    }
}
