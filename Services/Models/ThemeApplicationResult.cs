using System.Collections.Generic;
using System.Linq;
using ClearGlass.Services.Reliability;

namespace ClearGlass.Services.Models
{
    /// <summary>
    /// Represents the overall result of applying theme settings
    /// </summary>
    public class ThemeApplicationResult
    {
        /// <summary>
        /// Gets whether all operations completed successfully
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets the results of individual operations
        /// </summary>
        public IReadOnlyList<OperationResult> OperationResults { get; init; } = new List<OperationResult>();

        /// <summary>
        /// Gets the count of successful operations
        /// </summary>
        public int SuccessCount => OperationResults.Count(r => r.Success);

        /// <summary>
        /// Gets the count of failed operations
        /// </summary>
        public int FailureCount => OperationResults.Count(r => !r.Success);

        /// <summary>
        /// Gets a summary message describing the result
        /// </summary>
        public string Summary
        {
            get
            {
                if (Success)
                {
                    return $"All {OperationResults.Count} operations completed successfully.";
                }

                var failed = OperationResults.Where(r => !r.Success).Select(r => r.OperationName);
                return $"{FailureCount} of {OperationResults.Count} operations failed: {string.Join(", ", failed)}";
            }
        }

        /// <summary>
        /// Creates a successful result from operation results
        /// </summary>
        public static ThemeApplicationResult FromResults(IEnumerable<OperationResult> results)
        {
            var resultList = results.ToList();
            return new ThemeApplicationResult
            {
                Success = resultList.All(r => r.Success),
                OperationResults = resultList
            };
        }
    }
}
