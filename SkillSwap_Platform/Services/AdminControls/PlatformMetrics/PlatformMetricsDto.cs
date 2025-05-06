namespace SkillSwap_Platform.Services.AdminControls.PlatformMetrics
{
    public class PlatformMetricsDto
    {
        // Growth
        public int TotalUsers { get; set; }
        public int NewUsersToday { get; set; }
        public int NewUsersThisWeek { get; set; }

        // Engagement
        public int DAU { get; set; }
        public int WAU { get; set; }
        public int MAU { get; set; }
        public double Stickiness { get; set; }               // DAU / MAU * 100

        // Retention
        public double Day1Retention { get; set; }
        public double Day7Retention { get; set; }

        // Monetization & Lifetime
        public decimal TotalRevenueMonth { get; set; }
        public decimal ARPU { get; set; }                    // revenue/MAU
        public double ChurnRate { get; set; }
        public decimal LTV { get; set; }
        // revenue/TotalUsers

        // Swap & Offer Metrics
        public double SwapsPerMAU { get; set; }
        public double OffersPer1kDAU { get; set; }
        public int TotalOffers { get; set; }
        public int TotalSwapsAttempted { get; set; }
        public int TotalSwapsSuccessful { get; set; }
        public double SwapSuccessRate { get; set; }          // successful/attempted *100
        public double OfferAcceptanceRate { get; set; }      // successful swaps / total offers *100
        public double AverageTokensPerSwap { get; set; }

        // Dispute Metrics (if you track escrows)
        public double DisputeRate { get; set; }              // disputed escrows / total escrows *100

        // System Health (stubs—wire in your APM/logging later)
        public double ErrorRate { get; set; }
        public double P95LatencyMs { get; set; }

        // ── Tokens & Exchange Metrics ─────────────────────────────
        public int TotalTokenTransactions { get; set; }
        public decimal TotalTokensProcessed { get; set; }
        public decimal AvgTokensPerTransaction { get; set; }
        public int TotalExchanges { get; set; }
        public int TotalSuccessfulExchanges { get; set; }
        public int TotalCancelledExchanges { get; set; }
        public double CancelRate { get; set; }      // = cancelled / total × 100
        public int InPersonExchanges { get; set; }
        public int DigitalExchanges { get; set; }


        public int TotalOnlineMeetings { get; set; }
        public int TotalInPersonMeetings { get; set; }
        public int TotalResourceShare => TotalOnlineMeetings + TotalInPersonMeetings;
    }
}
