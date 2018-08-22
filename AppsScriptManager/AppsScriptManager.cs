using Google.Apis.Auth.OAuth2;
using Google.Apis.Script.v1;
using Google.Apis.Script.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Google.Apis.Script.v1.ProjectsResource;

namespace AppsScriptManager
{
    /// <summary>
    /// This library is intended for use with the attached Source Editor Client appliation.
    /// Uses Tasks for asynchronous code.
    /// Ensure to use .result or .wait() when necessary.
    /// These functions may return null to denote an error.
    /// Check the debug console for library-related errors.
    /// By using this library, you agree to its Code of Conduct, Usage Policy, and Software License.
    /// You may find this here:
    /// https://docs.google.com/document/d/1TiT9CXUkvVxP8DQyyVfhkz1adzftrbIYvWgYzScaQ8U/edit?usp=sharing
    /// </summary>
    public static partial class AppsScriptSourceCodeManager
    {
        /// <summary>
        /// The Google API Scopes this application needs to authorize.
        /// </summary>
        private static readonly string[] scopes = { "https://www.googleapis.com/auth/script.projects", "https://www.googleapis.com/auth/script.deployments", "https://www.googleapis.com/auth/drive", "https://www.googleapis.com/auth/userinfo.email" };
        private const string applicationName = "ENTER NAME HERE";
        private const string apiKeyUser = "ENTER API KEY HERE";

        private const string credPath = "client_token.json";
        private const string lastScriptID = "ScriptID.txt";
        private const string webAppDeploymentString = "web app meta-version";
        private const string webAppEntryPoint = "WEB_APP";
        private const string webAppManifestName = "appsscript";

        /// <summary>
        /// Whether or not the script should extract a <script> **your code** </script> tag from an HTML (.html) file into a JAVASCRIPT (.js) file.
        /// This is an extremely useful feature if you want autocomplete, variable renaming, etc for your javascript code.
        /// The library will substitute you script code for a tag that looks like <script data-placeholder="filenameJS.js"></script>
        /// Please do not modify this tag, and if you do, make sure it matches that format.
        /// Note, there is no way to disable this feature on uploads. If you leave a script tag with a placeholder attribute like the above,
        /// the library will always search for a corresponding file. If you do not seek this behavior, simply remove the attribute or tag from your .html file.
        /// </summary>
        public static bool ParseHTMLScriptTagToJS = true;

        public static bool AutoCreateWebappDeployment = true;

        private static string getJavascriptPlaceholderFileName(string filepath) => Path.GetFileNameWithoutExtension(filepath) + javascriptPlaceholder;

        private static string getJavascriptPlaceholder(string filename) => "<script data-placeholder=\"" + filename + "\">" + endscriptTag;
        private const string scriptTag = "<script>";
        private const string endscriptTag = "</script>";
        private const string javascriptPlaceholder = "_JS.js";

        /// <summary>
        /// The time before your OAuth authenticator will be automatically cancelled.
        /// </summary>
        private static TimeSpan waitTime = TimeSpan.FromSeconds(30);
        private static UserCredential credential;
        private static ScriptService scriptService;
        private static Project curProject;
        private static ProjectsResource projectResource;
        private static List<Deployment> deployments;

        /// <summary>
        /// Your web-app deployment that this application references.
        /// </summary>
        private static Deployment webAppDeployment;

        /// <summary>
        /// The development URL ( a little secret :D )
        /// </summary>
        private static Deployment headDeployment;

        private static List<Google.Apis.Script.v1.Data.Version> versions;

        /// <summary>
        /// The directories used to store your Source Code.
        /// </summary>
        private static DirectoryInfo sourceCodeDirectory, javascriptDirectory, htmlDirectory, jsonDirectory, backupDirectory;

        /// <summary>
        /// The maximum number of versions and deployments this library can return.
        /// </summary>
        private const int maxDownloadPageSize = 50000;

        private static bool libraryReady { get { return !string.IsNullOrEmpty(ScriptID) && credential != null && scriptService != null && projectResource != null; } }

