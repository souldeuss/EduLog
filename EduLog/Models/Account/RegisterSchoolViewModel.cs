using System.ComponentModel.DataAnnotations;

namespace EduLog.Models.Account
{
    public class RegisterSchoolViewModel
    {
        [Required(ErrorMessage = "Введіть назву школи")]
        [Display(Name = "Назва школи")]
        public string SchoolName { get; set; } = string.Empty;

        [Display(Name = "Адреса")]
        public string? SchoolAddress { get; set; }

        [Display(Name = "Тип закладу")]
        public string? SchoolType { get; set; }

        [Required(ErrorMessage = "Введіть ім'я")]
        [Display(Name = "Ім'я")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть прізвище")]
        [Display(Name = "Прізвище")]
        public string Surname { get; set; } = string.Empty;

        [Display(Name = "По батькові")]
        public string? Patronymic { get; set; }

        [Required(ErrorMessage = "Введіть Email")]
        [EmailAddress(ErrorMessage = "Невірний формат Email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введіть пароль")]
        [StringLength(100, ErrorMessage = "Пароль має містити щонайменше {2} символів", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Підтвердіть пароль")]
        [DataType(DataType.Password)]
        [Display(Name = "Підтвердження паролю")]
        [Compare("Password", ErrorMessage = "Паролі не співпадають")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
