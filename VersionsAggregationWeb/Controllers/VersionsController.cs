using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.IO.Compression;
using LibGit2Sharp;
using System.Net.Sockets;

namespace VersionsAggregationWeb.Controllers
{
    [Route("[controller]")]
    public class VersionsController : Controller
    {
        private readonly IConfiguration _configuration;
        public VersionsController(IConfiguration configuration) => _configuration = configuration;

        [Route("/")]
        [Route("[action]")]
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [Route("[action]")]
        [HttpPost]
        public IActionResult Index(string githubRepoUrl, string versionsPath, string projectName, string rootFolderName)
        {
            string clonePath = @"C:\Users\Ali\Desktop\Practice\CloneFolder";

            if (!CloneRepositoryFromGithub(githubRepoUrl, clonePath))
                throw new InvalidOperationException(message: "Repository Url is incorrect!");

            string localPath = @"C:\Users\Ali\Desktop\Practice\CreationFolder";

            CreateLocalFolder(localPath, rootFolderName, projectName);

            CreateDatabaseFolders(localPath, rootFolderName, projectName);

            ChooseSubFolder(localPath, rootFolderName, clonePath, versionsPath, projectName);

            return File(ZipLocalFolder(localPath, rootFolderName), "application/zip", fileDownloadName: rootFolderName + ".zip");

        }