        private const string libraryUninitialized = "Please initialize the library first, and then provide a GAS Script ID!";
        private static readonly TaskInfo<string> libraryUnitializedInfo = new TaskInfo<string>(libraryUninitialized, false, libraryUninitialized);

        private static string scriptID;
        private static readonly Assembly assembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// The path where your Google oauth credentials are stored.
        /// </summary>
        public static string CredentialsPath { get; private set; }

        /// <summary>
        /// Script ID that you want this library to access.
        /// You can get this from File->Project properties->Script ID from your Google Apps Script project.
        /// You must own or have edit access to modify this script.
        /// Please ensure you have enabled the Apps Script API @ https://script.google.com/home/usersettings
        /// Changing this value will store it in a text file for your convenience.
        /// </summary>
        /// <exception cref="InfoException">Occurs when validation of the Script ID fails</exception>
        public static string ScriptID
        {
            get { return scriptID; }
            set
            {
                bool changed = scriptID == null || value == null || !scriptID.Equals(value);
                scriptID = value;
                if (!string.IsNullOrEmpty(scriptID) && changed)
                {
                    var strw = System.IO.File.CreateText(lastScriptID);
                    strw.Write(scriptID);
                    strw.Close();

                    if (libraryReady)
                    {
                        GetRequest gr = projectResource.Get(scriptID);

                        try
                        {
                            versions = null;
                            deployments = null;
                            headDeployment = null;
                            webAppDeployment = null;

                            if (libraryReady)
                            {
                                curProject = gr.Execute();
                                if (AutoCreateWebappDeployment)
                                    getWebAppDeployment();
                            }
                        }
                        catch (InfoException ex) //cast it upwards
                        {
                            Debug.WriteLine(ex);
                            scriptID = null;
                            curProject = null;
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                            scriptID = null;
                            curProject = null;
                            throw new InfoException("Script ID Validation Failed! Please Try Again. Ensure that the ID is correct and you have permission!");
                        }
                    }
                }
            }
        }

        private static string getDevelopmentURL()
        {
            string devURL = "No development URL found";
            if (headDeployment?.EntryPoints?.Count > 0)
            {
                var devDeployment = headDeployment?.EntryPoints[0];
                if (!string.IsNullOrEmpty(devDeployment.WebApp?.Url))
                    devURL = devDeployment.WebApp.Url;
            }
            return devURL;
        }

        /// <summary>
        /// Retrieves script information as an array.
        /// </summary>
        /// <returns>Script info array</returns>
        public static string[] GetScriptInfo()
        {
            if (!string.IsNullOrEmpty(scriptID) && projectResource != null && curProject != null)
            {
                StringBuilder info = new StringBuilder();
                info.AppendFormat("Script ID: {0}", scriptID).AppendLine();
                info.AppendFormat("Project Title {0} By {1}", curProject.Title, curProject.Creator.Email).AppendLine();

                if (webAppDeployment?.EntryPoints != null && webAppDeployment.EntryPoints.Count > 0)
                {
                    foreach (EntryPoint e in webAppDeployment.EntryPoints)
                    {
                        Debug.WriteLine(e.EntryPointType);
                        if (e.EntryPointType.Equals(webAppEntryPoint))
                        {
                            info.AppendFormat("Webapp URL: {0}", e.WebApp.Url).AppendLine();
                            break;
                        }
                    }
                }
                string devURL = getDevelopmentURL();

                info.AppendFormat("Dev URL: {0}", devURL).AppendLine();

                int latestVersion = getLatestVersion();
                if (latestVersion > 0)
                    info.AppendFormat("Your Latest Project Version is {0}, Current Deployment Version {1}", latestVersion, webAppDeployment?.DeploymentConfig?.VersionNumber?.ToString() ?? "deployment config unavailable");
                else
                    info.Append("No Versions Exist Yet");
                return info.ToString().Split(Environment.NewLine);
            }
            return new string[] { "No Script Info Exists Yet!" };
        }

