using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using CliWrap;
using Microsoft.Win32.SafeHandles;

namespace MemoryPcapng;

public enum TSharkOutputMode
{
    Fields,
    Pdml,
    Json
}

public class TShark : IDisposable
{
    private StringBuilder _sbErr = new StringBuilder(); // TODO: Make public?

    private TSharkOutputMode _mode;
    private int _packetIndex = 0;
    private ManualResetEvent _packetArrived = new ManualResetEvent(false);

    private CommandTask<CommandResult> _task { get; set; }
    private Task _readerTask;
    public NamedPipeServerStream Pipe { get; private set; }

    public event EventHandler<TSharkOutputEvent> NewPacketLine;

    public TShark(string tsharkPath, TSharkOutputMode mode)
    {
        _mode = mode;
        _sbErr = new StringBuilder();

        string pipeName = "memorypcapng_" + (new Random()).Next();
        Pipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var localPipeServer = new AnonymousPipeServerStream(PipeDirection.Out);
        var handle = localPipeServer.ClientSafePipeHandle;

        string args = $@"-i \\.\pipe\{pipeName} -l ";
        switch (mode)
        {
            case TSharkOutputMode.Fields:
                args +=
                    $" -T fields -e _ws.col.No. -e _ws.col.Time -e _ws.col.Source -e _ws.col.Destination -e _ws.col.Protocol -e _ws.col.Length -e _ws.col.Info -Eseparator=~ -t d";
                _readerTask = Task.Run(() => { HandleFieldsStdOutput(handle); });
                break;
            case TSharkOutputMode.Pdml:
                args += " -T pdml";
                _readerTask = Task.Run(() => { HandlePdmlStdOutput(handle); });
                break;
            case TSharkOutputMode.Json:
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        var command = CliWrap.Cli.Wrap(tsharkPath)
                //.WithArguments("-T fields")
                .WithArguments(args)
                //.WithStandardOutputPipe(PipeTarget.ToDelegate(HandleNewLine, Encoding.UTF8))
                .WithStandardOutputPipe(PipeTarget.ToStream(localPipeServer))
                //.WithStandardErrorPipe(PipeTarget.ToStringBuilder(_sbErr, Encoding.UTF8))
                //.WithStandardErrorPipe(PipeTarget.ToDelegate(x => Debug.WriteLine($"[TSHARK STDERR] {x}")))
                ;

        _task = command.ExecuteAsync();
        Pipe.WaitForConnection();
    }

    private void HandlePdmlStdOutput(SafePipeHandle handle)
    {
        AnonymousPipeClientStream localPipe = new AnonymousPipeClientStream(PipeDirection.In, handle);
        using StreamReader sr = new StreamReader(localPipe);

        StringBuilder ongoingXml = new StringBuilder();
        while (true)
        {
            string line = sr.ReadLine();

            if (line == null)
                break;

            if (line == "<packet>")
            {
                Debug.WriteLine($"[V][{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff}] ReadLine returned --- packet start");
                // New packet started
                ongoingXml.Clear();
                ongoingXml.AppendLine("<pdml version=\"0\" creator=\"wireshark/4.0.7\">");
            }
            ongoingXml.AppendLine(line);
            if (line == "</packet>")
            {
                Debug.WriteLine($"[V][{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff}] ReadLine returned --- packet end");
                // Packet ended
                ongoingXml.AppendLine("</pdml>");
                string xml = ongoingXml.ToString();
                HandleNewLine(xml);
            }
        }
    }

    private void HandleFieldsStdOutput(SafePipeHandle handle)
    {
        AnonymousPipeClientStream localPipe = new AnonymousPipeClientStream(PipeDirection.In, handle);
        using StreamReader sr = new StreamReader(localPipe);

        while (true)
        {
            string line = sr.ReadLine();
            if (line == null)
                break;

            // Expecting: Frame Number, Time, Src, Dest, Protocol, Length, Info
            string[] sub = line.Split('~');
            string newLine =
                $"{sub[0],-10} {sub[1],-15} {sub[2],-18} {sub[3],-18} {sub[4],-8} {sub[5],-8} {sub[6]}";

            HandleNewLine(newLine);
        }
        // TODO: Close anon pipe?
    }

    private void HandleNewLine(string newLine)
    {
        NewPacketLine?.Invoke(this, new TSharkOutputEvent(_packetIndex, newLine));

        _packetIndex++;
        _packetArrived.Set();
    }

    public void WaitForPackets(int packetCount)
    {
        while (true)
        {
            _packetArrived.Reset();
            if (_packetIndex >= packetCount)
                return;
            _packetArrived.WaitOne();
        }
    }

    public Task WaitForPacketsAsync(int packetCount)
    {
        return Task.Run(() =>
        {
            while (true)
            {
                _packetArrived.Reset();
                if (_packetIndex >= packetCount)
                    return;
                _packetArrived.WaitOne();
            }
        });
    }

    public void Dispose()
    {
        Debug.WriteLine($"[{DateTime.Now}] Disposing!!!!");

        Close();

        Debug.WriteLine($"[{DateTime.Now}] Disposed!!!!");
    }

    public void Close()
    {

        Debug.WriteLine($"[{DateTime.Now}] Closing!!!!");
        try
        {
            Pipe?.Close();
            Pipe?.Dispose();
            Pipe = null;
            if (_task != null && !_task.Task.IsFaulted)
                _task?.Task.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch
        {
        }

        try
        {
            if (!_task.Task.IsCompleted)
                _task?.Dispose();
            _task = null;
        }
        catch
        {
        }
        Debug.WriteLine($"[{DateTime.Now}] Closed!!!!");
    }

    public static Task<TShark> CreateNew(string tsharkPath, TSharkOutputMode mode)
    {
        return Task.Run(() => new TShark(tsharkPath, mode));
    }
}
