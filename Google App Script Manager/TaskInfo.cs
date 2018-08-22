using System;

namespace AppScriptManager
{
    public static partial class AppScriptSourceCodeManager
    {
        /// <summary>
        /// Maintains information about Tasks.
        /// Allows for a single type as the result.
        /// Includes boolean indicating the success of the task.
        /// Allows for string of additional information when classes have non-string results.
        /// </summary>
        /// <typeparam name="T">The Type to use for MyResult</typeparam>
        public sealed class TaskInfo<T>
        {
            /// <summary>
            /// The Result of the using Task
            /// </summary>
            public T MyResult { get; }

            /// <summary>
            /// Whether or not the Task succeeded
            /// </summary>
            public bool IsSuccess { get; }

            /// <summary>
            /// Extra information about the execution of this task.
            /// This can descript any errors, settings, details, etc.
            /// </summary>
            public string AdditionalInformation { get; }

            /// <summary>
            /// Constructs a successful TaskInfo
            /// </summary>
            /// <param name="Result">The result of this task</param>
            public TaskInfo(T Result)
            {
                MyResult = Result;
                IsSuccess = true;
                AdditionalInformation = "";
            }

            /// <summary>
            /// Constructs a Task Info
            /// </summary>
            /// <param name="Result">The result of this task</param>
            /// <param name="Succeeded">If it completed successfully</param>
            public TaskInfo(T Result, bool Succeeded)
            {
                MyResult = Result;
                IsSuccess = Succeeded;
                AdditionalInformation = "";
            }

            /// <summary>
            /// Constructs a Task Info
            /// </summary>
            /// <param name="Result">The result of this task</param>
            /// <param name="Succeeded">If it completed successfully</param>
            /// <param name="Info">Extra information about this task</param>
            public TaskInfo(T Result, bool Succeeded, string Info)
            {
                MyResult = Result;
                IsSuccess = Succeeded;
                AdditionalInformation = Info;
            }

            /// <summary>
            /// Prints either MyResult (if the script succeeded), or the AdditionalInfo.
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                if (IsSuccess)
                    return MyResult.ToString();
                return AdditionalInformation;
            }

        }
    }
}