using System;
using System.Diagnostics;
using System.Text;

namespace AppsScriptManager
{
    public static partial class AppsScriptSourceCodeManager
    {
        /// <summary>
        /// Local exception class for errors emanating from within specific library.
        /// </summary>
        public class InfoException : Exception
        {
            /// <summary>
            /// Not used.
            /// </summary>
            private InfoException()
            {
            }

            /// <summary>
            /// Construct an InfoException
            /// </summary>
            /// <param name="Message">The message</param>
            public InfoException(string Message)
                : base(Message)
            {
            }

            /// <summary>
            /// This is used to Create an Info Exception with multiple input message strings.
            /// </summary>
            /// <param name="Messages">String array of messages</param>
            /// <returns></returns>
            public static InfoException GetInfoException(params string[] Messages)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var str in Messages)
                    sb.AppendLine(str);

                return new InfoException(sb.ToString());
            }

            /// <summary>
            /// Construct an InfoException
            /// </summary>
            /// <param name="Message">The message</param>
            /// <param name="Inner">The inner error</param>
            public InfoException(string Message, Exception Inner)
                : base(Message, Inner)
            {
            }

            /// <summary>
            /// Not used.
            /// </summary>
            /// <param name="info"></param>
            /// <param name="context"></param>
            protected InfoException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
            {
            }

            /// <summary>
            /// Prints the inner message.
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return base.Message;
            }
        }

        /// <summary>
        /// Uses the error message for InfoExceptions,
        /// otherwise prints all other exceptions to debug and returns a default message.
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="defaultMsg"></param>
        /// <returns></returns>
        private static string getExceptionString(Exception ex, string defaultMsg)
        {
            if (ex is InfoException)
            {
                return ex.Message;
            }
            else
            {
                Debug.WriteLine(ex.Message);
                return defaultMsg;
            }
        }

    }
}