        private static bool CloneRepositoryFromGithub(string githubRepoUrl, string clonePath)
        {
            try
            {
                Repository.Clone(githubRepoUrl, clonePath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error ocurrred: {ex.Message}");
                return false;
            }
        }

        private string[] GetLocalFolderNames(string projectName)
        {
            return _configuration.GetSection($"databaseDirectories:{projectName}:Applications")
                                 .Get<string[]>()
                                 ?? throw new InvalidOperationException
                                 ("Configuration for Applications is missing or null.");
        }

        private string[] GetDatabaseFolderNames(string projectName)
        {
            return _configuration.GetSection($"databaseDirectories:{projectName}:databaseSubDirectories")
                                 .Get<string[]>()
                                 ?? throw new InvalidOperationException
                                 ("Configuration for Applications is missing or null.");
        }

        private void CreateLocalFolder(string localPath, string rootFolderName, string projectName)
        {
            string localFolderPath = Path.Combine(localPath, rootFolderName);

            if (Directory.Exists(localFolderPath))
                Directory.Delete(localFolderPath, true);

            Directory.CreateDirectory(localFolderPath);

            string[] localFolderSubDirectories = GetLocalFolderNames(projectName)!;

            foreach (string localFolderSubDirectory in localFolderSubDirectories)
                Directory.CreateDirectory(Path.Combine(localFolderPath, localFolderSubDirectory));
        }

        private void CreateDatabaseFolders(string localPath, string rootFolderName, string projectName)
        {
            string[] databaseSubDirectories = GetDatabaseFolderNames(projectName)!;

            foreach (string databaseSubDirectory in databaseSubDirectories)
            {
                string path = Path.Combine(localPath, rootFolderName, "Databases", databaseSubDirectory);

                if (Directory.Exists(path))
                    Directory.Delete(path, true);

                Directory.CreateDirectory(path);
            }
        }

        private void ChooseSubFolder(string localPath, string rootFolderName, string clonePath, string versionsPath, string projectName)
        {
            string[] versionDirectories = Directory.GetDirectories(Path.Combine(clonePath, versionsPath));
            string[] localFolderNames = GetLocalFolderNames(projectName);

            foreach (string versionDirectory in versionDirectories)
            {
                foreach (string versionSubDirectory in Directory.GetDirectories(versionDirectory))
                {
                    if (versionSubDirectory.Contains(localFolderNames[0]))
                        AggregateApplicationFolders(versionSubDirectory, Path.Combine(localPath, rootFolderName, localFolderNames[0]));

                    else if (versionSubDirectory.Contains(localFolderNames[1]))
                        AggregateDatabaseFolders(versionSubDirectory, Path.Combine(localPath, rootFolderName, localFolderNames[1]), projectName);

                    else if (versionSubDirectory.Contains(localFolderNames[2]))
                        AggregateReportFolders(versionSubDirectory, Path.Combine(localPath, rootFolderName, localFolderNames[2]));
                }
            }
        }

        private static void AggregateApplicationFolders(string srcPath, string destPath)
        {
            string[] applications = Directory.GetDirectories(srcPath);

            foreach (string application in applications)
            {
                string applicationDestPath = Path.Combine(destPath, Path.GetFileName(application));

                if (Directory.Exists(applicationDestPath))
                    Directory.Delete(applicationDestPath, true);

                Directory.Move(application, applicationDestPath);
            }
        }

        private void AggregateDatabaseFolders(string srcPath, string destPath, string projectName)
        {
            string[] databases = Directory.GetDirectories(srcPath);

            foreach (string database in databases)
            {
                string databaseName = Path.GetFileName(database);

                int slashIndex = databaseName.LastIndexOf('-');

                string databaseDirectoryName;

                if (slashIndex != -1)
                    databaseDirectoryName = databaseName.Substring(slashIndex + 1);
                else
                    databaseDirectoryName = databaseName;

                string databasesDestinationPath = Path
                    .Combine(destPath,
                     GetDatabaseFolderNames(projectName)
                    .First(databases => databases.Contains(databaseDirectoryName)));

                string[] databaseSubDirectories = Directory.GetDirectories(database);

                foreach (string databaseSubDirectory in databaseSubDirectories)
                {
                    string databaseSubDirectoryDestinationPath = Path.Combine(databasesDestinationPath, Path.GetFileName(databaseSubDirectory));

                    if (!Directory.Exists(databaseSubDirectoryDestinationPath))
                        Directory.CreateDirectory(databaseSubDirectoryDestinationPath);

                    string[] subDirectoryFiles = Directory.GetFiles(databaseSubDirectory);

                    foreach (string subDirectoryFile in subDirectoryFiles)
                    {
                        string subDirectoryFileDestinationPath = Path.Combine(databaseSubDirectoryDestinationPath, Path.GetFileName(subDirectoryFile));

                        if (System.IO.File.Exists(subDirectoryFileDestinationPath))
                            System.IO.File.Delete(subDirectoryFileDestinationPath);
                        System.IO.File.Move(subDirectoryFile, subDirectoryFileDestinationPath);
                    }
                }

                string[] databaseFiles = Directory.GetFiles(database);

                foreach (string databaseFile in databaseFiles)
                {
                    string fileDestinationPath = Path.Combine(databasesDestinationPath, Path.GetFileName(databaseFile));

                    if (System.IO.File.Exists(fileDestinationPath))
                        System.IO.File.Delete(fileDestinationPath);
                    System.IO.File.Move(databaseFile, fileDestinationPath);
                }
            }
        }

        private static void AggregateReportFolders(string srcPath, string destPath)
        {
            string[] reports = Directory.GetDirectories(srcPath);

            foreach (string report in reports)
            {
                string reportDestinationPath = Path.Combine(destPath, Path.GetFileName(report));

                if (Directory.Exists(reportDestinationPath))
                    Directory.Delete(reportDestinationPath, true);
                Directory.Move(report, reportDestinationPath);
            }
        }

        private static byte[] ZipLocalFolder(string localPath, string rootFolderName)
        {
            string zipFilePath = Path.Combine(localPath, rootFolderName + ".zip");

            if (System.IO.File.Exists(zipFilePath))
                System.IO.File.Delete(zipFilePath);

            ZipFile.CreateFromDirectory(Path.Combine(localPath, rootFolderName), zipFilePath);

            return System.IO.File.ReadAllBytes(zipFilePath);
        }
    }
}
