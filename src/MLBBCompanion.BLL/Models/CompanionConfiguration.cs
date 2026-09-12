namespace MLBBCompanion.BLL.Models;

public class CompanionConfiguration
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5050;
    public string BlueStacksInstance { get; set; } = "Nougat32";
    public int DefaultAdbPort { get; set; } = 5555;
    public List<string> PackageNames { get; set; } = new()
    {
        "com.mobile.legends.usa",
        "com.mobile.legends"
    };
}
