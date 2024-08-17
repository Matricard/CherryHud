using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace MacTemp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private string _gpuTemp = "";

    public string GpuTemp
    {
        get => _gpuTemp;
        set => SetProperty(ref _gpuTemp, value);
    }
    private string _cpuTemp = "";

    public string CpuTemp
    {
        get => _cpuTemp;
        set => SetProperty(ref _cpuTemp, value);
    }

    public MainWindowViewModel()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                CpuTemp = $"CPU: {GetCpuTemp()}°C";
                GpuTemp = $"GPU: {GetGpuTemp()}°C";
                await Task.Delay(1000);
            }
        });
    }
    static int GetCpuTemp()
    {
        string command = "smctemp";
        string arguments = "-c";
        // Initialisation du processus
        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Démarrage du processus
        using (Process process = Process.Start(processInfo))
        {
            // Lecture de la sortie du processus
            string result = process.StandardOutput.ReadToEnd();
            return (int)float.Parse(result, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
    static int GetGpuTemp()
    {
        string command = "smctemp";
        string arguments = "-g";
        // Initialisation du processus
        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Démarrage du processus
        using (Process process = Process.Start(processInfo))
        {
            // Lecture de la sortie du processus
            string result = process.StandardOutput.ReadToEnd();
            return (int)float.Parse(result, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
