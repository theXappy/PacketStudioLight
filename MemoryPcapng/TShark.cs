using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using CliWrap;

namespace MemoryPcapng;

public class TShark : IDisposable
{
    private StringBuilder _sbErr = new StringBuilder(); // TODO: Make public?
    
    private int _packetIndex = 0;
    private object _packetIndexLock = new();
    private ManualResetEvent _packetArrived = new ManualResetEvent(false);

    private CommandTask<CommandResult> _task { get; set; }
    private Task _readerTask;
    public NamedPipeServerStream Pipe { get; private set; }

    public event EventHandler<TSharkOutputEvent> NewPacketLine;

    public TShark(string tsharkPath, bool immediateFlush = true)
    {
        _sbErr = new StringBuilder();

        string pipeName = "memorypcapng_" + (new Random()).Next();
        Pipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var localPipeServer = new AnonymousPipeServerStream(PipeDirection.Out);
        var handle = localPipeServer.ClientSafePipeHandle;

        _readerTask = Task.Run(() =>
        {
            AnonymousPipeClientStream localPipe = new AnonymousPipeClientStream(PipeDirection.In, handle);
            using StreamReader sr = new StreamReader(localPipe);
            while (true)
            {
                string line = sr.ReadLine();
                if (line == null)
                    break;
                HandleNewLine(line);
            }
            // TODO: Close anon pipe?
        });

        string args = $"-i \\\\.\\pipe\\{pipeName} ";
        if (immediateFlush)
            args += "-l";

        var command = CliWrap.Cli.Wrap(tsharkPath)
            //.WithArguments("-T fields")
            .WithArguments(args)
            //.WithStandardOutputPipe(PipeTarget.ToDelegate(HandleNewLine, Encoding.UTF8))
            .WithStandardOutputPipe(PipeTarget.ToStream(localPipeServer))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(_sbErr, Encoding.UTF8));

        _task = command.ExecuteAsync();
        Pipe.WaitForConnection();
    }

    private void HandleNewLine(string obj)
    {
        lock (_packetIndexLock)
        {
            NewPacketLine?.Invoke(this, new TSharkOutputEvent(_packetIndex, obj));
            _packetIndex++;
            _packetArrived.Set();
        }
    }

    public void WaitForPackets(int packetCount)
    {
        while (true)
        {
            lock (_packetIndexLock)
            {
                if (_packetIndex >= packetCount)
                    return;
                _packetArrived.Reset();
            }
            _packetArrived.WaitOne();
        }
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
            Pipe?.Dispose();
            Pipe = null;
            _task?.Task.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch
        {
        }

        try
        {
            _task?.Dispose();
            _task = null;
        }
        catch
        {
        }
        Debug.WriteLine($"[{DateTime.Now}] Closed!!!!");
    }
}
