namespace ResumableFunctions.Handler.UiService.InOuts
{
    public record ServiceInfo(int Id, string Name, string Url, string[] Dlls, DateTime Registration, DateTime LastScan)
    {
        public int LogErrors { get; set; }
        public int FunctionsCount { get; set; }
        public int MethodsCount { get; set; }
        public int PushedCallsCount { get; set; }
        public bool IsScanRunning { get; set; }
    }
}
