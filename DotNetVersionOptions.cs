using System.Text.Json.Serialization;

namespace Sniffer.DotNetversion
{
    [JsonSerializable(typeof(DotNetVersionOptions))]
    public class DotNetVersionOptions
    {
        public bool SolutionsOnly { get; set; } = false;
        public string[]? ExcludePaths { get; set; }
        public string[]? Warn { get; set; }
        public string[]? Critical { get; set; }


        public static DotNetVersionOptions Default()
        {
            return new DotNetVersionOptions
            {
                Warn = new[] { "netcoreapp*", "net5.0" },
                Critical = new[] { "/v4\\.7(?!\\.2)|v4(?!\\.7)|v[1-3]/", "netcoreapp2.1" }
            };
        }
    }
}
