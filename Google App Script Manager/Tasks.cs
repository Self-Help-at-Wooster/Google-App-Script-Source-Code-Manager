using Google.Apis.Script.v1.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AppScriptManager
{
    public static partial class AppScriptSourceCodeManager
    {
        /// <summary>
        /// Clears all current Google resources
        /// Removes stored oauth credentials from your files.
        /// This will necessitate authorization to reuse this library.
        /// </summary>
        public static Task<TaskInfo<string>> ClearCredentials()
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                try
                {
                    credential = null;
                    scriptService = null;
                    projectResource = null;
                    webAppDeployment = null;
                    curProject = null;
                    scriptID = null;
                    versions = null;
                    deployments = null;
                    headDeployment = null;
                    webAppDeployment = null;

                    if (System.IO.File.Exists(lastScriptID))
                        System.IO.File.Delete(lastScriptID);

                    Debug.WriteLine("Local Information Cleared. Looking for token.json credentials path...");

                    if (Directory.Exists(CredentialsPath))
                    {
                        Directory.Delete(CredentialsPath, true);
                        Debug.WriteLine("Credentials Cleared!");
                    }

                    return new TaskInfo<string>("Completed removing your credentials. Please log in again to regain access.");
                }
                catch (Exception ex)
                {
                    return new TaskInfo<string>(getExceptionString(ex, "Error clearing your credentials."), false);
                }
            }
            );
        }

        /// <summary>
        /// Initializes the library
        /// You will have to provide a script ID, either by pasting one or creating a new GAS project.
        /// </summary>
        /// <param name="Source">Provide where your solution folder is</param>
        public static Task<TaskInfo<string>> Initialize(string Source)
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                try
                {
                    var x = initUser();
                    createSourceCodeDirectories(Source);
                    x.Wait();
                    loadLastScriptID();
                    return new TaskInfo<string>(x.Result);
                }
                catch (AggregateException ae)
                {
                    string result = "";
                    int i = 0;
                    ae = ae.Flatten();
                    foreach (Exception ex in ae.InnerExceptions)
                    {
                        if (ex is InfoException)
                        {
                            if (i < ae.InnerExceptions.Count - 1)
                                result += ex.Message + Environment.NewLine;
                            else
                                result += ex.Message + Environment.NewLine;
                        }

                        i++;
                    }
                    return new TaskInfo<string>(result, false);
                }
                catch (Exception ex)
                {
                    return new TaskInfo<string>(getExceptionString(ex, "Initialization Failed!"), false);
                }
            });
        }



        /// <summary>
        /// Downloads the source files into your directory.
        /// Note that this will overwrite your existing files in the case of duplicates.
        /// </summary>
        /// <param name="VersionNumber">(Optional) The Version Number of which to download the Source Code.</param>
        public static Task<TaskInfo<string>> DownloadFiles(int? VersionNumber = null)
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                if (libraryReady)
                {
                    try
                    {
                        sourceCodeBackup();
                        retrieveFiles(VersionNumber);
                    }
                    catch (Exception ex)
                    {
                        return new TaskInfo<string>(getExceptionString(ex, "Error downloading your Source Code Files"), false);
                    }
                    return new TaskInfo<string>("Source Code Download Complete!");
                }
                return libraryUnitializedInfo;
            }
            );
        }

        /// <summary>
        /// Uploads your entire SourceCode directory files back to Google Apps Script.
        /// This will only sync your .js, .html, and appsscript.json files from the folders:
        /// SourceCode/JAVASCRIPT, SourceCode/HTML, and SourceCode/JSON.
        /// Note that you should never delete your appsscript.json, or this call will not function properly.
        /// However, you may call CreateNewAppsScriptManifestJSONFile() to generate a possible fix.
        /// Any subfolders or extraneous file types not mentioned here will not synchronize.
        /// Files in the SourceCode folder alone will not be checked for upload.
        /// Please note that this function will destroy all your source files on your Google Apps Script project.
        /// Therefore, please ensure that you backup any desirable files from your online project prior to calling this function.
        /// </summary>
        public static Task<TaskInfo<string>> SyncChanges()
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                if (libraryReady)
                {
                    try
                    {
                        uploadFiles();
                        return new TaskInfo<string>("File Upload (Sync) Complete");
                    }
                    catch (Exception ex)
                    {
                        return new TaskInfo<string>(getExceptionString(ex, "Unable to upload. Check Debug (Immediate Window) to view message."), false);
                    }
                }
                return libraryUnitializedInfo;
            }
            );
        }

        /// <summary>
        /// This function creates a backup version of your script, then uploads your changes.
        /// </summary>
        public static Task<TaskInfo<string>> PreVersionAndSyncChanges()
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                if (libraryReady)
                {
                    try
                    {
                        createVersion("Autosaved Version");
                        uploadFiles();
                        return new TaskInfo<string>("File Upload (Sync) Complete");
                    }
                    catch (Exception ex)
                    {
                        return new TaskInfo<string>(getExceptionString(ex, "Unable to version and upload. Check Debug (Immediate Window) to view message."), false);
                    }
                }
                return libraryUnitializedInfo;
            }
            );
        }

        /// <summary>
        /// Creates a new Google Apps Script Project in your drive.
        /// Stores your Script ID for future usage.
        /// Also creates a web-app deployment for you.
        /// </summary>
        /// <param name="Name">The Name of this project</param>
        public static Task<TaskInfo<string>> CreateNewGASProject(string Name)
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                if (scriptService == null)
                    return libraryUnitializedInfo;
                if (!string.IsNullOrEmpty(Name))
                {
                    try
                    {
                        createGoogleAppsScriptProject(Name, projectResource);

                        Debug.WriteLine("Project Creation Succeeded!");

                        return new TaskInfo<string>("Project Creation Complete");
                    }
                    catch (Exception ex)
                    {
                        return new TaskInfo<string>(getExceptionString(ex, "Unable to create a new project."));
                    }
                }
                return new TaskInfo<string>("Invalid Project Name!");
            }
            );
        }

        /// <summary>
        /// Creates a new source file in your project.
        /// Do not call this function to create JSON files. You may only create a JSON manifest file using 
        /// createNewAppsScriptManifestJSONFile()
        /// </summary>
        /// <param name="Name">The file name</param>
        /// <param name="Type">The file type you are adding</param>
        /// <param name="Sync">Sync current changes with new file to drive</param>
        public static Task<TaskInfo<string>> AddNewSourceFile(string Name, FILE_TYPES Type, bool Sync)
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                try
                {
                    if (Sync)
                    {
                        if (libraryReady)
                        {
                            createFile(Name, Type);
                            uploadFiles();
                        }
                        return libraryUnitializedInfo;
                    }
                    else
                        createFile(Name, Type);

                    return new TaskInfo<string>("Source File Creation Complete");
                }
                catch (Exception ex)
                {
                    return new TaskInfo<string>(getExceptionString(ex, "Unable to create source code file."));
                }
            }
            );
        }

        /// <summary>
        /// Only use this function if you've accidentally deleted your appsscript.json file.
        /// This action will get only the manifest from the current Self-Help webapp.
        /// It may not reflect the purview of what your webapp needs depending on the changes you've made.
        /// Read more here: https://developers.google.com/apps-script/concepts/manifests
        /// </summary>
        public static Task<TaskInfo<string>> CreateNewAppsScriptManifestJSONFile()
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                try
                {
                    retrieveJSONManifestFile(true);
                    return new TaskInfo<string>("Manifest File (appsscript.json) Creation Complete." + Environment.NewLine + "You may now try uploading your source code again.");
                }
                catch (Exception ex)
                {
                    return new TaskInfo<string>(getExceptionString(ex, "Unable to retrieve Self-Help's manifest source file."));
                }
            }
            );
        }

        /// <summary>
        /// Because these projects are web apps, letting the coder chose the deployment is not sensible.
        /// THIS IS FOR DEBUGGING ONLY.
        /// </summary>
        public static Task<TaskInfo<List<Deployment>>> ListDeployments()
        {
            return Task<TaskInfo<List<Deployment>>>.Factory.StartNew(() =>
            {
                if (libraryReady)
                {
                    try
                    {
                        getDeployments();
                        return new TaskInfo<List<Deployment>>(deployments);
                    }
                    catch (Exception ex)
                    {
                        return new TaskInfo<List<Deployment>>(null, false, getExceptionString(ex, "Failed to get deployments!"));
                    }
                }
                return new TaskInfo<List<Deployment>>(null, false, libraryUnitializedInfo.MyResult);
            }
            );
        }

        /// <summary>
        /// Retrieves the versions of your project.
        /// </summary>
        /// <returns>List of your versions.</returns>
        public static Task<TaskInfo<List<Google.Apis.Script.v1.Data.Version>>> ListVersions()
        {
            return Task<TaskInfo<List<Google.Apis.Script.v1.Data.Version>>>.Factory.StartNew(() =>
            {
                if (libraryReady)
                {
                    try
                    {
                        getVersions();
                        return new TaskInfo<List<Google.Apis.Script.v1.Data.Version>>(versions);
                    }
                    catch (Exception ex)
                    {
                        return new TaskInfo<List<Google.Apis.Script.v1.Data.Version>>(null, false, getExceptionString(ex, "Failed to get versions!"));
                    }
                }
                return new TaskInfo<List<Google.Apis.Script.v1.Data.Version>>(null, false, libraryUnitializedInfo.MyResult);
            }
           );
        }

        /// <summary>
        /// Creates a new "commit" version with your given description.
        /// The description should not be null or empty.
        /// Note that this will not automatically update your current web-app deployment to this version.
        /// Please run CreateVersionAndUpdateDeployment() to perform both actions.
        /// </summary>
        /// <param name="Description"></param>
        public static Task<TaskInfo<string>> CreateVersion(string Description)
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                if (libraryReady)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(Description))
                        {
                            int num = createVersion(Description).Value;
                            return new TaskInfo<string>("Created Version Number " + num);
                        }
                        else
                            return new TaskInfo<string>("Invalid description!", false);
                    }
                    catch (Exception ex)
                    {
                        return new TaskInfo<string>(getExceptionString(ex, "Unable to create a new version."), false);
                    }
                }
                return libraryUnitializedInfo;
            });
        }

        /// <summary>
        /// This action will change your web-app's corresponding version number.
        /// Note, that this will not change your source code on Google Apps Script or in Visual Studio.
        /// This action simply changes to the code snapshot on GAS of said version.
        /// Should you want to look at the code for this version,
        /// then run DownloadFiles(int) and provide a version number!
        /// </summary>
        /// <param name="VersionNumber">The non-zero, non-negative version number you desire to deploy</param>
        public static Task<TaskInfo<string>> UpdateDeploymentVersionNumber(int VersionNumber)
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                if (libraryReady)
                {
                    try
                    {
                        updateDeploymentVersionNumber(VersionNumber);
                        return new TaskInfo<string>("Updated Web-App Deployment to Version Number " + VersionNumber);
                    }
                    catch (Exception ex)
                    {
                        return new TaskInfo<string>(getExceptionString(ex, "Unable to update deployment's version number."), false);
                    }
                }
                return libraryUnitializedInfo;
            });
        }

        /// <summary>
        /// Run this function when you want to fully deploy a new live version to the exec link.
        /// </summary>
        /// <param name="Description"></param>
        public static Task<TaskInfo<string>> CreateNewVersionAndUpdateDeployment(string Description)
        {
            return Task<TaskInfo<string>>.Factory.StartNew(() =>
            {
                if (libraryReady)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(Description))
                        {
                            createVersionAndUpdateDeployment(Description);
                            return new TaskInfo<string>("Created new version and deployment!");
                        }
                        else
                        {
                            return new TaskInfo<string>("Invalid Description!", false);
                        }
                    }
                    catch (Exception ex)
                    {
                        return new TaskInfo<string>(getExceptionString(ex, "Unable to create a new version or deployment."));
                    }
                }
                return libraryUnitializedInfo;
            });
        }
    }
}
