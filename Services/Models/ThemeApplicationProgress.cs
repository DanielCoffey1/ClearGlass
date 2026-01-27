namespace ClearGlass.Services.Models
{
    /// <summary>
    /// Represents progress information during theme application
    /// </summary>
    public class ThemeApplicationProgress
    {
        /// <summary>
        /// Gets the current step description
        /// </summary>
        public string Step { get; }

        /// <summary>
        /// Gets the percentage complete (0-100)
        /// </summary>
        public int PercentComplete { get; }

        /// <summary>
        /// Gets whether the current step is a retry attempt
        /// </summary>
        public bool IsRetry { get; }

        /// <summary>
        /// Gets the current attempt number for the step
        /// </summary>
        public int AttemptNumber { get; }

        public ThemeApplicationProgress(string step, int percentComplete, bool isRetry = false, int attemptNumber = 1)
        {
            Step = step;
            PercentComplete = percentComplete;
            IsRetry = isRetry;
            AttemptNumber = attemptNumber;
        }
    }
}
