using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using LibGit2Sharp;

namespace VersionsAggregationWeb.Controllers
{
    [Route("[controller]")]
    public class VersionsController(IConfiguration configuration) : Controller
    {
        private readonly IConfiguration _configuration = configuration;

        [Route("/")]
        [Route("[action]")]
        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.ProjectName = _configuration.GetSection("Directories").GetChildren().Select(child => child.Key);
            return View();
        }

        [Route("[action]")]
        [HttpPost]
        public IActionResult Index(string gitOnlineService,
                                   string? username,
                                   string? appPassword,
                                   string repoUrl,
                                   string branch,
                                   string versionsPath,
                                   string? fromVersion,
                                   string? toVersion,
                                   string projectName)
        {
            string filesPath = CreateFilesDirectoryInProject();

            (string localPath, string clonePath, Guid guid) = CreateEachRequestDirectory(filesPath);

            if (username != null && appPassword != null)
                CloneRepository(gitOnlineService, repoUrl, clonePath, branch, username, appPassword);
            else
                CloneRepository(gitOnlineService, repoUrl, clonePath, branch);

            CreateLocalFolder(localPath, projectName);

            CreateDatabaseFolders(localPath, projectName);

            if (fromVersion != null && toVersion != null)
                ChooseSubFolder(localPath, clonePath, versionsPath, fromVersion, toVersion, projectName);
            else
                ChooseSubFolder(localPath, clonePath, versionsPath, projectName);

            byte[] fileContent = ZipLocalFolder(localPath);

            DeleteRequestDirectory(filesPath, guid);

            return File(fileContent, "application/zip", fileDownloadName: "Operation.zip");
        }

        private static void DeleteRequestDirectory(string filesPath, Guid guid)
        {
            string requestDirectoryPath = Path.Combine(filesPath, guid.ToString());

            Directory.GetFiles(requestDirectoryPath, "*", SearchOption.AllDirectories)
             .ToList()
             .ForEach(file => new FileInfo(file) { IsReadOnly = false });

            if (Directory.Exists(requestDirectoryPath))
               Directory.Delete(requestDirectoryPath, true);
        }

        private static string CreateFilesDirectoryInProject()
        {
            string currentDirectory = Directory.GetCurrentDirectory();

            string filesPath = Path.Combine(currentDirectory, "Files");

            Directory.CreateDirectory(filesPath);

            return filesPath;
        }

        private static (string, string, Guid) CreateEachRequestDirectory(string filesPath)
        {
            Guid guid = Guid.NewGuid();

            string creationPath = Path.Combine(filesPath, guid.ToString(), "CreationFolder");
            string clonePath = Path.Combine(filesPath, guid.ToString(), "CloneFolder");

            Directory.CreateDirectory(creationPath);
            Directory.CreateDirectory(clonePath);

            return (creationPath, clonePath, guid);
        }

        private string[] GetLocalFolderNames(string projectName)
        {
            return _configuration.GetSection($"Directories:{projectName}:Operations")
                                 .Get<string[]>()
                                 ?? throw new InvalidOperationException
                                 ("Configuration for Applications is missing or null.");
        }

        private string[] GetDatabaseFolderNames(string projectName)
        {
            return _configuration.GetSection($"Directories:{projectName}:DataBases")
                                 .Get<string[]>()
                                 ?? throw new InvalidOperationException
                                 ("Configuration for Applications is missing or null.");
        }

        private void CreateLocalFolder(string localPath, string projectName)
        {
            Directory.CreateDirectory(localPath);

            string[] localFolderSubDirectories = GetLocalFolderNames(projectName)!;

            foreach (string localFolderSubDirectory in localFolderSubDirectories)
                Directory.CreateDirectory(Path.Combine(localPath, localFolderSubDirectory));
        }

        private void CreateDatabaseFolders(string localPath, string projectName)
        {
            string[] databaseSubDirectories = GetDatabaseFolderNames(projectName)!;

            foreach (string databaseSubDirectory in databaseSubDirectories)
            {
                string path = Path.Combine(localPath, "DataBases", databaseSubDirectory);

                if (Directory.Exists(path))
                    Directory.Delete(path, true);

                Directory.CreateDirectory(path);
            }
        }

        private void ChooseSubFolder(string localPath, string clonePath, string versionsPath, string fromVersion, string toVersion, string projectName)
        {
            string[] localFolderNames = GetLocalFolderNames(projectName);

            string startDirectory = Path.Combine(clonePath, versionsPath, fromVersion);
            string endDirectory = Path.Combine(clonePath, versionsPath, toVersion);

            ReadOnlySpan<string> versionDirectories = GetDirectoriesInRange(Directory.GetDirectories(Path.Combine(clonePath, versionsPath)), startDirectory , endDirectory);
            
            foreach (string versionDirectory in versionDirectories)
            {
                foreach (string versionSubDirectory in Directory.GetDirectories(versionDirectory))
                {
                    if (versionSubDirectory.Contains(localFolderNames[0]))
                        AggregateApplicationFolders(versionSubDirectory, Path.Combine(localPath, localFolderNames[0]));

                    else if (versionSubDirectory.Contains(localFolderNames[1]))
                        AggregateDatabaseFolders(versionSubDirectory, Path.Combine(localPath, localFolderNames[1]), projectName);

                    else if (versionSubDirectory.Contains(localFolderNames[2]))
                        AggregateReportFolders(versionSubDirectory, Path.Combine(localPath, localFolderNames[2]));
                }
            }
        }
        
