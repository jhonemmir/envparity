using System.Text;
using EnvParity.Cli;

var useUnicode = TryEnableUtf8Output();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    return CliApplication.Run(args, Console.Out, Console.Error, useUnicode, cancellation.Token);
}
catch (Exception)
{
    // No stack trace and no exception text is ever shown (D13).
    Console.Error.WriteLine("envparity: unexpected error");
    return 2;
}

static bool TryEnableUtf8Output()
{
    try
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        return true;
    }
    catch (Exception)
    {
        return false;
    }
}
