
using System.Text;
using System.Text.Json;
using Serilog;
using LSP.Types;
using LSP.Analysis;
using TrieDictionary;

namespace Main;

public class Program
{

    public static void Main()
    {
        bool keepRunning = true;

        Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            keepRunning = false;
        };

        string? basePath = Environment.GetEnvironmentVariable("PROJECTS_DIR");
        if (basePath == null)
        {
            throw new Exception("PROJECTS_DIR environment variable is not set");
        }

        string _filePath = $"{basePath}/MarkdownLSP/log.txt";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(_filePath)
            .CreateLogger();

        Log.Debug("The program has started");

        try
        {
            var literalDictionary = new LiteralDictionary();
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
        }

        State state = new State();

        Encoder encoder = new Encoder();
        using (Stream stdin = Console.OpenStandardInput())
        {
            string requestStr = "";
            while (keepRunning)
            {
                try
                {
                    requestStr = encoder.DecodeMessage(stdin);
                    if (!string.IsNullOrEmpty(requestStr))
                    {
                        HandelRequest(state, requestStr);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            }
        }
    }

    public static void HandelRequest(State state, string requestStr)
    {
        // inital request
        Request<InitializeRequestParams> request = JsonSerializer.Deserialize<Request<InitializeRequestParams>>(requestStr);

        // response parcer
        Parcer parcer = new Parcer();

        Log.Debug(request.method);


        switch (request.method)
        {
            case "initialize":
                var response = Generator.ParseInitializeRequest(request.id);
                parcer.SendRequest(response);

                Log.Debug("initialize request has been handled");
                Log.Debug($"Params: {response.result.capabilities.hoverProvider}");

                break;

            case "textDocument/didOpen":
                var requestDidOpen = JsonSerializer.Deserialize<Notification<DidOpenTextDocumentParams>>(requestStr);

                var diagnostics = state.GetDiagnosticsForFile(requestDidOpen);

                var notification = new Notification<PublishDiagnosticsParams>()
                {
                    jsonrpc = requestDidOpen.jsonrpc,
                    method = "textDocument/publishDiagnostics",
                    @params = new PublishDiagnosticsParams()
                    {
                        uri = requestDidOpen.@params.textDocument.uri,
                        diagnostics = diagnostics,
                    }
                };

                parcer.SendRequest(notification);

                break;
            case "textDocument/didChange":
                Log.Debug("To be implemented Change");

                break;
            case "textDocument/completion":
                Log.Debug("To be implemented Competition");

                break;
            case "textDocument/codeAction":
                Log.Debug("To be implemented code Action");

                break;
            case "textDocument/definition":
                Log.Debug("To be implemented definition");

                break;
            case "textDocument/hover":

                var hoverRequest = JsonSerializer.Deserialize<Request<HoverParams>>(requestStr);

                var result = state.Hover(hoverRequest);
                var respond = new Response<HoverResult>()
                {
                    jsonrpc = request.jsonrpc,
                    id = request.id,
                    result = result,
                };
                parcer.SendRequest(respond);

                break;

            case "shutdown":

                Log.Debug("Closing LSP");
                Environment.Exit(0);
                break;

            default:
                break;
        }
    }
}

public class Parcer
{
    private Encoder encoder;

    public Parcer()
    {
        encoder = new Encoder();
    }

    public void SendRequest(object Response)
    {
        string responseStr = JsonSerializer.Serialize(Response);
        Log.Debug("debug responde");
        Log.Debug(responseStr);
        byte[] buffer = encoder.EncodeMessage(responseStr);
        Console.OpenStandardOutput().Write(buffer, 0, buffer.Length);
    }
}


public class Encoder
{
    public byte[] EncodeMessage(string message)
    {
        string content = $"Content-Length: {message.Length}\r\n\r\n{message}";
        byte[] buffer = Encoding.ASCII.GetBytes(content);
        return buffer;
    }
    public string DecodeMessage(Stream stream)
    {
        // Get the header
        const string headerStr = "Content-Length: ";
        byte[] buffer = new byte[headerStr.Length];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        string headerRead = Encoding.ASCII.GetString(buffer, 0, bytesRead);

        if (string.IsNullOrEmpty(headerRead))
        {
            return "";
        }
        if (!headerStr.Equals(headerRead))
        {
            throw new Exception($" Wronhg header {headerRead}");
        }

        var sizeContent = new StringBuilder();
        byte[] B = new byte[1];
        while (true)
        {
            stream.Read(B, 0, 1);
            string Bstr = Encoding.ASCII.GetString(B, 0, 1);
            if (Bstr.Equals("\r"))
            {
                byte[] others = new byte[3];
                stream.Read(others, 0, 3);
                break;
            }
            else
            {
                sizeContent.Append(Bstr);
            }
        }
        int contentLength = int.Parse(sizeContent.ToString());

        byte[] contentBuffer = new byte[contentLength];
        stream.Read(contentBuffer, 0, contentLength);
        string content = Encoding.ASCII.GetString(contentBuffer);

        return content;
    }
}
