using System.ComponentModel.DataAnnotations;
using EduFlyUp.BusinessObjects;

namespace EduFlyUp.Web.Models.Instructor;

public sealed class InstructorPayoutForm
{
    [Required(ErrorMessage = "Mã lần gửi không hợp lệ. Vui lòng mở lại trang yêu cầu rút tiền.")]
    public Guid? SubmissionKey { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập số tiền.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Số tiền phải lớn hơn 0 và trong giới hạn cho phép.")]
    public decimal? Amount { get; set; }
    [Required, StringLength(100)] public string BankName { get; set; } = "";
    [Required, StringLength(34, MinimumLength = 4), RegularExpression("^[A-Za-z0-9]+$")]
    public string AccountNumber { get; set; } = "";
    [Required, StringLength(100)] public string AccountHolderName { get; set; } = "";
    public InstructorPayoutBalance Balance { get; set; } = new(null, 0, 0, 0);
}
