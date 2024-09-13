using Microsoft.Extensions.Configuration;
using Microsoft.Toolkit.Uwp.Notifications;
using StockService.Model;
using StockService.Services;
using System.Text.Json;

var builder = new ConfigurationBuilder().AddJsonFile($"appsettings.json", true, true).AddJsonFile($"stocklookup.json", true, true).Build();
var config = builder.GetSection("Config").Get<Config>();
var stockLookup = builder.GetSection("StockLookUp").Get<List<StockDetail>>();

ConsolidateAlertService consolidateAlertService = new(new NseService(config.NSEFilter), 
    new BseService(config.BSEFilter), 
    stockLookup.Where(a=>a.MarketCap > 1000).ToList());
AlertProcessorService alertProcessorService = new();
SoundService soundService = new(config.muteAlertSound, config.loopAlert);

//Console.WriteLine($"config:{JsonSerializer.Serialize(config)}");

Console.WriteLine("NSE(Default) 1; BSE 2;");
AlertSource nseOrBse = Console.ReadLine() == "2" ? AlertSource.BSE : AlertSource.NSE;
int alertNo = 1;
while (true)
{
    try
    {
        WriteToConsole(".", textColor: ConsoleColor.Green);
        Thread.Sleep((int)config.refreshIntervalMs);

        List<AnnouncementAlert> corpAnnouncements = new List<AnnouncementAlert>();
        corpAnnouncements = (await consolidateAlertService.GetAnnouncementAlerts(nseOrBse)).ToList();

        bool isNewAlertsAvailable = false;
        var newAlerts = alertProcessorService.Push(corpAnnouncements, out isNewAlertsAvailable);

        if (isNewAlertsAvailable)
        {
            soundService.PlaySound();
            ShowToaster(newAlerts);
            Console.WriteLine("");
            Console.WriteLine("**********************************************************************************");
            Console.WriteLine($"*********************************NEW ALERTS START {DateTime.Now.ToLongTimeString()}*********************************");
            Console.WriteLine("**********************************************************************************");

            foreach (AnnouncementAlert alert in newAlerts) {
                Console.WriteLine("-------------------------------------------------------------------------------------------------------");
                Console.WriteLine($"{alertNo}) alert time:{DateTime.Now.ToLongTimeString()}, source time:{alert.date.ToLongTimeString()}, ex dis time:{alert.exchDisseminateDate.ToLongTimeString()}");
                WriteToConsole($"{alert.stock_code} {alert.scrip_code} {alert.stock_name}", textColor:ConsoleColor.DarkYellow);
                Console.WriteLine($" | {alert.type}{(alert.sub_type != null ? " - " : "")}{alert.sub_type}");
                Console.WriteLine($"{alert.subject}");
                Console.WriteLine($"{alert.announcement_file_url}");
                alertNo++;
            }

            Console.WriteLine("**********************************************************************************");
            Console.WriteLine("*********************************NEW ALERTS END***********************************");
            Console.WriteLine("**********************************************************************************");

            WriteToConsole($"{nseOrBse} ALERT!!!!!! {DateTime.Now.ToString("hh:mm:ss tt")}", bgColor: ConsoleColor.Red, writeNewLine:true);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{ex.Message}");
    }
}

void WriteToConsole(string text, ConsoleColor textColor = ConsoleColor.White, ConsoleColor bgColor = ConsoleColor.Black, bool writeNewLine = false)
{
    Console.BackgroundColor = bgColor;
    Console.ForegroundColor = textColor;
    Console.Write(text);
    Console.BackgroundColor = ConsoleColor.Black;
    Console.ForegroundColor = ConsoleColor.White;

    if (writeNewLine) Console.WriteLine("");
}

void RemovePrevLine()
{
    Console.SetCursorPosition(0, Console.CursorTop - 1);
}

void ShowToaster(List<AnnouncementAlert> alerts)
{
    if (config.enableWindowsNotifications)
    {
        foreach (var alert in alerts.TakeLast(5))
        {
            var toaster = new ToastContentBuilder()
         //.AddArgument("action", "viewConversation")
         //.AddArgument("conversationId", 9813)
         .AddText($"{alert.alertSource.ToString().Substring(0, 2)}:{alert.stock_code}")
         .SetToastDuration(ToastDuration.Long);

            if(alert.announcement_file_url.Contains(".com"))
                toaster.SetProtocolActivation(new Uri(alert.announcement_file_url));
       //.AddText("Check this out, The Enchantments in Washington!")
            toaster.Show();
        }
    }
}

//[DllImport("user32.dll")]
//[return: MarshalAs(UnmanagedType.Bool)]
//static extern bool SetForegroundWindow(IntPtr hWnd);

//[DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
//static extern IntPtr FindWindowByCaption(IntPtr zeroOnly, string lpWindowName);
//void BringAppToFront()
//{
//    string originalTitle = Console.Title;
//    string uniqueTitle = Guid.NewGuid().ToString();
//    Console.Title = uniqueTitle;
//    Thread.Sleep(50);
//    IntPtr handle = FindWindowByCaption(IntPtr.Zero, uniqueTitle);

//    if (handle == IntPtr.Zero)
//    {
//        Console.WriteLine("Oops, cant find main window.");
//        return;
//    }
//    Console.Title = originalTitle;

//    SetForegroundWindow(handle);
//}
