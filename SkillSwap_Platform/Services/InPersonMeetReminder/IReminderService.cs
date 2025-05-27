namespace SkillSwap_Platform.Services.InPersonMeetReminder
{
    public interface IReminderService
    {
        Task SendEndMeetingReminder(int exchangeId);
        Task SendMissingEndProofReminder(int exchangeId);
    }
}
