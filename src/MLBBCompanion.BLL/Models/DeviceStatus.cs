namespace MLBBCompanion.BLL.Models;

public class DeviceStatus
{
    public bool Bluestacks { get; set; }
    public bool GameRunning { get; set; }
    public string Status { get; set; } = "offline";
    public bool CloseTab { get; set; }
    public bool BrowserOpened { get; set; }
}
