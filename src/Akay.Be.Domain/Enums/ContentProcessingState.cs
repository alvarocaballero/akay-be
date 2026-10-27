namespace Akay.Be.Domain.Enums;

public enum ContentProcessingState
{
    UploadPending = 0,
    Uploading = 1,
    UploadFailed = 2,
    Pending = 3,
    Processing = 4,
    Ready = 5,
    Failed = 6
}
