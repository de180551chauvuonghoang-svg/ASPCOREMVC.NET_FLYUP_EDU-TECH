using EduFlyUp.BusinessObjects;
using EduFlyUp.Repositories;

namespace EduFlyUp.Services;

public interface IInstructorPayoutService
{
    Task<InstructorPayoutBalance> BalanceAsync(InstructorIdentity instructor);
    Task<InstructorPayoutResult> CreateAsync(InstructorIdentity instructor, InstructorPayoutInput input);
    Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page, PayoutStatus? status = null);
}

public sealed class InstructorPayoutService(IInstructorPayoutRepository repository) : IInstructorPayoutService
{
    private static bool ValidIdentity(InstructorIdentity instructor) => !string.IsNullOrWhiteSpace(instructor.UserId) && !string.IsNullOrWhiteSpace(instructor.UserName);
    public Task<InstructorPayoutBalance> BalanceAsync(InstructorIdentity instructor)
        => ValidIdentity(instructor) ? repository.BalanceAsync(instructor) : Task.FromResult(new InstructorPayoutBalance(null, 0, 0, 0));
    public async Task<InstructorPayoutResult> CreateAsync(InstructorIdentity instructor, InstructorPayoutInput input)
    {
        input = InstructorPayoutPolicy.Normalize(input);
        var change = ValidIdentity(instructor) && InstructorPayoutPolicy.Valid(input) && input.SubmissionKey.HasValue && input.SubmissionKey != Guid.Empty
            ? await repository.CreateAsync(instructor, input) : InstructorPayoutChange.Invalid;
        return new(change, change switch
        {
            InstructorPayoutChange.Created => "Đã gửi yêu cầu rút tiền. Vui lòng chờ Admin xử lý.",
            InstructorPayoutChange.AlreadySubmitted => "Yêu cầu này đã được gửi trước đó. Vui lòng xem lịch sử xử lý.",
            InstructorPayoutChange.SubmissionConflict => "Thông tin lần gửi không khớp. Vui lòng mở lại trang yêu cầu rút tiền.",
            InstructorPayoutChange.EarningsUnavailable => "Chưa xác minh được số dư từ doanh thu đã đối soát. Hiện chưa thể gửi yêu cầu rút tiền.",
            InstructorPayoutChange.PendingExists => "Bạn đang có yêu cầu chờ xử lý. Vui lòng chờ kết quả trước khi gửi yêu cầu mới.",
            InstructorPayoutChange.InsufficientBalance => "Số tiền yêu cầu vượt số dư khả dụng. Vui lòng kiểm tra lại.",
            _ => "Thông tin yêu cầu không hợp lệ. Vui lòng kiểm tra số tiền và thông tin ngân hàng."
        });
    }
    public Task<InstructorPayoutHistory> HistoryAsync(InstructorIdentity instructor, int page, PayoutStatus? status = null)
        => !string.IsNullOrWhiteSpace(instructor.UserId) && !string.IsNullOrWhiteSpace(instructor.UserName) && (!status.HasValue || Enum.IsDefined(status.Value))
            ? repository.HistoryAsync(instructor, Math.Max(1, page), status)
            : Task.FromResult(new InstructorPayoutHistory([], 1, 0));
}
