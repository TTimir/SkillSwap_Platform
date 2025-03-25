namespace SkillSwap_Platform.Models.ViewModels.MessagesVM
{
    public class MessageAttachmentVM
    {
        public int AttachmentId { get; set; }
        public int MessageId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }

        public TblMessage Message { get; set; }
    }
}
