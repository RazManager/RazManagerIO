using Microsoft.Extensions.Logging;


namespace RazManagerIO.Host.Services.CpuInfo
{
    public class CpuInfoService : ICpuInfoService
    {
        public CpuInfoDto CpuInfo { get; private set; }


        public CpuInfoService(ILogger<CpuInfoService> logger)
        {
            CpuInfo = new CpuInfoDto();

            try
            {
                var cpuInfoLines = System.IO.File.ReadAllLines("/proc/cpuinfo");
                foreach (var cpuInfoLine in cpuInfoLines)
                {
                    var pos = cpuInfoLine.IndexOf(": ");
                    if (pos > 0)
                    {
                        var key = cpuInfoLine.Substring(0, pos).Replace("\t", "");
                        var value = cpuInfoLine.Substring(pos + 2).Trim();

                        switch (key)
                        {
                            case "processor":
                                CpuInfo.Processor = value;
                                break;

                            case "model name":
                                CpuInfo.ModelName = value;
                                break;

                            case "Serial":
                                CpuInfo.Serial = value;
                                break;

                            case "Model":
                                CpuInfo.Model = value;
                                break;

                            default:
                                break;
                        }
                    }
                }
            }
            catch (System.Exception exception)
            {
                logger.LogError(exception, $"Could not read /proc/cpuinfo: {exception.Message}");
            }
        }
    }
}
