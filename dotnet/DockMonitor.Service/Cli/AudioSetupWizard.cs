using DockMonitor.Service.Actions;
using DockMonitor.Service.Config;
using DockMonitor.Service.Paths;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DockMonitor.Service.Cli;

public static class AudioSetupWizard
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Task<int> RunAsync()
    {
        var enumerator = new AudioDeviceEnumerator();

        var renderDevices = enumerator.GetEndpoints(DataFlowDirection.Render, DeviceStateFilter.Active | DeviceStateFilter.Unplugged);
        var captureDevices = enumerator.GetEndpoints(DataFlowDirection.Capture, DeviceStateFilter.Active | DeviceStateFilter.Unplugged);

        var allDevices = new List<AudioEndpoint>();
        allDevices.AddRange(renderDevices);
        allDevices.AddRange(captureDevices);

        if (allDevices.Count == 0)
        {
            Console.WriteLine("No audio devices found.");
            return Task.FromResult(1);
        }

        // Display table
        Console.WriteLine();
        Console.WriteLine("=== Audio Devices ===");
        Console.WriteLine();

        Console.WriteLine("  Render:");
        Console.WriteLine("  {0,-4} {1,-50} {2}", "#", "Name", "State");
        Console.WriteLine("  " + new string('-', 66));
        for (int i = 0; i < renderDevices.Count; i++)
        {
            var d = renderDevices[i];
            var state = d.IsActive ? "Active" : "Unplugged";
            Console.WriteLine("  {0,-4} {1,-50} {2}", i + 1, d.FriendlyName, state);
        }

        Console.WriteLine();
        Console.WriteLine("  Capture:");
        Console.WriteLine("  {0,-4} {1,-50} {2}", "#", "Name", "State");
        Console.WriteLine("  " + new string('-', 66));
        for (int i = 0; i < captureDevices.Count; i++)
        {
            var d = captureDevices[i];
            var state = d.IsActive ? "Active" : "Unplugged";
            Console.WriteLine("  {0,-4} {1,-50} {2}", renderDevices.Count + i + 1, d.FriendlyName, state);
        }

        Console.WriteLine();

        // Collect Docked profile
        Console.WriteLine("--- Docked profile ---");
        var dockedAudio = CollectAudioConfig(renderDevices, captureDevices, allDevices);

        Console.WriteLine();

        // Collect Undocked profile
        Console.WriteLine("--- Undocked profile ---");
        var undockedAudio = CollectAudioConfig(renderDevices, captureDevices, allDevices);

        // Write to config
        WriteToConfig(dockedAudio, undockedAudio);

        Console.WriteLine();
        Console.WriteLine($"Audio configuration saved to {ConfigPaths.DefaultConfigPath}");
        return Task.FromResult(0);
    }

    private static AudioConfig CollectAudioConfig(
        List<AudioEndpoint> renderDevices,
        List<AudioEndpoint> captureDevices,
        List<AudioEndpoint> allDevices)
    {
        var config = new AudioConfig();

        config.RenderDefault = AskDevice("  Render Default", allDevices, renderDevices.Count);
        config.RenderMultimedia = AskDevice("  Render Multimedia", allDevices, renderDevices.Count, config.RenderDefault);
        config.RenderCommunications = AskDevice("  Render Communications", allDevices, renderDevices.Count);

        config.CaptureDefault = AskDevice("  Capture Default", allDevices, renderDevices.Count);
        config.CaptureMultimedia = AskDevice("  Capture Multimedia", allDevices, renderDevices.Count, config.CaptureDefault);
        config.CaptureCommunications = AskDevice("  Capture Communications", allDevices, renderDevices.Count);

        return config;
    }

    private static string? AskDevice(string prompt, List<AudioEndpoint> allDevices, int renderCount, string? defaultHint = null)
    {
        var hintText = defaultHint is not null ? $" [enter = same as above: {defaultHint}]" : " [enter to skip]";

        while (true)
        {
            Console.Write($"{prompt}{hintText}: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
            {
                return defaultHint;
            }

            if (int.TryParse(input, out var num) && num >= 1 && num <= allDevices.Count)
            {
                return allDevices[num - 1].FriendlyName;
            }

            Console.WriteLine($"    Invalid number. Enter 1-{allDevices.Count} or press Enter.");
        }
    }

    private static void WriteToConfig(AudioConfig dockedAudio, AudioConfig undockedAudio)
    {
        AppPaths.EnsureDataDir();
        ConfigPaths.EnsureDefaultConfigExists();

        var configPath = ConfigPaths.DefaultConfigPath;
        var json = File.ReadAllText(configPath);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch
        {
            root = new JsonObject();
        }

        root ??= new JsonObject();

        SetAudioNode(root, "Docked", dockedAudio);
        SetAudioNode(root, "Undocked", undockedAudio);

        var output = root.ToJsonString(JsonOpts);
        File.WriteAllText(configPath, output);
    }

    private static void SetAudioNode(JsonNode root, string profileName, AudioConfig audio)
    {
        var profile = root[profileName];
        if (profile is null)
        {
            profile = new JsonObject();
            root[profileName] = profile;
        }

        // Remove old AudioProfile field if present
        if (profile is JsonObject profileObj)
        {
            profileObj.Remove("AudioProfile");
        }

        var audioNode = new JsonObject();

        if (audio.RenderDefault is not null)
            audioNode["RenderDefault"] = audio.RenderDefault;
        if (audio.RenderMultimedia is not null)
            audioNode["RenderMultimedia"] = audio.RenderMultimedia;
        if (audio.RenderCommunications is not null)
            audioNode["RenderCommunications"] = audio.RenderCommunications;
        if (audio.CaptureDefault is not null)
            audioNode["CaptureDefault"] = audio.CaptureDefault;
        if (audio.CaptureMultimedia is not null)
            audioNode["CaptureMultimedia"] = audio.CaptureMultimedia;
        if (audio.CaptureCommunications is not null)
            audioNode["CaptureCommunications"] = audio.CaptureCommunications;

        profile["Audio"] = audioNode;
    }
}
