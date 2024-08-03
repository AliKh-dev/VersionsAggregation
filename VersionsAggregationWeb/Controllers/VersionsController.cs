using Microsoft.AspNetCore.Mvc;
using System.Text;
using LibGit2Sharp;

namespace VersionsAggregationWeb.Controllers
{
    [Route("[controller]")]
    public class VersionsController : Controller
    {
        [Route("/")]
        [Route("[action]")]
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [Route("[action]")]
        [HttpPost]
        public IActionResult Index(string githubRepoUrl, string versionsPath, string rootFolderName)
        {
            #region Github Clone
            string repoUrl = githubRepoUrl;
            string clonePath = @"C:\Users\Ali\Desktop\Practice\CloneFolder";

            try
            {
                Repository.Clone(repoUrl, clonePath);
                Console.WriteLine($"Repository Clone to: {clonePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error ocurrred: {ex.Message}");
            }
            #endregion

            #region Path and Name
            string localPath = @"C:\Users\Ali\Desktop\Practice\CreationFolder";
            string localFolderName = '\\' + rootFolderName;
            #endregion

            #region Operation Directory
            StringBuilder creationPath = new(localPath);

            if (!Directory.Exists(creationPath.Append(localFolderName).ToString()))
                Directory.CreateDirectory(creationPath.ToString());

            // Applications, DataBases, Reports Directories
            string[] localFolderSubDirectories = [@"\Applications", @"\DataBases", @"\Reports"];

            foreach (string localFolderSubDirectory in localFolderSubDirectories)
            {
                if (!Directory.Exists(creationPath.Append(localFolderSubDirectory).ToString()))
                    Directory.CreateDirectory(creationPath.ToString());
                creationPath.Replace(localFolderSubDirectory, string.Empty);
            }

            // Folders inside DataBase Folder
            string[] databaseSubDirectories = ["\\1-Counterparty", "\\2-AccountManagement", "\\3-CCS_Oracle_views", "\\4-Common"];
            creationPath.Append(localFolderSubDirectories[1]);

            foreach (string databaseSubDirectory in databaseSubDirectories)
            {
                if (!Directory.Exists(creationPath.Append(databaseSubDirectory).ToString()))
                    Directory.CreateDirectory(creationPath.ToString());
                creationPath.Replace(databaseSubDirectory, string.Empty);
            }
            creationPath.Replace(localFolderSubDirectories[1], string.Empty);
            #endregion

            #region Version Directory
            string[] versionDirectories = Directory.GetDirectories(clonePath + '\\' + versionsPath);

            foreach (string versionDirectory in versionDirectories)
            {
                foreach (string versionSubDirectory in Directory.GetDirectories(versionDirectory))
                {
                    if (versionSubDirectory.Contains("Applications"))
                    {
                        string[] applications = Directory.GetDirectories(versionSubDirectory);
                        creationPath.Append(localFolderSubDirectories[0]);

                        foreach (string application in applications)
                        {
                            string applicationsDestinationPath = new(creationPath.ToString() + '\\' + Path.GetFileName(application));

                            if (Directory.Exists(applicationsDestinationPath))
                                Directory.Delete(applicationsDestinationPath, true);
                            Directory.Move(application, applicationsDestinationPath);
                        }
                        creationPath.Replace(localFolderSubDirectories[0], string.Empty);
                    }

                    if (versionSubDirectory.Contains("DataBases"))
                    {
                        string[] databases = Directory.GetDirectories(versionSubDirectory);
                        creationPath.Append(localFolderSubDirectories[1]);

                        foreach (string database in databases)
                        {
                            int slashIndex = database.LastIndexOf('-');
                            string databaseDirectoryName;
                            if (slashIndex != -1)
                                databaseDirectoryName = database.Substring(slashIndex + 1);
                            else
                                databaseDirectoryName = Path.GetFileName(database);

                            string databasesDestinationPath = Directory.GetDirectories(creationPath.ToString()).First(path => Path.GetFileName(path).Contains(databaseDirectoryName));

                            #region RollBack Directory
                            string[] databaseSubDirectoriesSrc = Directory.GetDirectories(database);
                            foreach (string databaseSubDirectory in databaseSubDirectoriesSrc)
                            {
                                string databaseSubDirectoryDestinationPath = new(databasesDestinationPath + '\\' + Path.GetFileName(databaseSubDirectory));

                                if (!Directory.Exists(databaseSubDirectoryDestinationPath))
                                    Directory.CreateDirectory(databaseSubDirectoryDestinationPath);

                                string[] subDirectoryFiles = Directory.GetFiles(databaseSubDirectory);
                                foreach (string subDirectoryFile in subDirectoryFiles)
                                {
                                    string subDirectoryFileDestinationPath = new(databaseSubDirectoryDestinationPath + '\\' + Path.GetFileName(subDirectoryFile));

                                    if (System.IO.File.Exists(subDirectoryFileDestinationPath))
                                        System.IO.File.Delete(subDirectoryFileDestinationPath);
                                    System.IO.File.Move(subDirectoryFile, subDirectoryFileDestinationPath);
                                }
                            }
                            #endregion

                            #region Files
                            string[] databaseFiles = Directory.GetFiles(database);
                            foreach (string databaseFile in databaseFiles)
                            {
                                string fileDestinationPath = new(databasesDestinationPath + '\\' + Path.GetFileName(databaseFile));

                                if (System.IO.File.Exists(fileDestinationPath))
                                    System.IO.File.Delete(fileDestinationPath);
                                System.IO.File.Move(databaseFile, fileDestinationPath);
                            }
                            #endregion
                        }

                        creationPath.Replace(localFolderSubDirectories[1], string.Empty);
                    }

                    if (versionSubDirectory.Contains("Reports"))
                    {
                        string[] reports = Directory.GetDirectories(versionSubDirectory);
                        creationPath.Append(localFolderSubDirectories[2]);

                        foreach (string report in reports)
                        {
                            string reportDestinationPath = new(creationPath.ToString() + '\\' + Path.GetFileName(report));

                            if (Directory.Exists(reportDestinationPath))
                                Directory.Delete(reportDestinationPath, true);
                            Directory.Move(report, reportDestinationPath);
                        }
                        creationPath.Replace(localFolderSubDirectories[2], string.Empty);
                    }
                }
            }
            #endregion

            return View();
        }
    }
}
