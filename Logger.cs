using System;

// console output for a human watching the run. flip Verbose off to run silent
// once the websocket feed is the real consumer.
static class Logger
{
    public static bool Verbose = true;

    public static void Log(string message)
    {
        if (Verbose)
            Console.WriteLine(message);
    }

    // startup banners and shutdown: printed regardless of Verbose
    public static void Always(string message) => Console.WriteLine(message);
}
