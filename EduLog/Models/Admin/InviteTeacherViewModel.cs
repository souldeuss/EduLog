using System.ComponentModel.DataAnnotations;

namespace EduLog.Models.Admin
{
    public class InviteTeacherViewModel
    {
        [Required(ErrorMessage = "Введіть Email вчителя")]
        [EmailAddress(ErrorMessage = "Невірний формат Email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }
}
