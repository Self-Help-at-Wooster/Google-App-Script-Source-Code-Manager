using System.IO;

namespace AppsScriptManager
{
    public static partial class AppsScriptSourceCodeManager
    {
        /// <summary>
        /// Denotes all usabled file types by Google Apps Script
        /// </summary>
        public enum FILE_TYPES
        {
            /// <summary>
            /// The .gs files that run server code.
            /// </summary>
            SERVER_JS,
            /// <summary>
            /// The .html files that run client code. These can contain HTML, CSS, or javascript.
            /// </summary>
            HTML,
            /// <summary>
            /// The .json file only for appsscript.json. These cannot be instantiated.
            /// </summary>
            JSON
        }

        /// <summary>
        /// Gets the Google file type from a given file path.
        /// </summary>
        /// <param name="path">The path to your file</param>
        /// <returns>Google File Type enum (as string)</returns>
        private static string getGoogleFileType(string path)
        {
            switch (path.Substring(path.LastIndexOf(".")))
            {
                case ".js":
                    return "SERVER_JS";
                case ".html":
                    return "HTML";
                case ".json":
                    return "JSON";
            }
            return null;
        }

        private static string getGoogleFileType(FILE_TYPES f)
        {
            switch (f)
            {
                case FILE_TYPES.SERVER_JS:
                    return "SERVER_JS";
                case FILE_TYPES.HTML:
                    return "HTML";
                default:
                    return "JSON";
            }
        }

        private static string getExtensionFromFileType(Google.Apis.Script.v1.Data.File file)
        {
            switch (file.Type)
            {
                case "SERVER_JS":
                    return ".js";
                case "HTML":
                    return ".html";
                case "JSON":
                    return ".json";
                default:
                    return "";
            }
        }

        private static DirectoryInfo getFolderFromFileType(Google.Apis.Script.v1.Data.File file)
        {
            switch (file.Type)
            {
                case "SERVER_JS":
                    return javascriptDirectory;
                case "HTML":
                    return htmlDirectory;
                case "JSON":
                    return jsonDirectory;
            }
            return null;
        }

        private static string getExtensionFromFileType(FILE_TYPES f)
        {
            switch (f)
            {
                case FILE_TYPES.SERVER_JS:
                    return ".js";
                case FILE_TYPES.HTML:
                    return ".html";
                case FILE_TYPES.JSON:
                    return ".json";
                default:
                    return "";
            }
        }

        private static DirectoryInfo getFolderFromFileType(FILE_TYPES f)
        {
            switch (f)
            {
                case FILE_TYPES.SERVER_JS:
                    return javascriptDirectory;
                case FILE_TYPES.HTML:
                    return htmlDirectory;
                case FILE_TYPES.JSON:
                    return jsonDirectory;
            }
            return null;
        }

    }
}