using System.Text;

namespace ArturRios.Hermes.WebApi.Tests;

/// <summary>
/// Replaces the console's output for as long as it lives, so a test can read what the application logged.
/// </summary>
internal sealed class ConsoleCapture : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly TextWriter _original = Console.Out;
    private readonly RecordingWriter _writer = new();

    public ConsoleCapture() => Console.SetOut(_writer);

    public string Text => _writer.Text;

    /// <summary>
    /// Waits for <paramref name="expected"/> to be written. A request is logged after its response has started, so
    /// a test asserting that something is absent must first see the log line it is inspecting.
    /// </summary>
    public async Task WaitForAsync(string expected)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (!Text.Contains(expected, StringComparison.Ordinal))
        {
            Assert.True(DateTime.UtcNow < deadline, $"Nothing containing '{expected}' was logged. Logged: {Text}");

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
    }

    public void Dispose() => Console.SetOut(_original);

    private sealed class RecordingWriter : TextWriter
    {
        private readonly StringBuilder _text = new();

        public override Encoding Encoding => Encoding.UTF8;

        public string Text
        {
            get
            {
                lock (_text)
                {
                    return _text.ToString();
                }
            }
        }

        public override void Write(char value)
        {
            lock (_text)
            {
                _text.Append(value);
            }
        }

        public override void Write(string? value)
        {
            lock (_text)
            {
                _text.Append(value);
            }
        }
    }
}
