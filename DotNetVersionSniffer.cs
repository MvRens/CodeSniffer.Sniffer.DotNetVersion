using System.Text;
using System.Xml.Linq;
using CodeSniffer.Core.Sniffer;
using CodeSniffer.SnifferLib.Matchers;
using CodeSniffer.SnifferLib.VsProjects;
using Serilog;

namespace Sniffer.DotNetversion
{
    public class DotNetVersionSniffer : ICsSniffer
    {
        private readonly ILogger logger;
        private readonly DotNetVersionOptions options;

        private readonly IMatcher excludePathsMatcher;
        private readonly IMatcher criticalMatcher;
        private readonly IMatcher warningMatcher;


        public DotNetVersionSniffer(ILogger logger, DotNetVersionOptions options)
        {
            this.logger = logger;
            this.options = options;

            excludePathsMatcher = MatcherFactory.Create(options.ExcludePaths ?? Enumerable.Empty<string>());
            criticalMatcher = MatcherFactory.Create(options.Critical ?? Enumerable.Empty<string>());
            warningMatcher = MatcherFactory.Create(options.Warn ?? Enumerable.Empty<string>());
        }


        public async ValueTask<ICsReport?> Execute(string path, ICsScanContext context, CancellationToken cancellationToken)
        {
            var builder = CsReportBuilder.Create();
            var projectsEnumerator = new VsProjectsEnumerator(logger, builder, excludePathsMatcher);

            foreach (var project in projectsEnumerator.GetProjects(path, options.SolutionsOnly))
            {
                logger.Debug("Scanning project file {filename}", project.Filename);
                try
                {
                    var result = CsReportResult.Critical;
                    var targetFrameworks = new StringBuilder();
                    project.Asset.SetResult(CsReportResult.Critical, Strings.ResultNoTargetFrameworkVersion);

                    foreach (var targetFramework in await GetTargetFrameworks(project.Filename, cancellationToken))
                    {
                        if (targetFrameworks.Length > 0)
                            targetFrameworks.Append(", ");

                        targetFrameworks.Append(targetFramework);

                        if (criticalMatcher.Matches(targetFramework))
                        {
                            // Only critical if all frameworks are critical
                            if (result == CsReportResult.Critical)
                                project.Asset.SetResult(CsReportResult.Critical,
                                    string.Format(Strings.ResultCritical, targetFramework));
                        }
                        else if (warningMatcher.Matches(targetFramework))
                        {
                            // Only warning if all frameworks are either warning or critical
                            // ReSharper disable once InvertIf
                            if (result >= CsReportResult.Warning)
                            {
                                project.Asset.SetResult(CsReportResult.Warning,
                                    string.Format(Strings.ResultWarning, targetFramework));
                                result = CsReportResult.Warning;
                            }
                        }
                        else
                        {
                            // If any framework is valid, consider the check a success
                            project.Asset.SetResult(CsReportResult.Success, "");
                            result = CsReportResult.Success;
                        }
                    }

                    project.Asset.SetProperty("Target framework", targetFrameworks.ToString());
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error while parsing project file {filename}: {message}", project.Filename, e.Message);
                }
            }

            return builder.Build();
        }


        private async ValueTask<IEnumerable<string>> GetTargetFrameworks(string projectFile, CancellationToken cancellationToken)
        {
            XElement project;
            await using (var stream = new FileStream(projectFile, FileMode.Open, FileAccess.Read))
            {
                project = await XElement.LoadAsync(stream, LoadOptions.None, cancellationToken);
            }

            var versions = FindFirstNodeValue(project, "Project", "PropertyGroup", "TargetFrameworks");
            if (versions != null)
            {
                var versionValues = versions.Split(';').Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                if (versionValues.Count > 0)
                    return versionValues;
            }

            var version =
                FindFirstNodeValue(project, "Project", "PropertyGroup", "TargetFramework") ??
                FindFirstNodeValue(project, "Project", "PropertyGroup", "TargetFrameworkVersion");

            if (!string.IsNullOrWhiteSpace(version))
                return Enumerable.Repeat(version, 1);
            
            logger.Warning(
                "Could not find TargetFramework or TargetFrameworkVersion element in Project file {filename}, skipping",
                projectFile);

            return Enumerable.Empty<string>();
        }


        private static string? FindFirstNodeValue(XElement document, string documentElement, params string[] path)
        {
            if (path.Length == 0)
                return null;

            if (document.Name.LocalName != documentElement)
                return null;

            // XPath insists on using namespaces, this is easier and good enough
            return path
                .Aggregate(
                    new [] { document } as IEnumerable<XElement>, 
                    (current, part) => current.SelectMany(n => n.Elements().Where(e => e.Name.LocalName == part)))
                .Where(e => !string.IsNullOrWhiteSpace(e.Value))
                .Select(e => e.Value)
                .FirstOrDefault();
        }
    }
}