        /// <summary>
        /// Uses the last Script ID set you set for your convenience.
        /// </summary>
        /// <returns></returns>
        private static void loadLastScriptID()
        {
            if (System.IO.File.Exists(lastScriptID))
            {
                try
                {
                    ScriptID = System.IO.File.ReadAllText(lastScriptID);
                    if (!string.IsNullOrEmpty(scriptID))
                        Debug.WriteLine("Your last Script ID is " + scriptID);
                }
                catch (Exception)
                {
                    System.IO.File.Delete(lastScriptID);
                    //Debug.WriteLine(ex.Message);
                }
            }
            else
                Debug.WriteLine("No Saved Script ID File.");
        }

        private static string debugWriteLn(string message)
        {
            Debug.WriteLine(message);
            return message;
        }

        /// <summary>
        /// Dumps all your source code into the backup directory.
        /// </summary>
        private static void sourceCodeBackup()
        {
            string date = DateTime.Now.ToLongTimeString().Replace(":", ".");

            DirectoryInfo newBackup;
            if (curProject?.Title != null)
                newBackup = backupDirectory.CreateSubdirectory("Backup for " + curProject.Title + " at" + date);
            else
                newBackup = backupDirectory.CreateSubdirectory("Backup at " + date);

            var javascriptBackup = newBackup.CreateSubdirectory("JAVASCRIPT");
            var htmlBackup = newBackup.CreateSubdirectory("HTML");
            var jsonBackup = newBackup.CreateSubdirectory("JSON");

            foreach (var file in javascriptDirectory.GetFiles())
                System.IO.File.Copy(file.FullName, javascriptBackup.FullName + @"\" + file.Name, true);
            foreach (var file in htmlDirectory.GetFiles())
                System.IO.File.Copy(file.FullName, htmlBackup.FullName + @"\" + file.Name, true);
            foreach (var file in htmlDirectory.GetFiles())
                System.IO.File.Copy(file.FullName, jsonBackup.FullName + @"\" + file.Name, true);
            foreach (var file in sourceCodeDirectory.GetFiles()) //just in case there's any in the main directory
                System.IO.File.Copy(file.FullName, newBackup.FullName + @"\" + file.Name, true);

            Debug.WriteLine("Project Backup stored at " + newBackup.FullName);
        }

        /// <summary>
        /// Creates your SourceCode Directory and subfolders
        /// Also retrieves the necessary DirectoryInfo.
        /// </summary>
        /// <param name="source">Where to create your SourceCode folder</param>
        private static void createSourceCodeDirectories(string source)
        {
            try
            {
                sourceCodeDirectory = Directory.CreateDirectory(source + "\\SourceCode");
                javascriptDirectory = sourceCodeDirectory.CreateSubdirectory("JAVASCRIPT");
                htmlDirectory = sourceCodeDirectory.CreateSubdirectory("HTML");
                jsonDirectory = sourceCodeDirectory.CreateSubdirectory("JSON");
                backupDirectory = Directory.CreateDirectory("BACKUPS");
            }
            catch (Exception)
            {
                throw new InfoException("Directory creation failed! Please ensure your program files are not in a protected directory!");
            }
        }

        /// <summary>
        /// Returns a list of watchers for your 3 Source Code Directories (HTML/, JAVASCRIPT/, and JSON/)
        /// It's up to you to decide which ones you care about.
        /// You can use this to "auto deploy" your solution :D
        /// </summary>
        /// <returns>List of FileSystemWatcher, otherwise null if no SourceCode directory exists yet. (You have not initialized the library)</returns>
        public static List<FileSystemWatcher> GetWatchers()
        {
            List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();

            if (sourceCodeDirectory == null)
            {
                //Debug.WriteLine("No SourceCode Directory!");
                return null;
            }

            watchers.Add(new FileSystemWatcher(javascriptDirectory.FullName));
            watchers.Add(new FileSystemWatcher(htmlDirectory.FullName));
            watchers.Add(new FileSystemWatcher(jsonDirectory.FullName));

            return watchers;
        }

        /// <summary>
        /// Authenticates user and then initializes important classes for using the Google API.
        /// Asynchonosly authorizes credentials, allocating 20 seconds before cancelling.
        /// If successful, then uses credentials to create ScriptService and ProjectsResource
        /// </summary>
        /// <exception cref="InfoException"></exception>
        /// <exception cref="Exception"></exception>
        private static async Task<string> initUser()
        {
            var cts = new CancellationTokenSource(waitTime);
            cts.Token.Register(() => Debug.WriteLine("Cancellation token invoked due to taking too long!"));

            Task<UserCredential> auth;
            Task delay;

            try
            {
                using (var stream = assembly.GetManifestResourceStream("AppScriptManager.credentials.json"))
                {
                    auth = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        scopes,
                        "user",
                        cts.Token,
                        new FileDataStore(credPath, true)
                    );
                }
            }
            catch (Exception)
            {
                throw;
            }

            delay = Task.Delay(waitTime, cts.Token);

            if (await Task.WhenAny(auth, delay).ConfigureAwait(true) == auth)
            {
                credential = await auth;
                cts.Dispose();
                if (credential != null)
                {
                    scriptService = new ScriptService(new BaseClientService.Initializer()
                    {
                        ApiKey = apiKeyUser,
                        HttpClientInitializer = credential,
                        ApplicationName = applicationName
                    });

                    Debug.WriteLine("Credential file saved to: " + credPath);
                    CredentialsPath = Directory.GetCurrentDirectory() + "\\" + credPath;

                    projectResource = new ProjectsResource(scriptService);

                    return "User Authorization Succeeded!";
                }
                else
                    throw new InfoException("Credentials failed!");
            }
            else
            {
                cts.Cancel(); //only cancel if failure occured
                throw new InfoException("User Authorization Failed due to taking too long!");
            }
        }

        private static void createGoogleAppsScriptProject(string name, ProjectsResource p)
        {
            var cpr = new CreateProjectRequest
            {
                Title = name
            };

            ScriptID = p.Create(cpr).Execute().ScriptId;
        }

        private static Google.Apis.Script.v1.Data.File retrieveJSONManifestFile(bool save)
        {
            //THIS FUNCTION SHOULD BE RE-IMPLEMENTED
            return null;
        }

        private static void createFile(string name, FILE_TYPES extension)
        {
            string defaultCode = string.Empty;
            switch (extension)
            {
                case FILE_TYPES.SERVER_JS:
                    defaultCode = "function myFunction() {" + Environment.NewLine + Environment.NewLine + "}";
                    break;
                case FILE_TYPES.HTML:
                    defaultCode = "<!DOCTYPE html>" + Environment.NewLine + "<html>" + Environment.NewLine + "  <head>" + Environment.NewLine + "    <base target=\"_top\">" + Environment.NewLine + "  </head>" + Environment.NewLine + "  <body>" + Environment.NewLine + Environment.NewLine + "  </body>" + Environment.NewLine + "</html>";
                    break;
                default:
                    Debug.WriteLine("Unable to create JSON file type with this function.");
                    return;
            }

            string folder = getFolderFromFileType(extension).FullName;
            string ext = getExtensionFromFileType(extension);

            string path = folder + "\\" + name + ext;

            if (!System.IO.File.Exists(path))
            {
                createFileAndWrite(path, defaultCode);
                Debug.WriteLine("Created File... Please synchronize changes when ready to upload.");
            }
            else
            {
                Debug.WriteLine("A file with that name already exists!");
            }
        }

        private static void addFileToContent(FileInfo fi, Content content)
        {
            if (!fi.FullName.Contains(javascriptPlaceholder))
            {
                string path = fi.FullName;
                string googleType = getGoogleFileType(path);
                if (googleType != null)
                {
                    Debug.WriteLine("Uploading... " + path);
                    Google.Apis.Script.v1.Data.File file = new Google.Apis.Script.v1.Data.File();

                    using (FileStream f = fi.OpenRead())
                    using (StreamReader reader = new StreamReader(f))
                    {
                        file.Name = Path.GetFileNameWithoutExtension(path);
                        file.Type = googleType;
                        string source = reader.ReadToEnd(); //update the source code

                        if (Path.GetExtension(path).Equals(".html"))
                        {
                            string placeholderFileName = getJavascriptPlaceholderFileName(path);
                            string placeholderTag = getJavascriptPlaceholder(placeholderFileName);

                            int indscriptTag = source.IndexOf(placeholderTag);
                            //check for a single script tag
                            if (indscriptTag >= 0)
                            {
                                Debug.WriteLine("   Javascript placeholder tag located!");

                                string first = source.Substring(0, indscriptTag) + scriptTag;
                                string last = source.Substring(indscriptTag + placeholderTag.Length) + endscriptTag;

                                string placeHolderFilePath = Path.GetDirectoryName(path) + @"\" + placeholderFileName;

                                if (System.IO.File.Exists(placeHolderFilePath))
                                {
                                    StreamReader sr = new StreamReader(System.IO.File.OpenRead(placeHolderFilePath));
                                    string substitution = sr.ReadToEnd();

                                    source = first + substitution + last; //update for javascript file
                                    Debug.WriteLine("   Successfully substituted placeholder from file " + placeHolderFilePath);
                                }
                            }
                        }

                        file.Source = source;
                        Debug.WriteLine(file.Name + " " + file.Type + " Done");
                    }

                    content.Files.Add(file);
                }
            }
        }

        private static void uploadFiles()
        {
            Content content = new Content() { ScriptId = ScriptID }; //create content

            foreach (var file in javascriptDirectory.GetFiles())
                addFileToContent(file, content);

            foreach (var file in htmlDirectory.GetFiles())
                addFileToContent(file, content);

            foreach (var file in jsonDirectory.GetFiles())
                addFileToContent(file, content);

            new UpdateContentRequest(scriptService, content, ScriptID).Execute(); //send update request.

            Debug.WriteLine("Finished uploading your changes!");
        }

        private static void uploadTemporaryFiles(Content content)
        {
            new UpdateContentRequest(scriptService, content, ScriptID).Execute(); //send update request.

            Debug.WriteLine("Finished uploading your changes!");
        }

        private static Content retrieveContentFromProject(int? versionNumber = null)
        {
            GetContentRequest cr = projectResource.GetContent(ScriptID);
            if (versionNumber != null)
            {
                getVersions();
                if (versions.Find(v => v.VersionNumber == versionNumber.Value) != null)
                {
                    cr.VersionNumber = versionNumber.Value;
                }
                else
                {
                    throw new InfoException("Unable to find a version to retrieve project content from. Please ensure your version number corresponds to a version and is > 0.");
                }
            }
            return cr.Execute();
        }

        private static void createFileAndWrite(string filepath, string write)
        {
            try
            {
                if (Path.GetExtension(filepath).Equals(".html") && ParseHTMLScriptTagToJS) //check for script tags inside HTML!
                {
                    int indscriptTag = write.IndexOf(scriptTag);
                    int indendscriptTag = write.IndexOf(endscriptTag);
                    //check for a single script tag
                    if (indscriptTag >= 0 && indendscriptTag >= 0)
                    {
                        string newJSPath = getJavascriptPlaceholderFileName(filepath);
                        Debug.WriteLine("      Detected inner <script> tag. Moving contents to " + newJSPath);
                        string scriptSubstring = write.Substring(indscriptTag + scriptTag.Length, indendscriptTag - indscriptTag - endscriptTag.Length); //everything in script tag

                        string first = write.Substring(0, indscriptTag);
                        string last = write.Substring(indendscriptTag + endscriptTag.Length);

                        write = first + getJavascriptPlaceholder(newJSPath) + last; //update for javascript file
                        createFileAndWrite(Path.GetDirectoryName(filepath) + @"\" + newJSPath, scriptSubstring); //create new javascript file for you.
                    }
                }

                FileStream f = System.IO.File.Create(filepath);
                StreamWriter s = new StreamWriter(f);
                s.Write(write);
                s.Close();
                f.Close();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        /// <summary>
        /// Retrieves all the current source code from the project
        /// </summary>
        /// <param name="versionNumber">Optional to specifiy the version you want. This is really useful for going back in time.</param>
        private static void retrieveFiles(int? versionNumber = null)
        {
            Content content = retrieveContentFromProject(versionNumber);
            if (content == null)
            {
                throw new Exception("Failed to get content");
            }

            Debug.WriteLine("Content received");

            foreach (Google.Apis.Script.v1.Data.File file in content.Files)
            {
                string folder = getFolderFromFileType(file).FullName;
                string ext = getExtensionFromFileType(file);

                Debug.WriteLine("   {0}{1} {2}", file.Name, ext, file.Type);

                string path = folder + "\\" + file.Name + ext;

                createFileAndWrite(path, file.Source); //use the file's source code.
            }
        }

        #region Deployments
        /// <summary>
        /// Lists all of your deployments.
        /// These are connected to specific version numbers.
        /// </summary>
        private static void getDeployments()
        {
            var requestd = projectResource.Deployments.List(scriptID);
            requestd.PageSize = maxDownloadPageSize; //an absurdly high number
            var result = requestd.Execute();
            if (result.Deployments?.Count > 0)
                deployments = new List<Deployment>(result.Deployments);
        }

        private static void createNewWebAppDeployment()
        {
            try
            {
                webAppDeployment = projectResource.Deployments.Create(new DeploymentConfig()
                {
                    Description = webAppDeploymentString,
                    VersionNumber = getLatestVersion()
                }, scriptID).Execute();
            }
            catch
            {
                Debug.WriteLine("Failed to create web-app deployment");
            }
        }

        private static void updateDeploymentVersionNumber(int versionNumber)
        {
            if (versionNumber > 0)
            {
                webAppDeployment.DeploymentConfig.VersionNumber = versionNumber;

                projectResource.Deployments.Update(new UpdateDeploymentRequest()
                {
                    DeploymentConfig = webAppDeployment.DeploymentConfig
                }, scriptID, webAppDeployment.DeploymentId).Execute();
            }
        }

        private static void createVersionAndUpdateDeployment(string d)
        {
            int newVersion = createVersion(d).Value;
            updateDeploymentVersionNumber(newVersion);
        }

        private static void getWebAppDeployment()
        {
            getDeployments();
            webAppDeployment = null;
            deployments?.ForEach(d =>
            {
                if (d.DeploymentConfig.VersionNumber == null) //head deployment never has a version number!
                    headDeployment = d;

                if (d.DeploymentConfig.Description?.Equals(webAppDeploymentString) == true && webAppDeployment == null)
                {
                    Debug.WriteLine("Found Web App Deployment");
                    webAppDeployment = d;
                }
            });
            if (headDeployment == null)
            {
                Debug.WriteLine("Unable to find a head deployment. This is quite unexpected!");
            }
            if (webAppDeployment == null && AutoCreateWebappDeployment)
            {
                Debug.WriteLine("Unable to find web app deployment. Creating one for you now!");
                createNewWebAppDeployment();
            }
        }

        #endregion

        #region Versions
        private static int getLatestVersion()
        {
            getVersions();
            int latest = 1;
            foreach (var v in versions)
            {
                if (v.VersionNumber > latest)
                    latest = v.VersionNumber ?? 1;
            }
            return latest;
        }

        private static void getVersions()
        {
            var requestv = projectResource.Versions.List(scriptID);
            requestv.PageSize = maxDownloadPageSize; //an absurdly high number

            var result = requestv.Execute();
            bool found = result.Versions?.Count > 0;
            if (found)
                versions = new List<Google.Apis.Script.v1.Data.Version>(result.Versions);
            else //this is to prevent possible errors
                createVersion("First Version");
        }

        private static int? createVersion(string description)
        {
            var exec = projectResource.Versions.Create(new Google.Apis.Script.v1.Data.Version() { Description = description }, scriptID).Execute();

            if (versions == null)
                versions = new List<Google.Apis.Script.v1.Data.Version>();
            versions.Add(exec);

            return exec.VersionNumber;
        }
        #endregion
    }
}