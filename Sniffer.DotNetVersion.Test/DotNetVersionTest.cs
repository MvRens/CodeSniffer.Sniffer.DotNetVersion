using System.Diagnostics;
using System.Reflection;
using CodeSniffer.Core.Sniffer;
using FluentAssertions;
using FluentAssertions.Execution;
using Serilog;
using Sniffer.DotNetVersion;
using Xunit;
using Xunit.Abstractions;

namespace Sniffer.DotNetVersion.Test
{
    public class DotNetVersionTest
    {
        private readonly ILogger logger;
        private readonly string testDataPath;


        public DotNetVersionTest(ITestOutputHelper testOutputHelper)
        {
            logger = new LoggerConfiguration()
                .WriteTo.TestOutput(testOutputHelper)
                .CreateLogger();

            testDataPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "data"), Assembly.GetExecutingAssembly().Location);
            if (!Directory.Exists(testDataPath))
                throw new Exception($"Test data path not found: {testDataPath}");
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Test(bool solutionsOnly)
        {
            var sniffer = new DotNetVersionSniffer(logger, new DotNetVersionOptions
            {
                SolutionsOnly = solutionsOnly,
                Warn = new [] { "net6.0" },
                Critical = new[] { "net472", "v4.7.2" },
                ExcludePaths = new [] { "ExcludePath" }
            });

            var report = await sniffer.Execute(testDataPath, new TestContext(), CancellationToken.None);
            Debug.Assert(report != null);

            CheckAsset(report, "NET47.csproj", "NET47.csproj", CsReportResult.Critical);
            CheckAsset(report, "NET6.csproj", "NET6.csproj", CsReportResult.Warning);
            CheckAsset(report, "NETCoreMultiTarget.csproj", "NETCoreMultiTarget.csproj", CsReportResult.Success);
            CheckAsset(report, "NETFrameworkMultiTarget.csproj", "NETFrameworkMultiTarget.csproj", CsReportResult.Success);

            if (solutionsOnly)
                CheckNoAsset(report, "ProjectOutsideSolution.csproj");
            else
                CheckAsset(report, "ProjectOutsideSolution.csproj", "ProjectOutsideSolution.csproj", CsReportResult.Success);
        }


        private static void CheckAsset(ICsReport report, string assetId, string expectedName, CsReportResult expectedResult)
        {
            var asset = report.Assets.Should().Contain(a => a.Id == assetId, $"{assetId} should be present").Which;

            using (new AssertionScope())
            {
                asset.Name.Should().Be(expectedName, assetId);
                asset.Result.Should().Be(expectedResult, assetId);
            }
        }


        private static void CheckNoAsset(ICsReport report, string assetId)
        {
            report.Assets.Should().NotContain(a => a.Id == assetId, $"{assetId} should not be present");
        }


        private class TestContext : ICsScanContext
        {
            public string BranchName => "test";
        }
    }
}