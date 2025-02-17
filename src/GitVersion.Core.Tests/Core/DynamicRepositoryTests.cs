using System.IO;
using GitVersion.Core.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace GitVersion.Core.Tests
{
    [TestFixture]
    public class DynamicRepositoryTests : TestBase
    {
        private string workDirectory;

        private static void ClearReadOnly(DirectoryInfo parentDirectory)
        {
            if (parentDirectory == null) return;
            parentDirectory.Attributes = FileAttributes.Normal;
            foreach (var fi in parentDirectory.GetFiles())
            {
                fi.Attributes = FileAttributes.Normal;
            }
            foreach (var di in parentDirectory.GetDirectories())
            {
                ClearReadOnly(di);
            }
        }

        [OneTimeSetUp]
        public void CreateTemporaryRepository()
        {
            // Note: we can't use guid because paths will be too long
            this.workDirectory = Path.Combine(Path.GetTempPath(), "GV");

            // Clean directory upfront, some build agents are having troubles
            if (Directory.Exists(this.workDirectory))
            {
                var di = new DirectoryInfo(this.workDirectory);
                ClearReadOnly(di);

                Directory.Delete(this.workDirectory, true);
            }

            Directory.CreateDirectory(this.workDirectory);
        }


        [OneTimeTearDown]
        public void Cleanup()
        {

        }

        // Note: use same name twice to see if changing commits works on same (cached) repository
        [NonParallelizable]
        [TestCase("GV_main", "https://github.com/GitTools/GitVersion", MainBranch, "4783d325521463cd6cf1b61074352da84451f25d", "4.0.0+1086")]
        [TestCase("GV_main", "https://github.com/GitTools/GitVersion", MainBranch, "3bdcd899530b4e9b37d13639f317da04a749e728", "4.0.0+1092")]
        public void FindsVersionInDynamicRepo(string name, string url, string targetBranch, string commitId, string expectedFullSemVer)
        {
            var root = Path.Combine(this.workDirectory, name);
            var dynamicDirectory = Path.Combine(root, "D"); // dynamic, keeping directory as short as possible
            var workingDirectory = Path.Combine(root, "W"); // working, keeping directory as short as possible
            var gitVersionOptions = new GitVersionOptions
            {
                RepositoryInfo =
                {
                    TargetUrl = url,
                    DynamicRepositoryClonePath = dynamicDirectory,
                    TargetBranch = targetBranch,
                    CommitId = commitId,
                },
                Settings = { NoFetch = false },
                WorkingDirectory = workingDirectory,
            };
            var options = Options.Create(gitVersionOptions);

            Directory.CreateDirectory(dynamicDirectory);
            Directory.CreateDirectory(workingDirectory);

            var sp = ConfigureServices(services => services.AddSingleton(options));

            var gitPreparer = sp.GetService<IGitPreparer>();
            gitPreparer?.Prepare();

            var gitVersionCalculator = sp.GetService<IGitVersionCalculateTool>();

            var versionVariables = gitVersionCalculator?.CalculateVersionVariables();

            Assert.AreEqual(expectedFullSemVer, versionVariables?.FullSemVer);
        }
    }
}