        private void ChooseSubFolder(string localPath, string clonePath, string versionsPath, string projectName)
        {
            string[] localFolderNames = GetLocalFolderNames(projectName);

            string[] versionDirectories = Directory.GetDirectories(Path.Combine(clonePath, versionsPath));
            
            foreach (string versionDirectory in versionDirectories)
            {
                foreach (string versionSubDirectory in Directory.GetDirectories(versionDirectory))
                {
                    if (versionSubDirectory.Contains(localFolderNames[0]))
                        AggregateApplicationFolders(versionSubDirectory, Path.Combine(localPath, localFolderNames[0]));

                    else if (versionSubDirectory.Contains(localFolderNames[1]))
                        AggregateDatabaseFolders(versionSubDirectory, Path.Combine(localPath, localFolderNames[1]), projectName);

                    else if (versionSubDirectory.Contains(localFolderNames[2]))
                        AggregateReportFolders(versionSubDirectory, Path.Combine(localPath, localFolderNames[2]));
                }
            }
        }

        private static ReadOnlySpan<string> GetDirectoriesInRange(string[] directories, string start, string end)
        {
            ReadOnlySpan<string> span = new(directories);

            int startIndex = span.IndexOf(start);
            int endIndex = span.IndexOf(end);

            if (startIndex == -1)
                throw new ArgumentException("Start directory not found.");
            
            else if (endIndex == -1)
                throw new ArgumentException("End directory not found.");
            
            else if (endIndex < startIndex)
                throw new ArgumentException("End directory must come after start directory.");


            return span.Slice(startIndex, endIndex - startIndex + 1);
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
                    databaseDirectoryName = databaseName[(slashIndex + 1)..];
                else
                    databaseDirectoryName = databaseName;

                string databasesDestinationPath = Path.Combine(destPath,GetDatabaseFolderNames(projectName)
                    .FirstOrDefault(databases => databases.Contains(databaseDirectoryName),
                                    databaseDirectoryName + "(Unknown)"));

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

        private static byte[] ZipLocalFolder(string localPath)
        {
            string zipFilePath = localPath + ".zip";

            if (System.IO.File.Exists(zipFilePath))
                System.IO.File.Delete(zipFilePath);

            ZipFile.CreateFromDirectory(localPath, zipFilePath);

            return System.IO.File.ReadAllBytes(zipFilePath);
        }

        private static void CloneRepository(string gitOnlineService, string repoUrl, string clonePath, string branch)
        {
            switch (gitOnlineService)
            {
                case "github":
                    GithubCloneRepository(repoUrl, clonePath, branch);
                    break;
                case "bitbucket":
                    BitbucketCloneRepository(repoUrl, clonePath, branch);
                    break;
                default:
                    break;
            }
        }
        
        private static void CloneRepository(string gitOnlineService, string repoUrl, string clonePath, string branch, string username, string appPassword)
        {
            switch (gitOnlineService)
            {
                case "github":
                    GithubCloneRepository(repoUrl, clonePath, branch, username, appPassword);
                    break;
                case "bitbucket":
                    BitbucketCloneRepository(repoUrl, clonePath, branch, username, appPassword);
                    break;
                default:
                    break;
            }
        }

        private static void BitbucketCloneRepository(string repositoryUrl, string clonePath, string branch)
        {
            CloneOptions cloneOptions = new() { BranchName = branch };

            Repository.Clone(repositoryUrl, clonePath, cloneOptions);
        }
        
        private static void BitbucketCloneRepository(string repositoryUrl, string clonePath, string branch, string username, string appPassword)
        {
            CloneOptions cloneOptions = GetCloneOptionsWithCredentials(username, appPassword);
            cloneOptions.BranchName = branch;

            Repository.Clone(repositoryUrl, clonePath, cloneOptions);
        }

        private static void GithubCloneRepository(string repositoryUrl, string clonePath, string branch)
        {
            CloneOptions cloneOptions = new() { BranchName = branch };

            Repository.Clone(repositoryUrl, clonePath, cloneOptions);
        }
        
        private static void GithubCloneRepository(string repositoryUrl, string clonePath, string branch, string username, string appPassword)
        {
            CloneOptions cloneOptions = GetCloneOptionsWithCredentials(username, appPassword);
            cloneOptions.BranchName = branch;

            Repository.Clone(repositoryUrl, clonePath, cloneOptions);
        }

        private static CloneOptions GetCloneOptionsWithCredentials(string username, string appPassword)
        {
            CloneOptions cloneOptions = new();
            cloneOptions.FetchOptions.CredentialsProvider = (_url, _user, _cre) => new UsernamePasswordCredentials
            {
                Username = username,
                Password = appPassword
            };

            return cloneOptions;
        }
    }
}
