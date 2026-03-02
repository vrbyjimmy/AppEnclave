namespace AppEnclave
{
    public class AppEnclaveOptions
    {
        public IEnumerable<string> Hosts { get; set; } = new List<string>();
        public ITenantPlugin Plugin { get; set; } = null;
        public string Name { get; set; } = string.Empty;
        public string EnvironmentName{ get; set; } = string.Empty;
        public string ContentRoot { get; set; } = string.Empty;
        public string BinRoot { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool UseAuthentication { get; set; } = false;
        public bool AllowSubAppsOnSameHost { get; set; } = false;
    }
}
