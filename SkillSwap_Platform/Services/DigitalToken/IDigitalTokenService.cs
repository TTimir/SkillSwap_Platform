namespace SkillSwap_Platform.Services.DigitalToken
{
    public interface IDigitalTokenService
    {
        /// <summary>
        /// Called immediately after both parties sign.  Debits buyer and holds tokens in escrow.
        /// </summary>
        Task HoldTokensAsync(int exchangeId);

        /// <summary>
        /// Called when the exchange actually completes: releases the held tokens from escrow to the seller.
        /// </summary>
        Task ReleaseTokensAsync(int exchangeId);

        /// <summary>
        /// Returns the current token‐balance for the given user.
        /// </summary>
        Task<decimal> GetBalanceAsync(int userId);

        /// <summary>
        /// Returns true if the user has at least <paramref name="amount"/> tokens.
        /// </summary>
        Task<bool> HasSufficientBalanceAsync(int userId, decimal amount);

        Task RecordTransactionAsync(int? exchangeId, int fromUserId, int toUserId, decimal amount, string txType, string description);
    }
}
