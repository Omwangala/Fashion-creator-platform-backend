using System;

namespace backend.Models
{
    public enum UploadStatus
    {
        Pending,
        Uploading,
        Processing,
        Ready,
        Failed
    }
}
